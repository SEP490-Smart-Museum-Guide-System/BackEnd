using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMGS.Application.DTOs.Artifact.Request;
using SMGS.Application.DTOs.Artifact.Response;
using SMGS.Application.DTOs.Common;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[TypeFilter(typeof(CommerceExceptionFilter))]
public class ArtifactsController(IArtifactService artifactService, ICommerceService commerce) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new SMGS.Application.DTOs.Commerce.CommerceException("Vui lòng đăng nhập.", 401);
    private async Task EnsureManagement(int artifactId)
    {
        var artifact = await artifactService.GetByIdAsync(artifactId);
        if (artifact.Data == null) throw new SMGS.Application.DTOs.Commerce.CommerceException("Không tìm thấy hiện vật.", 404);
        await commerce.EnsureMuseumManagement(artifact.Data.MuseumId, UserId, User.IsInRole(RoleNames.Admin));
    }
    /// <summary>
    /// Lấy danh sách hiện vật (phân trang, tìm kiếm, lọc theo bảo tàng / phòng).
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(ResponseDto<PaginatedResponseDto<ArtifactResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? museumId,
        [FromQuery] int? galleryId,
        [FromQuery] string? keyword,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var response = await artifactService.GetAllAsync(museumId, galleryId, keyword, isActive, page, pageSize);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Lấy chi tiết một hiện vật.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ResponseDto<ArtifactResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseDto<ArtifactResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var response = await artifactService.GetByIdAsync(id);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Tạo hiện vật mới (chỉ Admin hoặc Museum Staff).
    /// QR code sẽ được tự động sinh nếu không truyền.
    /// </summary>
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpPost]
    [ProducesResponseType(typeof(ResponseDto<ArtifactResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseDto<ArtifactResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateArtifactRequestDto dto)
    {
        await commerce.EnsureMuseumManagement(dto.MuseumId, UserId, User.IsInRole(RoleNames.Admin));
        var response = await artifactService.CreateAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Cập nhật hiện vật (chỉ Admin hoặc Museum Staff).
    /// </summary>
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ResponseDto<ArtifactResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseDto<ArtifactResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseDto<ArtifactResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateArtifactRequestDto dto)
    {
        await EnsureManagement(id);
        var response = await artifactService.UpdateAsync(id, dto);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Vô hiệu hóa hiện vật — soft delete (chỉ Admin hoặc Museum Staff).
    /// </summary>
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ResponseDto<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        await EnsureManagement(id);
        var response = await artifactService.DeleteAsync(id);
        return StatusCode(response.StatusCode, response);
    }

    // ────────────────────────────────────────────────────────────
    //  QR CODE ENDPOINTS
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Quét QR code → trả về thông tin chi tiết hiện vật kèm nội dung đã xuất bản (đa ngôn ngữ).
    /// Đây là endpoint mà QR code sẽ trỏ tới khi được quét.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("scan/{qrCode}")]
    [ProducesResponseType(typeof(ResponseDto<ArtifactScanResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseDto<ArtifactScanResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Scan(string qrCode)
    {
        var response = await artifactService.ScanAsync(qrCode,
            Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Tải ảnh QR code (PNG) của hiện vật — dùng để in và dán lên hiện vật.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{id:int}/qr-image")]
    [Produces("image/png")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQrImage(int id)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var response = await artifactService.GetQrCodeImageAsync(id, baseUrl);

        if (response.StatusCode != 200)
            return StatusCode(response.StatusCode, ResponseDto<object>.Fail(response.Message, response.StatusCode));

        return File(response.Data!, "image/png", $"artifact-{id}-qr.png");
    }

    /// <summary>
    /// Tạo lại mã QR mới cho hiện vật (khi mã cũ bị lộ hoặc hỏng).
    /// </summary>
    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpPost("{id:int}/regenerate-qr")]
    [ProducesResponseType(typeof(ResponseDto<ArtifactResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseDto<ArtifactResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegenerateQr(int id)
    {
        await EnsureManagement(id);
        var response = await artifactService.RegenerateQrCodeAsync(id);
        return StatusCode(response.StatusCode, response);
    }
}

