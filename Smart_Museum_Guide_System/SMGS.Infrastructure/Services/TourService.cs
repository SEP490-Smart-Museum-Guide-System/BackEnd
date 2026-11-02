using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.Interfaces;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Services;

public class TourService(ApplicationDbContext db) : ITourService
{
    private async Task Validate(int museumId, TourRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Length > 200 || r.Description is null || r.Description.Length > 4000 ||
            r.EstimatedMinutes is < 1 or > 1440 || r.ArtifactIds is null || r.ArtifactIds.Length is < 1 or > 100 || r.ArtifactIds.Distinct().Count() != r.ArtifactIds.Length)
            throw new ExperienceException("Tên, thời lượng hoặc danh sách điểm dừng không hợp lệ.");
        if (await db.Artifacts.CountAsync(a => r.ArtifactIds.Contains(a.Id) && a.MuseumId == museumId && a.IsActive) != r.ArtifactIds.Length)
            throw new ExperienceException("Các hiện vật phải đang hoạt động và thuộc cùng bảo tàng.");
    }
    public async Task<Tour> CreateAsync(int museumId, TourRequest request, Guid user, bool admin)
    {
        await ExperienceAccess.Museum(db, museumId, user, admin); await Validate(museumId, request);
        var tour = new Tour { MuseumId = museumId, CreatedBy = user, Title = request.Title.Trim(), Description = request.Description.Trim(), EstimatedMinutes = request.EstimatedMinutes,
            Items = request.ArtifactIds.Select((id, i) => new TourItem { ArtifactId = id, OrderNo = i + 1 }).ToList() };
        db.Add(tour); await db.SaveChangesAsync(); return tour;
    }
    public async Task<Tour> UpdateStopsAsync(int id, UpdateTourRequest request, Guid user, bool admin)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var tour = await db.Set<Tour>().FromSqlInterpolated($"SELECT * FROM tours WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync()
            ?? throw new ExperienceException("Không tìm thấy tour.", 404);
        await ExperienceAccess.Museum(db, tour.MuseumId, user, admin);
        if (tour.Revision != request.Revision) throw new ExperienceException("Tour đã thay đổi, hãy tải lại.", 409);
        if (await db.Visits.AnyAsync(v => v.TourId == id)) throw new ExperienceException("Tour đã có lượt tham quan; tạo tour mới để giữ nguyên lịch sử.", 409);
        await Validate(tour.MuseumId, request.Tour);
        await db.Entry(tour).Collection(x => x.Items).LoadAsync();
        db.RemoveRange(tour.Items); await db.SaveChangesAsync();
        tour.Items = request.Tour.ArtifactIds.Select((a, i) => new TourItem { ArtifactId = a, OrderNo = i + 1 }).ToList();
        tour.Title = request.Tour.Title.Trim(); tour.Description = request.Tour.Description.Trim(); tour.EstimatedMinutes = request.Tour.EstimatedMinutes;
        tour.Status = "draft"; tour.Revision++;
        await db.SaveChangesAsync(); await tx.CommitAsync(); return tour;
    }
    public async Task<Tour> PublishAsync(int id, int revision, Guid user, bool admin)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var tour = await db.Set<Tour>().FromSqlInterpolated($"SELECT * FROM tours WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync()
            ?? throw new ExperienceException("Không tìm thấy tour.", 404);
        await ExperienceAccess.Museum(db, tour.MuseumId, user, admin);
        if (tour.Revision != revision) throw new ExperienceException("Tour đã thay đổi, hãy tải lại.", 409);
        await db.Entry(tour).Collection(x => x.Items).LoadAsync();
        await Validate(tour.MuseumId, new(tour.Title, tour.Description, tour.EstimatedMinutes, tour.Items.Select(x => x.ArtifactId).ToArray()));
        tour.Status = "published"; tour.Revision++; await db.SaveChangesAsync(); await tx.CommitAsync(); return tour;
    }
    public async Task<Tour> GetAsync(int id, Guid? user, bool admin)
    {
        var tour = await db.Set<Tour>().AsNoTracking().Include(x => x.Items.OrderBy(i => i.OrderNo)).SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new ExperienceException("Không tìm thấy tour.", 404);
        if (!await db.Museums.AnyAsync(x => x.Id == tour.MuseumId && x.IsActive)) throw new ExperienceException("Không tìm thấy tour.", 404);
        if (tour.Status != "published") {
            if (user is null) throw new ExperienceException("Không tìm thấy tour.", 404);
            await ExperienceAccess.Museum(db, tour.MuseumId, user.Value, admin);
        }
        return tour;
    }
    public async Task<IReadOnlyList<Tour>> ListAsync(int museumId, bool drafts, Guid? user, bool admin, int page, int pageSize)
    {
        if (drafts) await ExperienceAccess.Museum(db, museumId, user ?? throw new ExperienceException("Vui lòng đăng nhập.", 401), admin);
        if (!await db.Museums.AnyAsync(x => x.Id == museumId && x.IsActive)) throw new ExperienceException("Không tìm thấy bảo tàng.", 404);
        var (skip, take) = ExperienceAccess.Page(page, pageSize);
        return await db.Set<Tour>().AsNoTracking().Where(x => x.MuseumId == museumId && (drafts || x.Status == "published"))
            .OrderBy(x => x.Id).Skip(skip).Take(take).Include(x => x.Items.OrderBy(i => i.OrderNo)).ToListAsync();
    }
}
