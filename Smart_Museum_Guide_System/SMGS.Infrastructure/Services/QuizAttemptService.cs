using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

public class QuizAttemptService(ApplicationDbContext db, IGamificationService rewards, ICommerceService commerce,
    TimeProvider clock, IOptions<ExperienceOptions> options) : IQuizAttemptService
{
    private async Task Access(int artifactId, Guid user)
    {
        if (!await db.Artifacts.AnyAsync(a => a.Id == artifactId && a.IsActive && a.Museum!.IsActive)) throw new ExperienceException("Hiện vật không khả dụng.", 404);
        if (!await commerce.CanReadArtifact(artifactId, user)) throw new ExperienceException("Bạn chưa có quyền sử dụng nội dung hiện vật.", 403);
    }
    private async Task<QuizAttempt> Create(DailyQuizEntry entry, string mode, Guid user, long? previous)
    {
        if (mode is not ("immediate" or "later")) throw new ExperienceException("Chế độ làm bài không hợp lệ.");
        if (entry.EntryDate != DailyQuizService.LocalDate(clock.GetUtcNow().UtcDateTime, options.Value.TimeZoneId))
            throw new ExperienceException("Quiz thuộc ngày trước; hãy quét QR lại để mở quiz hôm nay.", 409);
        await Access(entry.ArtifactId, user);
        var quiz = await db.Set<Quiz>().Include(q => q.Questions).SingleOrDefaultAsync(q => q.Id == entry.QuizId && q.Status == "published")
            ?? throw new ExperienceException("Quiz chưa xuất bản.", 404);
        var used = previous is long old ? await db.Set<QuizAttemptAnswer>().Where(a => a.AttemptId == old).Select(a => a.QuestionId).ToListAsync() : [];
        var ids = quiz.Questions.Select(q => q.QuestionId).Except(used).ToArray();
        var available = await db.Set<Question>().Where(q => ids.Contains(q.Id) && q.Status == "approved" && q.ArtifactId == quiz.ArtifactId).Select(q => q.Id).ToListAsync();
        if (available.Count < quiz.QuestionsPerAttempt) throw new ExperienceException("Chưa đủ câu hỏi mới đã duyệt để tạo lượt làm khác.", 409);
        var selected = available.OrderBy(_ => Random.Shared.Next()).Take(quiz.QuestionsPerAttempt).ToArray();
        var attempt = new QuizAttempt { UserId = user, DailyEntryId = entry.Id, QuizId = quiz.Id, Mode = mode,
            TotalQuestions = selected.Length, StartedAt = clock.GetUtcNow().UtcDateTime,
            Answers = selected.Select((id, i) => new QuizAttemptAnswer { QuestionId = id, OrderNo = i + 1 }).ToList() };
        entry.Status = "in_progress"; return attempt;
    }
    public async Task<AttemptResult> StartAsync(StartAttemptRequest request, Guid user)
    {
        await using var tx = await db.Database.BeginTransactionAsync(); await ExperienceAccess.LockUser(db, user);
        var entry = await db.Set<DailyQuizEntry>().SingleOrDefaultAsync(x => x.Id == request.DailyEntryId && x.UserId == user)
            ?? throw new ExperienceException("Quiz chưa được mở bằng QR hoặc không thuộc tài khoản.", 404);
        var active = await db.Set<QuizAttempt>().Include(x => x.Answers).SingleOrDefaultAsync(x => x.DailyEntryId == entry.Id && x.Status == "in_progress");
        if (active is not null) { await Access(entry.ArtifactId, user); return await Project(active); }
        var previous = await db.Set<QuizAttempt>().Where(x => x.DailyEntryId == entry.Id).OrderByDescending(x => x.Id).Select(x => (long?)x.Id).FirstOrDefaultAsync();
        var attempt = await Create(entry, request.Mode, user, previous); db.Add(attempt); await db.SaveChangesAsync(); await tx.CommitAsync(); return await Project(attempt);
    }
    public async Task<AttemptResult> AnswerAsync(long id, AnswerRequest request, Guid user)
    {
        await using var tx = await db.Database.BeginTransactionAsync(); await ExperienceAccess.LockUser(db, user);
        var attempt = await Load(id, user);
        var entry = await db.Set<DailyQuizEntry>().SingleAsync(x => x.Id == attempt.DailyEntryId); await Access(entry.ArtifactId, user);
        var answer = attempt.Answers.SingleOrDefault(x => x.QuestionId == request.QuestionId) ?? throw new ExperienceException("Câu hỏi không thuộc lượt làm này.");
        if (answer.AnsweredAt is not null) {
            if (answer.SelectedOptionId != request.OptionId) throw new ExperienceException("Câu hỏi đã được trả lời, không thể đổi đáp án.", 409);
            return await Project(attempt);
        }
        if (attempt.Status != "in_progress") throw new ExperienceException("Lượt làm đã kết thúc.", 409);
        var option = await db.Set<QuestionOption>().SingleOrDefaultAsync(x => x.Id == request.OptionId && x.QuestionId == request.QuestionId)
            ?? throw new ExperienceException("Lựa chọn không thuộc câu hỏi.");
        answer.SelectedOptionId = option.Id; answer.IsCorrect = option.IsCorrect; answer.AnsweredAt = clock.GetUtcNow().UtcDateTime;
        if (option.IsCorrect) { attempt.CorrectCount++; answer.Points = await rewards.AwardQuizPointsAsync(user, request.QuestionId); }
        attempt.Score = (int)Math.Round(100d * attempt.CorrectCount / attempt.TotalQuestions);
        if (attempt.Answers.All(a => a.AnsweredAt is not null)) {
            attempt.Status = "completed"; attempt.CompletedAt = clock.GetUtcNow().UtcDateTime; entry.Status = "completed";
        }
        await db.SaveChangesAsync();
        var badges = await rewards.EvaluateBadgesAsync(user);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return (await Project(attempt)) with { NewBadges = badges.Select(b => new EarnedBadge(b.Id, b.Name)).ToList() };
    }
    public async Task<AttemptResult> ResetAsync(long id, Guid user)
    {
        await using var tx = await db.Database.BeginTransactionAsync(); await ExperienceAccess.LockUser(db, user);
        var old = await Load(id, user);
        var newer = await db.Set<QuizAttempt>().Include(a => a.Answers).Where(a => a.DailyEntryId == old.DailyEntryId && a.Id > old.Id).OrderBy(a => a.Id).FirstOrDefaultAsync();
        var entry = await db.Set<DailyQuizEntry>().SingleAsync(e => e.Id == old.DailyEntryId);
        await Access(entry.ArtifactId, user);
        if (newer is not null) return await Project(newer); // Retry reset of an old attempt returns its successor.
        var next = await Create(entry, old.Mode, user, old.Id);
        // Save the old status before inserting the next active attempt (filtered unique index).
        if (old.Status == "in_progress") { old.Status = "abandoned"; old.CompletedAt = clock.GetUtcNow().UtcDateTime; }
        await db.SaveChangesAsync(); db.Add(next); await db.SaveChangesAsync(); await tx.CommitAsync(); return await Project(next);
    }
    public async Task<AttemptResult> GetResultsAsync(long id, Guid user)
    {
        var attempt = await Load(id, user);
        if (attempt.Status == "in_progress") { var artifact = await db.Set<Quiz>().Where(q => q.Id == attempt.QuizId).Select(q => q.ArtifactId).SingleAsync(); await Access(artifact, user); }
        return await Project(attempt);
    }
    public async Task<IReadOnlyList<QuizAttempt>> ListAsync(Guid user, int page, int pageSize)
    {
        var (skip, take) = ExperienceAccess.Page(page, pageSize);
        return await db.Set<QuizAttempt>().AsNoTracking().Where(x => x.UserId == user).OrderByDescending(x => x.Id).Skip(skip).Take(take).ToListAsync();
    }
    private async Task<QuizAttempt> Load(long id, Guid user) => await db.Set<QuizAttempt>().Include(x => x.Answers).SingleOrDefaultAsync(x => x.Id == id && x.UserId == user)
        ?? throw new ExperienceException("Không tìm thấy lượt làm quiz.", 404);
    private async Task<AttemptResult> Project(QuizAttempt a)
    {
        var ids = a.Answers.Select(x => x.QuestionId).ToArray();
        var questions = await db.Set<Question>().AsNoTracking().Where(q => ids.Contains(q.Id)).Include(q => q.Options).ToDictionaryAsync(q => q.Id);
        return new(a.Id, a.DailyEntryId, a.Status, a.TotalQuestions, a.CorrectCount, a.Score, a.StartedAt, a.CompletedAt,
            a.Answers.OrderBy(x => x.OrderNo).Select(answer => {
                var q = questions[answer.QuestionId];
                return new AttemptQuestion(q.Id, q.Content, q.Options.OrderBy(o => o.OrderNo).Select(o => new PublicOption(o.Id, o.Content)).ToList(),
                    answer.SelectedOptionId, answer.AnsweredAt is null ? null : answer.IsCorrect, answer.AnsweredAt is null ? null : q.Explanation);
            }).ToList()) { PointsEarned = a.Answers.Sum(answer => answer.Points) };
    }
}
