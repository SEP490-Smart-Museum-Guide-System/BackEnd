using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Dashboard;

namespace SMGS.Application.Interfaces;

public interface IDashboardService
{
    Task<ResponseDto<AdminDashboardDto>> GetAdminDashboardAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);

    Task<ResponseDto<SystemStaffDashboardDto>> GetSystemStaffDashboardAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);

    Task<ResponseDto<MuseumDashboardDto>> GetMuseumDashboardAsync(
        int museumId,
        Guid callerId,
        bool isAdmin,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default);
}
