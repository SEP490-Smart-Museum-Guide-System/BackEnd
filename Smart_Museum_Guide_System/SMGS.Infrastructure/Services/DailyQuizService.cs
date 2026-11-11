using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

public class DailyQuizService(ApplicationDbContext db, TimeProvider clock, IOptions<ExperienceOptions> options) : IDailyQuizService
{
    public static DateOnly LocalDate(DateTime utc, string zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById(zone)));
    public async Task UnlockAsync(RecognitionHistory r)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Unlock requires the recognition transaction.");
        if (!r.IsSuccess || r.ArtifactId is null || r.Id == 0) throw new ExperienceException("Cáº§n láº§n nháº­n diá»‡n thÃ nh cÃ´ng Ä‘Ã£ lÆ°u.");
        var date = LocalDate(r.RecognizedAt, options.Value.TimeZoneId);
        var quizzes = await db.Set<Quiz>().Where(q => q.ArtifactId == r.ArtifactId && q.Status == "published").Select(q => q.Id).ToListAsync();
        var existing = await db.Set<DailyQuizEntry>().Where(e => e.UserId == r.UserId && e.EntryDate == date).Select(e => e.QuizId).ToListAsync();
        foreach (var id in quizzes.Except(existing)) db.Add(new DailyQuizEntry { UserId = r.UserId, QuizId = id, ArtifactId = r.ArtifactId.Value,
            RecognitionId = r.Id, VisitId = r.VisitId, EntryDate = date, CreatedAt = r.RecognizedAt });
    }
    public async Task<IReadOnlyList<DailyQuizEntry>> GetTodayAsync(Guid user)
    {
        var date = LocalDate(clock.GetUtcNow().UtcDateTime, options.Value.TimeZoneId);
        return await db.Set<DailyQuizEntry>().AsNoTracking().Where(x => x.UserId == user && x.EntryDate == date).OrderBy(x => x.Id).ToListAsync();
    }
    public async Task SetDeferredAsync(long id, bool deferred, Guid user)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await ExperienceAccess.LockUser(db, user);
        var date = LocalDate(clock.GetUtcNow().UtcDateTime, options.Value.TimeZoneId);
        var entry = await db.Set<DailyQuizEntry>().SingleOrDefaultAsync(e => e.Id == id && e.UserId == user && e.EntryDate == date)
            ?? throw new ExperienceException("Không tìm thấy quiz trong ngày của bạn.", 404);
        if (entry.Status is not ("pending" or "deferred"))
            throw new ExperienceException("Quiz đã bắt đầu hoặc hoàn thành. Hãy mở lại lượt làm trong lịch sử.", 409);
        entry.Status = deferred ? "deferred" : "pending";
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
