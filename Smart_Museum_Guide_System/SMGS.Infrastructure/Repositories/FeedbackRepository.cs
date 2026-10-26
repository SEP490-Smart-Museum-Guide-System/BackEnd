using Microsoft.EntityFrameworkCore;
using SMGS.Domain.Entities;
using SMGS.Domain.Interfaces;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Repositories;

public class VisitRepository(ApplicationDbContext db) : IVisitRepository
{
    public async Task<Visit?> GetByIdAsync(int id) =>
        await db.Visits.Include(v => v.Feedback).FirstOrDefaultAsync(v => v.Id == id);

    public async Task<Visit?> GetActiveVisitAsync(Guid visitorId, int museumId) =>
        await db.Visits.FirstOrDefaultAsync(v =>
            v.VisitorId == visitorId && v.MuseumId == museumId && v.Status == VisitStatus.InProgress);

    public async Task<(IList<Visit> Items, int TotalCount)> GetPagedAsync(
        Guid? visitorId, int? museumId, string? status, int page, int pageSize)
    {
        var query = db.Visits.Include(v => v.Feedback).AsNoTracking().AsQueryable();

        if (visitorId.HasValue) query = query.Where(v => v.VisitorId == visitorId.Value);
        if (museumId.HasValue) query = query.Where(v => v.MuseumId == museumId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(v => v.Status == status);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(v => v.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Visit> CreateAsync(Visit visit)
    {
        db.Visits.Add(visit);
        await db.SaveChangesAsync();
        return visit;
    }

    public async Task UpdateAsync(Visit visit)
    {
        db.Visits.Update(visit);
        await db.SaveChangesAsync();
    }
}

public class FeedbackRepository(ApplicationDbContext db) : IFeedbackRepository
{
    private IQueryable<VisitFeedback> WithDetails() =>
        db.VisitFeedbacks.Include(f => f.Visit).Include(f => f.StaffResponse);

    public async Task<VisitFeedback?> GetByIdAsync(int id) =>
        await WithDetails().FirstOrDefaultAsync(f => f.Id == id);

    public async Task<VisitFeedback?> GetByVisitIdAsync(int visitId) =>
        await WithDetails().FirstOrDefaultAsync(f => f.VisitId == visitId);

    public Task<(IList<VisitFeedback> Items, int TotalCount)> GetPagedByMuseumAsync(
        int museumId, string? moderationStatus, int page, int pageSize) =>
        PageAsync(WithDetails().Where(f => f.Visit!.MuseumId == museumId), moderationStatus, page, pageSize);

    public Task<(IList<VisitFeedback> Items, int TotalCount)> GetPagedAllAsync(
        string? moderationStatus, int page, int pageSize) =>
        PageAsync(WithDetails(), moderationStatus, page, pageSize);

    private static async Task<(IList<VisitFeedback> Items, int TotalCount)> PageAsync(
        IQueryable<VisitFeedback> query, string? moderationStatus, int page, int pageSize)
    {
        query = query.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(moderationStatus))
            query = query.Where(f => f.ModerationStatus == moderationStatus);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<VisitFeedback> CreateAsync(VisitFeedback feedback)
    {
        db.VisitFeedbacks.Add(feedback);
        await db.SaveChangesAsync();
        return feedback;
    }

    public async Task UpdateAsync(VisitFeedback feedback)
    {
        db.VisitFeedbacks.Update(feedback);
        await db.SaveChangesAsync();
    }

    public async Task<StaffResponse?> GetResponseByFeedbackIdAsync(int feedbackId) =>
        await db.StaffResponses.FirstOrDefaultAsync(r => r.FeedbackId == feedbackId);

    public async Task<StaffResponse> CreateResponseAsync(StaffResponse response)
    {
        db.StaffResponses.Add(response);
        await db.SaveChangesAsync();
        return response;
    }

    public async Task UpdateResponseAsync(StaffResponse response)
    {
        db.StaffResponses.Update(response);
        await db.SaveChangesAsync();
    }
}

public class ComplaintRepository(ApplicationDbContext db) : IComplaintRepository
{
    public async Task<Complaint?> GetByIdAsync(int id) => await db.Complaints.FindAsync(id);

    public async Task<(IList<Complaint> Items, int TotalCount)> GetPagedAsync(
        Guid? visitorId, string? status, Guid? assignedTo, int page, int pageSize)
    {
        var query = db.Complaints.AsNoTracking().AsQueryable();

        if (visitorId.HasValue) query = query.Where(c => c.VisitorId == visitorId.Value);
        if (assignedTo.HasValue) query = query.Where(c => c.AssignedTo == assignedTo.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(c => c.Status == status);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<Complaint> CreateAsync(Complaint complaint)
    {
        db.Complaints.Add(complaint);
        await db.SaveChangesAsync();
        return complaint;
    }

    public async Task UpdateAsync(Complaint complaint)
    {
        db.Complaints.Update(complaint);
        await db.SaveChangesAsync();
    }
}
