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
public class ArtifactsController(IArtifactService artifactService) : ControllerBase
{
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
    /// </summary>
    //[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.MuseumStaff}")]
    [AllowAnonymous]
    [HttpPost]
    [ProducesResponseType(typeof(ResponseDto<ArtifactResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseDto<ArtifactResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateArtifactRequestDto dto)
    {
        var response = await artifactService.CreateAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Cập nhật hiện vật (chỉ Admin hoặc Museum Staff).
    /// </summary>
    //[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.MuseumStaff}")]
    [AllowAnonymous]
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ResponseDto<ArtifactResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseDto<ArtifactResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseDto<ArtifactResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateArtifactRequestDto dto)
    {
        var response = await artifactService.UpdateAsync(id, dto);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Vô hiệu hóa hiện vật — soft delete (chỉ Admin hoặc Museum Staff).
    /// </summary>
    //[Authorize(Roles = $"{RoleNames.Admin},{RoleNames.MuseumStaff}")]
    [AllowAnonymous]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ResponseDto<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var response = await artifactService.DeleteAsync(id);
        return StatusCode(response.StatusCode, response);
    }
}
