using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

public class GamificationService(ApplicationDbContext db, TimeProvider clock, IOptions<ExperienceOptions> options) : IGamificationService
{
    private async Task<int> Award(Guid user, string reason, string type, long id, int points)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Awards require a user-locked transaction.");
        if (points == 0 || await db.Set<PointTransaction>().AnyAsync(p => p.UserId == user && p.Reason == reason && p.RefType == type && p.RefId == id) ||
            db.Set<PointTransaction>().Local.Any(p => p.UserId == user && p.Reason == reason && p.RefType == type && p.RefId == id)) return 0;
        db.Add(new PointTransaction { UserId = user, Reason = reason, RefType = type, RefId = id, Points = points, CreatedAt = clock.GetUtcNow().UtcDateTime }); return points;
    }
    public async Task AwardScanPointsAsync(Guid user, int artifactId) => await Award(user, "scan_artifact", "artifact", artifactId, options.Value.ScanPoints);
    public Task<int> AwardQuizPointsAsync(Guid user, int questionId) => Award(user, "quiz_correct", "question", questionId, options.Value.CorrectAnswerPoints);
    public async Task<IReadOnlyList<Badge>> EvaluateBadgesAsync(Guid user)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Badge grants require a user-locked transaction.");
        var awarded = new List<Badge>();
        var owned = await db.Set<UserBadge>().Where(b => b.UserId == user).Select(b => b.BadgeId).ToListAsync();
        foreach (var badge in await db.Set<Badge>().Where(b => !owned.Contains(b.Id)).ToListAsync()) {
            var rule = JsonSerializer.Deserialize<BadgeRule>(badge.RuleJson);
            if (rule is null || rule.Threshold < 1) continue;
            int count;
            if (rule.Metric == "scan_artifacts") count = await (from r in db.Set<RecognitionHistory>() join a in db.Artifacts on r.ArtifactId equals a.Id
                where r.UserId == user && r.IsSuccess && (badge.MuseumId == null || a.MuseumId == badge.MuseumId) select a.Id).Distinct().CountAsync();
            else if (rule.Metric == "quiz_correct") count = await (from answer in db.Set<QuizAttemptAnswer>() join attempt in db.Set<QuizAttempt>() on answer.AttemptId equals attempt.Id
                join q in db.Set<Question>() on answer.QuestionId equals q.Id join a in db.Artifacts on q.ArtifactId equals a.Id
                where attempt.UserId == user && answer.IsCorrect && answer.AnsweredAt != null && (badge.MuseumId == null || a.MuseumId == badge.MuseumId) select q.Id).Distinct().CountAsync();
            else continue;
            if (count >= rule.Threshold) {
                db.Add(new UserBadge { UserId = user, BadgeId = badge.Id, AwardedAt = clock.GetUtcNow().UtcDateTime });
                awarded.Add(badge);
            }
        }
        return awarded;
    }
    public async Task<long> GetBalanceAsync(Guid user) => await db.Set<PointTransaction>().Where(x => x.UserId == user).SumAsync(x => (long)x.Points);
    public async Task<IReadOnlyList<PointTransaction>> GetTransactionsAsync(Guid user, int page, int pageSize) {
        var (skip, take) = ExperienceAccess.Page(page, pageSize);
        return await db.Set<PointTransaction>().AsNoTracking().Where(x => x.UserId == user).OrderByDescending(x => x.Id).Skip(skip).Take(take).ToListAsync();
    }
    public async Task<IReadOnlyList<UserBadge>> GetBadgesAsync(Guid user) => await db.Set<UserBadge>().AsNoTracking().Where(x => x.UserId == user).OrderBy(x => x.BadgeId).ToListAsync();
    public async Task<Badge> CreateBadgeAsync(BadgeRequest request) {
        if (request.Metric is not ("scan_artifacts" or "quiz_correct") || request.Threshold < 1 || request.Threshold > 100000 || string.IsNullOrWhiteSpace(request.Code) ||
            request.Code.Length > 50 || string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200) throw new ExperienceException("Quy tắc huy hiệu không hợp lệ.");
        if (request.MuseumId is int m && !await db.Museums.AnyAsync(x => x.Id == m)) throw new ExperienceException("Không tìm thấy bảo tàng.", 404);
        var badge = new Badge { Code = request.Code.Trim().ToUpperInvariant(), Name = request.Name.Trim(), MuseumId = request.MuseumId,
            RuleJson = JsonSerializer.Serialize(new BadgeRule(request.Metric, request.Threshold)) };
        if (await db.Set<Badge>().AnyAsync(x => x.Code == badge.Code)) throw new ExperienceException("Mã huy hiệu đã tồn tại.", 409);
        db.Add(badge); await db.SaveChangesAsync(); return badge;
    }
    public async Task<IReadOnlyList<Badge>> ListBadgesAsync() => await db.Set<Badge>().AsNoTracking().OrderBy(x => x.Id).ToListAsync();
}
