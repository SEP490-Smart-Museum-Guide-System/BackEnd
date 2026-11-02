using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

public class RecognitionService(ApplicationDbContext db, IVisitProgressService progress, IDailyQuizService daily,
    IGamificationService rewards, TimeProvider clock) : IRecognitionService
{
    public async Task<ScanResult> RecordQrAsync(ScanQrRequest request, Guid user)
    {
        if (request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.QrCode) || request.QrCode.Length > 200)
            throw new ExperienceException("Cần QR hợp lệ và RequestId UUID để chống gửi trùng.");
        await using var tx = await db.Database.BeginTransactionAsync(); await ExperienceAccess.LockUser(db, user);
        var old = await db.Set<RecognitionHistory>().SingleOrDefaultAsync(x => x.UserId == user && x.RequestId == request.RequestId);
        if (old is not null) {
            var oldCode = await db.Artifacts.Where(a => a.Id == old.ArtifactId).Select(a => a.QrCode).SingleOrDefaultAsync();
            if (old.VisitId != request.VisitId || oldCode != request.QrCode.Trim()) throw new ExperienceException("RequestId đã được dùng cho lần quét khác.", 409);
            return new(old.Id, old.ArtifactId!.Value, old.VisitId, old.RecognizedAt);
        }
        var artifact = await db.Artifacts.SingleOrDefaultAsync(a => a.QrCode == request.QrCode.Trim() && a.IsActive && a.Museum!.IsActive)
            ?? throw new ExperienceException("QR không hợp lệ hoặc hiện vật không khả dụng.", 404);
        var now = clock.GetUtcNow().UtcDateTime;
        if (request.VisitId is int vid) await progress.RecordQrVisitAsync(user, vid, artifact.Id, now);
        var history = new RecognitionHistory { UserId = user, VisitId = request.VisitId, ArtifactId = artifact.Id, RecognizedAt = now,
            IsSuccess = true, RequestId = request.RequestId };
        db.Add(history); await db.SaveChangesAsync();
        await daily.UnlockAsync(history); await rewards.AwardScanPointsAsync(user, artifact.Id);
        await db.SaveChangesAsync(); await rewards.EvaluateBadgesAsync(user); await db.SaveChangesAsync(); await tx.CommitAsync();
        return new(history.Id, artifact.Id, history.VisitId, now);
    }
    public async Task<IReadOnlyList<RecognitionHistory>> GetHistoryAsync(Guid user, int page, int pageSize)
    {
        var (skip, take) = ExperienceAccess.Page(page, pageSize);
        return await db.Set<RecognitionHistory>().AsNoTracking().Where(x => x.UserId == user).OrderByDescending(x => x.Id).Skip(skip).Take(take).ToListAsync();
    }
}
