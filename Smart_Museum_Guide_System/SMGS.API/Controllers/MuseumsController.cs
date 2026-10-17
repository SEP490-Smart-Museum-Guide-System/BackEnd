using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Museum.Request;
using SMGS.Application.DTOs.Museum.Response;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MuseumsController(IMuseumService museumService) : ControllerBase
{
    /// <summary>
    /// Lấy danh sách bảo tàng (phân trang, tìm kiếm).
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(ResponseDto<PaginatedResponseDto<MuseumResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? keyword,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var response = await museumService.GetAllAsync(keyword, isActive, page, pageSize);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Lấy chi tiết một bảo tàng.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ResponseDto<MuseumResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseDto<MuseumResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var response = await museumService.GetByIdAsync(id);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Tạo bảo tàng mới (chỉ Admin).
    /// </summary>
    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost]
    [ProducesResponseType(typeof(ResponseDto<MuseumResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResponseDto<MuseumResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateMuseumRequestDto dto)
    {
        var response = await museumService.CreateAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Cập nhật bảo tàng (chỉ Admin).
    /// </summary>
    [Authorize(Roles = RoleNames.Admin)]
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ResponseDto<MuseumResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseDto<MuseumResponseDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ResponseDto<MuseumResponseDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMuseumRequestDto dto)
    {
        var response = await museumService.UpdateAsync(id, dto);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Vô hiệu hóa bảo tàng — soft delete (chỉ Admin).
    /// </summary>
    [Authorize(Roles = RoleNames.Admin)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ResponseDto<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ResponseDto<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var response = await museumService.DeleteAsync(id);
        return StatusCode(response.StatusCode, response);
    }
}
