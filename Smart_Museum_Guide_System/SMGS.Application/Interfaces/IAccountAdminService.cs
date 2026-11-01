using SMGS.Application.DTOs.Account;
using SMGS.Application.DTOs.Common;

namespace SMGS.Application.Interfaces;

public interface IAccountAdminService
{
    Task<ResponseDto<PaginatedResponseDto<AccountResponseDto>>> GetAccountsAsync(
        string? keyword, string? role, bool? isActive, int? museumId, int page, int pageSize);

    Task<ResponseDto<AccountResponseDto>> GetAccountByIdAsync(Guid userId);

    Task<ResponseDto<AccountResponseDto>> CreateStaffAsync(CreateStaffAccountRequestDto dto);

    Task<ResponseDto<AccountResponseDto>> UpdateAccountAsync(Guid userId, UpdateAccountRequestDto dto);

    Task<ResponseDto<AccountResponseDto>> SetAccountStatusAsync(Guid userId, bool isActive, Guid actorUserId);

    Task<ResponseDto<AccountResponseDto>> AssignRolesAsync(Guid userId, AssignRolesRequestDto dto, Guid actorUserId);

    Task<ResponseDto<List<ScopeAssignmentResponseDto>>> GetAssignmentsAsync(
        Guid? userId, int? museumId, int? galleryId, int? artifactId);

    Task<ResponseDto<ScopeAssignmentResponseDto>> AssignScopeAsync(Guid userId, ScopeAssignmentRequestDto dto);

    Task<ResponseDto<object>> RevokeScopeAsync(int assignmentId, Guid? userId = null);

    Task<bool> CanManageMuseumAsync(Guid userId, bool isAdmin, int museumId);
    Task<bool> CanManageGalleryAsync(Guid userId, bool isAdmin, int galleryId);
    Task<bool> CanManageArtifactAsync(Guid userId, bool isAdmin, int artifactId);
}

