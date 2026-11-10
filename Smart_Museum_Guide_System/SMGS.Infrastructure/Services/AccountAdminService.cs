using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Account;
using SMGS.Application.DTOs.Common;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Identity;

namespace SMGS.Infrastructure.Services;

public class AccountAdminService(
    UserManager<ApplicationUser> userManager,
    ApplicationDbContext db) : IAccountAdminService
{
    private static readonly string[] ValidRoles =
    [
        RoleNames.Admin,
        RoleNames.Curator,
        RoleNames.Staff,
        RoleNames.MuseumStaff,
        RoleNames.Visitor
    ];

    private static readonly string[] StaffEligibleRoles =
    [
        RoleNames.Admin,
        RoleNames.Curator,
        RoleNames.Staff,
        RoleNames.MuseumStaff
    ];

    private static List<string>? NormalizeRoles(IEnumerable<string>? roles)
    {
        if (roles is null) return null;
        var result = new List<string>();
        foreach (var raw in roles)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var match = ValidRoles.FirstOrDefault(r => string.Equals(r, raw.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is null) return null;
            if (!result.Contains(match)) result.Add(match);
        }
        return result.Count > 0 ? result : null;
    }

    public async Task<ResponseDto<PaginatedResponseDto<AccountResponseDto>>> GetAccountsAsync(
        string? keyword, string? role, bool? isActive, int? museumId, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLowerInvariant();
            query = query.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(kw)) ||
                u.FullName.ToLower().Contains(kw));
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var normalizedRole = ValidRoles.FirstOrDefault(r => string.Equals(r, role.Trim(), StringComparison.OrdinalIgnoreCase));
            if (normalizedRole is null)
                return ResponseDto<PaginatedResponseDto<AccountResponseDto>>.Fail("Vai trò lọc không hợp lệ.", 400);

            var roleUserIds = from ur in db.UserRoles
                              join r in db.Roles on ur.RoleId equals r.Id
                              where r.Name == normalizedRole
                              select ur.UserId;
            query = query.Where(u => roleUserIds.Contains(u.Id));
        }

        if (museumId.HasValue)
        {
            var assignedUserIds = db.StaffAssignments
                .Where(a => a.MuseumId == museumId.Value)
                .Select(a => a.UserId);
            query = query.Where(u => assignedUserIds.Contains(u.Id));
        }

        var totalCount = await query.CountAsync();
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .ThenBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = await BuildAccountDtosAsync(users);
        return ResponseDto<PaginatedResponseDto<AccountResponseDto>>.Success(new PaginatedResponseDto<AccountResponseDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    public async Task<ResponseDto<AccountResponseDto>> GetAccountByIdAsync(Guid userId)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
            return ResponseDto<AccountResponseDto>.Fail("Không tìm thấy tài khoản.", 404);

        var dtos = await BuildAccountDtosAsync([user]);
        return ResponseDto<AccountResponseDto>.Success(dtos[0]);
    }

    public async Task<ResponseDto<AccountResponseDto>> CreateStaffAsync(CreateStaffAccountRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.Password))
            return ResponseDto<AccountResponseDto>.Fail("Email, họ tên và mật khẩu là bắt buộc.", 400);

        var roles = NormalizeRoles(dto.Roles is { Count: > 0 } ? dto.Roles : [RoleNames.MuseumStaff]);
        if (roles is null)
            return ResponseDto<AccountResponseDto>.Fail("Danh sách vai trò không hợp lệ.", 400);

        var email = dto.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
            return ResponseDto<AccountResponseDto>.Fail("Email đã được sử dụng.", 400);

        await using var tx = await db.Database.BeginTransactionAsync();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = dto.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, dto.Password);
        if (!createResult.Succeeded)
            return ResponseDto<AccountResponseDto>.Fail(string.Join("; ", createResult.Errors.Select(e => e.Description)), 400);

        var roleResult = await userManager.AddToRolesAsync(user, roles);
        if (!roleResult.Succeeded)
            return ResponseDto<AccountResponseDto>.Fail(string.Join("; ", roleResult.Errors.Select(e => e.Description)), 400);

        await tx.CommitAsync();

        var built = await BuildAccountDtosAsync([user]);
        return ResponseDto<AccountResponseDto>.Success(built[0], "Tạo tài khoản nhân viên thành công.", 201);
    }

    public async Task<ResponseDto<AccountResponseDto>> UpdateAccountAsync(Guid userId, UpdateAccountRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FullName))
            return ResponseDto<AccountResponseDto>.Fail("Họ tên không được để trống.", 400);

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return ResponseDto<AccountResponseDto>.Fail("Không tìm thấy tài khoản.", 404);

        await using var tx = await db.Database.BeginTransactionAsync();

        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            var newEmail = dto.Email.Trim();
            if (!string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
            {
                var existing = await userManager.FindByEmailAsync(newEmail);
                if (existing is not null && existing.Id != user.Id)
                    return ResponseDto<AccountResponseDto>.Fail("Email đã được sử dụng bởi tài khoản khác.", 400);

                var emailRes = await userManager.SetEmailAsync(user, newEmail);
                if (!emailRes.Succeeded)
                    return ResponseDto<AccountResponseDto>.Fail(string.Join("; ", emailRes.Errors.Select(e => e.Description)), 400);

                var userNameRes = await userManager.SetUserNameAsync(user, newEmail);
                if (!userNameRes.Succeeded)
                    return ResponseDto<AccountResponseDto>.Fail(string.Join("; ", userNameRes.Errors.Select(e => e.Description)), 400);
            }
        }

        user.FullName = dto.FullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(dto.PhoneNumber) ? null : dto.PhoneNumber.Trim();

        if (!string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            foreach (var validator in userManager.PasswordValidators)
            {
                var valResult = await validator.ValidateAsync(userManager, user, dto.NewPassword);
                if (!valResult.Succeeded)
                    return ResponseDto<AccountResponseDto>.Fail(string.Join("; ", valResult.Errors.Select(e => e.Description)), 400);
            }
            user.PasswordHash = userManager.PasswordHasher.HashPassword(user, dto.NewPassword);
        }

        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
            return ResponseDto<AccountResponseDto>.Fail(string.Join("; ", updateResult.Errors.Select(e => e.Description)), 400);

        await tx.CommitAsync();

        var built = await BuildAccountDtosAsync([user]);
        return ResponseDto<AccountResponseDto>.Success(built[0], "Cập nhật tài khoản thành công.");
    }

    public async Task<ResponseDto<AccountResponseDto>> SetAccountStatusAsync(Guid userId, bool isActive, Guid actorUserId)
    {
        if (userId == actorUserId && !isActive)
            return ResponseDto<AccountResponseDto>.Fail("Không thể tự vô hiệu hóa tài khoản đang đăng nhập của chính mình.", 400);

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return ResponseDto<AccountResponseDto>.Fail("Không tìm thấy tài khoản.", 404);

        user.IsActive = isActive;
        user.LockoutEnabled = true;
        user.LockoutEnd = isActive ? null : DateTimeOffset.MaxValue;
        if (isActive) user.AccessFailedCount = 0;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return ResponseDto<AccountResponseDto>.Fail(string.Join("; ", result.Errors.Select(e => e.Description)), 400);

        var built = await BuildAccountDtosAsync([user]);
        return ResponseDto<AccountResponseDto>.Success(
            built[0],
            isActive ? "Đã kích hoạt tài khoản." : "Đã vô hiệu hóa tài khoản.");
    }

    public async Task<ResponseDto<AccountResponseDto>> AssignRolesAsync(Guid userId, AssignRolesRequestDto dto, Guid actorUserId)
    {
        var normalizedRoles = NormalizeRoles(dto.Roles);
        if (normalizedRoles is null)
            return ResponseDto<AccountResponseDto>.Fail("Danh sách vai trò không hợp lệ hoặc bị trống.", 400);

        if (userId == actorUserId && !normalizedRoles.Contains(RoleNames.Admin))
            return ResponseDto<AccountResponseDto>.Fail("Không thể tự gỡ vai trò Admin của chính mình.", 400);

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return ResponseDto<AccountResponseDto>.Fail("Không tìm thấy tài khoản.", 404);

        await using var tx = await db.Database.BeginTransactionAsync();
        var currentRoles = await userManager.GetRolesAsync(user);

        var toRemove = currentRoles.Except(normalizedRoles, StringComparer.OrdinalIgnoreCase).ToArray();
        if (toRemove.Length > 0)
        {
            var removeRes = await userManager.RemoveFromRolesAsync(user, toRemove);
            if (!removeRes.Succeeded)
                return ResponseDto<AccountResponseDto>.Fail(string.Join("; ", removeRes.Errors.Select(e => e.Description)), 400);
        }

        var toAdd = normalizedRoles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToArray();
        if (toAdd.Length > 0)
        {
            var addRes = await userManager.AddToRolesAsync(user, toAdd);
            if (!addRes.Succeeded)
                return ResponseDto<AccountResponseDto>.Fail(string.Join("; ", addRes.Errors.Select(e => e.Description)), 400);
        }

        await tx.CommitAsync();

        var built = await BuildAccountDtosAsync([user]);
        return ResponseDto<AccountResponseDto>.Success(built[0], "Cập nhật vai trò tài khoản thành công.");
    }

    public async Task<ResponseDto<List<ScopeAssignmentResponseDto>>> GetAssignmentsAsync(
        Guid? userId, int? museumId, int? galleryId, int? artifactId)
    {
        var query = from a in db.StaffAssignments.AsNoTracking()
                    join u in db.Users.AsNoTracking() on a.UserId equals u.Id
                    join m in db.Museums.AsNoTracking() on a.MuseumId equals m.Id
                    join g in db.Galleries.AsNoTracking() on a.GalleryId equals g.Id into gj
                    from g in gj.DefaultIfEmpty()
                    join ar in db.Artifacts.AsNoTracking() on a.ArtifactId equals ar.Id into arj
                    from ar in arj.DefaultIfEmpty()
                    select new
                    {
                        a.Id,
                        a.UserId,
                        UserEmail = u.Email ?? string.Empty,
                        UserFullName = u.FullName,
                        a.MuseumId,
                        MuseumName = m.Name,
                        a.GalleryId,
                        GalleryName = g != null ? g.Name : null,
                        a.ArtifactId,
                        ArtifactName = ar != null ? ar.Name : null,
                        a.AssignedAt
                    };

        if (userId.HasValue) query = query.Where(x => x.UserId == userId.Value);
        if (museumId.HasValue) query = query.Where(x => x.MuseumId == museumId.Value);
        if (galleryId.HasValue) query = query.Where(x => x.GalleryId == galleryId.Value);
        if (artifactId.HasValue) query = query.Where(x => x.ArtifactId == artifactId.Value);

        var rows = await query.OrderByDescending(x => x.AssignedAt).ThenByDescending(x => x.Id).ToListAsync();
        var userIds = rows.Select(x => x.UserId).Distinct().ToArray();
        var rolePairs = await (from ur in db.UserRoles.AsNoTracking()
                               join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                               where userIds.Contains(ur.UserId) && r.Name != null
                               select new { ur.UserId, RoleName = r.Name! }).ToListAsync();
        var rolesByUser = rolePairs.ToLookup(x => x.UserId, x => x.RoleName);
        var items = rows.Select(x => new ScopeAssignmentResponseDto
        {
            Id = x.Id,
            UserId = x.UserId,
            UserEmail = x.UserEmail,
            UserFullName = x.UserFullName,
            UserRoles = rolesByUser[x.UserId].OrderBy(r => r).ToList(),
            MuseumId = x.MuseumId,
            MuseumName = x.MuseumName,
            GalleryId = x.GalleryId,
            GalleryName = x.GalleryName,
            ArtifactId = x.ArtifactId,
            ArtifactName = x.ArtifactName,
            ScopeLevel = x.ArtifactId.HasValue ? "artifact" : x.GalleryId.HasValue ? "gallery" : "museum",
            AssignedAt = x.AssignedAt
        }).ToList();

        return ResponseDto<List<ScopeAssignmentResponseDto>>.Success(items);
    }

    public async Task<ResponseDto<ScopeAssignmentResponseDto>> AssignScopeAsync(Guid userId, ScopeAssignmentRequestDto dto)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return ResponseDto<ScopeAssignmentResponseDto>.Fail("Không tìm thấy tài khoản.", 404);

        var userRoles = await userManager.GetRolesAsync(user);
        if (!userRoles.Any(r => StaffEligibleRoles.Contains(r)))
            return ResponseDto<ScopeAssignmentResponseDto>.Fail("Tài khoản phải có vai trò nhân viên (Museum Staff, Staff, Curator hoặc Admin) để được phân công.", 400);

        var museum = await db.Museums.AsNoTracking().FirstOrDefaultAsync(m => m.Id == dto.MuseumId);
        if (museum is null)
            return ResponseDto<ScopeAssignmentResponseDto>.Fail("Không tìm thấy bảo tàng.", 404);

        int? galleryId = dto.GalleryId;
        string? galleryName = null;
        string? artifactName = null;

        if (dto.ArtifactId.HasValue)
        {
            var artifact = await db.Artifacts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == dto.ArtifactId.Value);
            if (artifact is null)
                return ResponseDto<ScopeAssignmentResponseDto>.Fail("Không tìm thấy hiện vật.", 404);
            if (artifact.MuseumId != dto.MuseumId)
                return ResponseDto<ScopeAssignmentResponseDto>.Fail("Hiện vật không thuộc bảo tàng đã chọn.", 400);
            if (galleryId.HasValue && artifact.GalleryId != galleryId.Value)
                return ResponseDto<ScopeAssignmentResponseDto>.Fail("Hiện vật không thuộc phòng trưng bày đã chọn.", 400);

            galleryId ??= artifact.GalleryId;
            artifactName = artifact.Name;
        }

        if (galleryId.HasValue)
        {
            var galleryInfo = await (from g in db.Galleries.AsNoTracking()
                                     join f in db.Floors.AsNoTracking() on g.FloorId equals f.Id
                                     join b in db.Buildings.AsNoTracking() on f.BuildingId equals b.Id
                                     where g.Id == galleryId.Value
                                     select new { g.Id, g.Name, b.MuseumId }).FirstOrDefaultAsync();

            if (galleryInfo is null)
                return ResponseDto<ScopeAssignmentResponseDto>.Fail("Không tìm thấy phòng trưng bày.", 404);
            if (galleryInfo.MuseumId != dto.MuseumId)
                return ResponseDto<ScopeAssignmentResponseDto>.Fail("Phòng trưng bày không thuộc bảo tàng đã chọn.", 400);

            galleryName = galleryInfo.Name;
        }

        var exists = await db.StaffAssignments.AnyAsync(a =>
            a.UserId == userId &&
            a.MuseumId == dto.MuseumId &&
            a.GalleryId == galleryId &&
            a.ArtifactId == dto.ArtifactId);

        if (exists)
            return ResponseDto<ScopeAssignmentResponseDto>.Fail("Phạm vi phân công này đã tồn tại cho nhân viên.", 409);

        var assignment = new StaffAssignment
        {
            UserId = userId,
            MuseumId = dto.MuseumId,
            GalleryId = galleryId,
            ArtifactId = dto.ArtifactId,
            AssignedAt = DateTime.UtcNow
        };

        db.StaffAssignments.Add(assignment);
        await db.SaveChangesAsync();

        return ResponseDto<ScopeAssignmentResponseDto>.Success(new ScopeAssignmentResponseDto
        {
            Id = assignment.Id,
            UserId = user.Id,
            UserEmail = user.Email ?? string.Empty,
            UserFullName = user.FullName,
            UserRoles = userRoles,
            MuseumId = museum.Id,
            MuseumName = museum.Name,
            GalleryId = galleryId,
            GalleryName = galleryName,
            ArtifactId = dto.ArtifactId,
            ArtifactName = artifactName,
            ScopeLevel = dto.ArtifactId.HasValue ? "artifact" : galleryId.HasValue ? "gallery" : "museum",
            AssignedAt = assignment.AssignedAt
        }, "Phân công phạm vi thành công.", 201);
    }

    public async Task<ResponseDto<object>> RevokeScopeAsync(int assignmentId, Guid? userId = null)
    {
        var query = db.StaffAssignments.Where(a => a.Id == assignmentId);
        if (userId.HasValue) query = query.Where(a => a.UserId == userId.Value);

        var assignment = await query.FirstOrDefaultAsync();
        if (assignment is null)
            return ResponseDto<object>.Fail("Không tìm thấy bản ghi phân công.", 404);

        db.StaffAssignments.Remove(assignment);
        await db.SaveChangesAsync();
        return ResponseDto<object>.Success(null!, "Đã thu hồi phân công phạm vi.");
    }

    public async Task<bool> CanManageMuseumAsync(Guid userId, bool isAdmin, int museumId)
    {
        if (isAdmin) return true;
        return await db.StaffAssignments.AnyAsync(a =>
            a.UserId == userId &&
            a.MuseumId == museumId &&
            a.GalleryId == null &&
            a.ArtifactId == null);
    }

    public async Task<bool> CanManageGalleryAsync(Guid userId, bool isAdmin, int galleryId)
    {
        if (isAdmin) return true;
        var museumId = await (from g in db.Galleries.AsNoTracking()
                              join f in db.Floors.AsNoTracking() on g.FloorId equals f.Id
                              join b in db.Buildings.AsNoTracking() on f.BuildingId equals b.Id
                              where g.Id == galleryId
                              select (int?)b.MuseumId).FirstOrDefaultAsync();
        if (museumId is null) return false;

        return await db.StaffAssignments.AnyAsync(a =>
            a.UserId == userId &&
            a.MuseumId == museumId.Value &&
            a.ArtifactId == null &&
            (a.GalleryId == null || a.GalleryId == galleryId));
    }

    public async Task<bool> CanManageArtifactAsync(Guid userId, bool isAdmin, int artifactId)
    {
        if (isAdmin) return true;
        var artifact = await db.Artifacts.AsNoTracking()
            .Where(a => a.Id == artifactId)
            .Select(a => new { a.MuseumId, a.GalleryId })
            .FirstOrDefaultAsync();
        if (artifact is null) return false;

        return await db.StaffAssignments.AnyAsync(a =>
            a.UserId == userId &&
            a.MuseumId == artifact.MuseumId &&
            (
                (a.GalleryId == null && a.ArtifactId == null) ||
                (artifact.GalleryId != null && a.GalleryId == artifact.GalleryId && a.ArtifactId == null) ||
                a.ArtifactId == artifactId
            ));
    }

    private async Task<List<AccountResponseDto>> BuildAccountDtosAsync(IReadOnlyList<ApplicationUser> users)
    {
        if (users.Count == 0) return [];
        var userIds = users.Select(u => u.Id).ToArray();

        var rolePairs = await (from ur in db.UserRoles.AsNoTracking()
                               join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                               where userIds.Contains(ur.UserId) && r.Name != null
                               select new { ur.UserId, RoleName = r.Name! }).ToListAsync();

        var rolesByUser = rolePairs
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => (IList<string>)g.Select(x => x.RoleName).OrderBy(x => x).ToList());

        var assignmentRows = await (from a in db.StaffAssignments.AsNoTracking()
                                    join m in db.Museums.AsNoTracking() on a.MuseumId equals m.Id
                                    join g in db.Galleries.AsNoTracking() on a.GalleryId equals g.Id into gj
                                    from g in gj.DefaultIfEmpty()
                                    join ar in db.Artifacts.AsNoTracking() on a.ArtifactId equals ar.Id into arj
                                    from ar in arj.DefaultIfEmpty()
                                    where userIds.Contains(a.UserId)
                                    orderby a.AssignedAt descending, a.Id descending
                                    select new
                                    {
                                        a.Id,
                                        a.UserId,
                                        a.MuseumId,
                                        MuseumName = m.Name,
                                        a.GalleryId,
                                        GalleryName = g != null ? g.Name : null,
                                        a.ArtifactId,
                                        ArtifactName = ar != null ? ar.Name : null,
                                        a.AssignedAt
                                    }).ToListAsync();

        var userDict = users.ToDictionary(u => u.Id);
        var assignmentsByUser = assignmentRows
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => new ScopeAssignmentResponseDto
                {
                    Id = x.Id,
                    UserId = x.UserId,
                    UserEmail = userDict[x.UserId].Email ?? string.Empty,
                    UserFullName = userDict[x.UserId].FullName,
                    UserRoles = rolesByUser.TryGetValue(x.UserId, out var assignedRoles) ? assignedRoles : [],
                    MuseumId = x.MuseumId,
                    MuseumName = x.MuseumName,
                    GalleryId = x.GalleryId,
                    GalleryName = x.GalleryName,
                    ArtifactId = x.ArtifactId,
                    ArtifactName = x.ArtifactName,
                    ScopeLevel = x.ArtifactId.HasValue ? "artifact" : x.GalleryId.HasValue ? "gallery" : "museum",
                    AssignedAt = x.AssignedAt
                }).ToList());

        var now = DateTimeOffset.UtcNow;
        return users.Select(u => new AccountResponseDto
        {
            Id = u.Id,
            Email = u.Email ?? string.Empty,
            FullName = u.FullName,
            PhoneNumber = u.PhoneNumber,
            IsActive = u.IsActive,
            IsLockedOut = !u.IsActive || (u.LockoutEnd.HasValue && u.LockoutEnd.Value > now),
            CreatedAt = u.CreatedAt,
            Roles = rolesByUser.TryGetValue(u.Id, out var r) ? r : [],
            Assignments = assignmentsByUser.TryGetValue(u.Id, out var a) ? a : []
        }).ToList();
    }
}

