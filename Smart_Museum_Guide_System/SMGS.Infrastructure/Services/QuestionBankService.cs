using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

public class QuestionBankService(ApplicationDbContext db, TimeProvider clock) : IQuestionBankService
{
    private static void Validate(QuestionRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Content) || r.Content.Length > 4000 || r.Explanation is null || r.Explanation.Length > 4000 ||
            string.IsNullOrWhiteSpace(r.LanguageCode) || r.LanguageCode.Length > 35 || r.Options is null || r.Options.Length is < 2 or > 6 ||
            r.Options.Any(x => x is null || string.IsNullOrWhiteSpace(x.Content) || x.Content.Length > 1000) ||
            r.Options.Count(x => x.IsCorrect) != 1 || r.Options.Select(x => x.Content.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != r.Options.Length)
            throw new ExperienceException("Câu hỏi cần 2–6 lựa chọn khác nhau, đúng một đáp án đúng.");
    }
    private static void Assign(Question q, QuestionRequest r)
    {
        q.Content = r.Content.Trim(); q.Explanation = r.Explanation.Trim(); q.LanguageCode = r.LanguageCode.Trim().ToLowerInvariant(); q.Status = "draft";
        q.ReviewedBy = null; q.ReviewedAt = null; q.ReviewNote = null;
        q.Options = r.Options.Select((o, i) => new QuestionOption { Content = o.Content.Trim(), IsCorrect = o.IsCorrect, OrderNo = i + 1 }).ToList();
    }
    public async Task<Question> CreateAsync(int artifactId, QuestionRequest request, Guid user, bool admin)
    {
        await ExperienceAccess.Artifact(db, artifactId, user, admin); Validate(request);
        var q = new Question { ArtifactId = artifactId, CreatedBy = user, CreatedAt = clock.GetUtcNow().UtcDateTime }; Assign(q, request);
        db.Add(q); await db.SaveChangesAsync(); return q;
    }
    public async Task<Question> UpdateAsync(int id, QuestionRequest request, Guid user, bool admin)
    {
        Validate(request); await using var tx = await db.Database.BeginTransactionAsync();
        var q = await db.Set<Question>().FromSqlInterpolated($"SELECT * FROM questions WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync()
            ?? throw new ExperienceException("Không tìm thấy câu hỏi.", 404);
        await ExperienceAccess.Artifact(db, q.ArtifactId, user, admin);
        if (q.Status == "approved") throw new ExperienceException("Câu hỏi đã duyệt được giữ nguyên cho lịch sử; hãy tạo câu mới.", 409);
        await db.Entry(q).Collection(x => x.Options).LoadAsync(); db.RemoveRange(q.Options); Assign(q, request);
        await db.SaveChangesAsync(); await tx.CommitAsync(); return q;
    }
    public async Task<Question> ReviewQuestionAsync(int id, ReviewQuestionRequest request, Guid user, bool admin)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var q = await db.Set<Question>().FromSqlInterpolated($"SELECT * FROM questions WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync()
            ?? throw new ExperienceException("Không tìm thấy câu hỏi.", 404);
        await ExperienceAccess.Artifact(db, q.ArtifactId, user, admin);
        if (q.Status == "approved") throw new ExperienceException("Câu hỏi đã duyệt, không thay đổi để bảo toàn kết quả.", 409);
        if (!request.Approve && string.IsNullOrWhiteSpace(request.Note)) throw new ExperienceException("Cần lý do từ chối.");
        q.Status = request.Approve ? "approved" : "rejected"; q.ReviewedBy = user; q.ReviewedAt = clock.GetUtcNow().UtcDateTime; q.ReviewNote = request.Note?.Trim();
        await db.SaveChangesAsync(); await tx.CommitAsync(); await db.Entry(q).Collection(x => x.Options).LoadAsync(); return q;
    }
    public async Task<IReadOnlyList<Question>> ListAsync(int artifactId, Guid user, bool admin, int page, int pageSize)
    {
        await ExperienceAccess.Artifact(db, artifactId, user, admin); var (skip, take) = ExperienceAccess.Page(page, pageSize);
        return await db.Set<Question>().AsNoTracking().Where(x => x.ArtifactId == artifactId).OrderByDescending(x => x.Id).Skip(skip).Take(take)
            .Include(x => x.Options.OrderBy(o => o.OrderNo)).ToListAsync();
    }
    public async Task<Quiz> CreateQuizAsync(int artifactId, QuizRequest request, Guid user, bool admin)
    {
        await ExperienceAccess.Artifact(db, artifactId, user, admin);
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200 || request.QuestionIds is null || request.QuestionIds.Length is < 1 or > 100 ||
            request.QuestionIds.Distinct().Count() != request.QuestionIds.Length || request.QuestionsPerAttempt is < 1 or > 20 || request.QuestionsPerAttempt > request.QuestionIds.Length)
            throw new ExperienceException("Danh sách câu hỏi và số câu mỗi lượt không hợp lệ.");
        var qs = await db.Set<Question>().Where(q => request.QuestionIds.Contains(q.Id)).ToListAsync();
        if (qs.Count != request.QuestionIds.Length || qs.Any(q => q.ArtifactId != artifactId || q.Status != "approved") || qs.Select(q => q.LanguageCode).Distinct().Count() != 1)
            throw new ExperienceException("Quiz chỉ dùng câu hỏi đã duyệt, cùng hiện vật và ngôn ngữ.");
        var quiz = new Quiz { ArtifactId = artifactId, Title = request.Title.Trim(), CreatedBy = user, QuestionsPerAttempt = request.QuestionsPerAttempt,
            CreatedAt = clock.GetUtcNow().UtcDateTime, Questions = request.QuestionIds.Select(id => new QuizQuestion { QuestionId = id }).ToList() };
        db.Add(quiz); await db.SaveChangesAsync(); return quiz;
    }
    public async Task<Quiz> PublishQuizAsync(int id, Guid user, bool admin)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var quiz = await db.Set<Quiz>().FromSqlInterpolated($"SELECT * FROM quizzes WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync()
            ?? throw new ExperienceException("Không tìm thấy quiz.", 404);
        await ExperienceAccess.Artifact(db, quiz.ArtifactId, user, admin); await db.Entry(quiz).Collection(x => x.Questions).LoadAsync();
        var ids = quiz.Questions.Select(x => x.QuestionId).ToArray();
        if (ids.Length < quiz.QuestionsPerAttempt || await db.Set<Question>().CountAsync(q => ids.Contains(q.Id) && q.Status == "approved" && q.ArtifactId == quiz.ArtifactId) != ids.Length)
            throw new ExperienceException("Quiz chưa đủ câu hỏi hợp lệ.", 409);
        quiz.Status = "published"; await db.SaveChangesAsync(); await tx.CommitAsync(); return quiz;
    }
    public async Task<IReadOnlyList<Quiz>> ListQuizzesAsync(int artifactId, Guid user, bool admin)
    {
        await ExperienceAccess.Artifact(db, artifactId, user, admin);
        return await db.Set<Quiz>().AsNoTracking().Where(q => q.ArtifactId == artifactId).OrderByDescending(q => q.Id).Take(100).Include(q => q.Questions).ToListAsync();
    }
}
