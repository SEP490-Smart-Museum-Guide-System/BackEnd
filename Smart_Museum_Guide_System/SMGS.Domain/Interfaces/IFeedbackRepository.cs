using SMGS.Domain.Entities;

namespace SMGS.Domain.Interfaces;

public interface IVisitRepository
{
    Task<Visit?> GetByIdAsync(int id);

    /// <summary>Lấy lượt tham quan đang InProgress của khách tại bảo tàng cụ thể.</summary>
    Task<Visit?> GetActiveVisitAsync(Guid visitorId, int museumId);

    Task<(IList<Visit> Items, int TotalCount)> GetPagedAsync(
        Guid? visitorId, int? museumId, string? status, int page, int pageSize);

    Task<Visit> CreateAsync(Visit visit);
    Task UpdateAsync(Visit visit);
}

public interface IFeedbackRepository
{
    Task<VisitFeedback?> GetByIdAsync(int id);
    Task<VisitFeedback?> GetByVisitIdAsync(int visitId);

    /// <summary>Danh sách feedback của một bảo tàng (Museum Staff xem).</summary>
    Task<(IList<VisitFeedback> Items, int TotalCount)> GetPagedByMuseumAsync(
        int museumId, string? moderationStatus, int page, int pageSize);

    /// <summary>Danh sách toàn bộ feedback (System Staff / Admin xem).</summary>
    Task<(IList<VisitFeedback> Items, int TotalCount)> GetPagedAllAsync(
        string? moderationStatus, int page, int pageSize);

    Task<VisitFeedback> CreateAsync(VisitFeedback feedback);
    Task UpdateAsync(VisitFeedback feedback);

    // StaffResponse
    Task<StaffResponse?> GetResponseByFeedbackIdAsync(int feedbackId);
    Task<StaffResponse> CreateResponseAsync(StaffResponse response);
    Task UpdateResponseAsync(StaffResponse response);
}

public interface IComplaintRepository
{
    Task<Complaint?> GetByIdAsync(int id);

    Task<(IList<Complaint> Items, int TotalCount)> GetPagedAsync(
        Guid? visitorId, string? status, Guid? assignedTo, int page, int pageSize);

    Task<Complaint> CreateAsync(Complaint complaint);
    Task UpdateAsync(Complaint complaint);
}
