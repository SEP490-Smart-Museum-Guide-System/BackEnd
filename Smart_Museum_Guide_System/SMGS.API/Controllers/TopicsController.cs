using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Topic;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;

namespace SMGS.API.Controllers;

[ApiController]
[Route("api")]
[Produces("application/json")]
public class TopicsController(ITopicService topicService) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    private bool IsAdmin => User.IsInRole(RoleNames.Admin);

    [AllowAnonymous]
    [HttpGet("topics")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? museumId,
        [FromQuery] string? keyword,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var response = await topicService.GetAllAsync(museumId, keyword, isActive, page, pageSize);
        return StatusCode(response.StatusCode, response);
    }

    [AllowAnonymous]
    [HttpGet("museums/{museumId:int}/topics")]
    public async Task<IActionResult> GetByMuseum(
        int museumId,
        [FromQuery] string? keyword,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var response = await topicService.GetAllAsync(museumId, keyword, isActive, page, pageSize);
        return StatusCode(response.StatusCode, response);
    }

    [AllowAnonymous]
    [HttpGet("topics/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var response = await topicService.GetByIdAsync(id);
        return StatusCode(response.StatusCode, response);
    }

    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpPost("topics")]
    public async Task<IActionResult> Create([FromBody] CreateTopicRequestDto dto)
    {
        if (UserId == Guid.Empty) return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập.", 401));
        var response = await topicService.CreateAsync(dto, UserId, IsAdmin);
        return StatusCode(response.StatusCode, response);
    }

    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpPost("museums/{museumId:int}/topics")]
    public async Task<IActionResult> CreateForMuseum(int museumId, [FromBody] CreateTopicRequestDto dto)
    {
        if (UserId == Guid.Empty) return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập.", 401));
        dto.MuseumId = museumId;
        var response = await topicService.CreateAsync(dto, UserId, IsAdmin);
        return StatusCode(response.StatusCode, response);
    }

    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpPut("topics/{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTopicRequestDto dto)
    {
        if (UserId == Guid.Empty) return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập.", 401));
        var response = await topicService.UpdateAsync(id, dto, UserId, IsAdmin);
        return StatusCode(response.StatusCode, response);
    }

    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpDelete("topics/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (UserId == Guid.Empty) return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập.", 401));
        var response = await topicService.DeleteAsync(id, UserId, IsAdmin);
        return StatusCode(response.StatusCode, response);
    }

    [AllowAnonymous]
    [HttpGet("artifacts/{artifactId:int}/topics")]
    public async Task<IActionResult> GetArtifactTopics(int artifactId)
    {
        var response = await topicService.GetArtifactTopicsAsync(artifactId);
        return StatusCode(response.StatusCode, response);
    }

    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpPost("artifacts/{artifactId:int}/topics")]
    public async Task<IActionResult> AssignTopics(int artifactId, [FromBody] AssignTopicsRequestDto dto)
    {
        if (UserId == Guid.Empty) return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập.", 401));
        var response = await topicService.AssignTopicsAsync(artifactId, dto.TopicIds, UserId, IsAdmin);
        return StatusCode(response.StatusCode, response);
    }

    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpPut("artifacts/{artifactId:int}/topics")]
    public async Task<IActionResult> SetTopics(int artifactId, [FromBody] AssignTopicsRequestDto dto)
    {
        if (UserId == Guid.Empty) return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập.", 401));
        var response = await topicService.SetArtifactTopicsAsync(artifactId, dto.TopicIds, UserId, IsAdmin);
        return StatusCode(response.StatusCode, response);
    }

    [Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
    [HttpDelete("artifacts/{artifactId:int}/topics/{topicId:int}")]
    public async Task<IActionResult> RemoveTopic(int artifactId, int topicId)
    {
        if (UserId == Guid.Empty) return Unauthorized(ResponseDto<object>.Fail("Vui lòng đăng nhập.", 401));
        var response = await topicService.RemoveTopicFromArtifactAsync(artifactId, topicId, UserId, IsAdmin);
        return StatusCode(response.StatusCode, response);
    }
}

