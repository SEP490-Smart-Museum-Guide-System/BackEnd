using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMGS.Application.DTOs.Common;
using SMGS.Application.DTOs.Museum.Response;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;

namespace SMGS.API.Controllers;

[ApiController, Route("api/museum-categories")]
public class MuseumCategoriesController(ApplicationDbContext db) : ControllerBase
{
    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> List(CancellationToken token) =>
        Ok(ResponseDto<object>.Success(await db.Set<MuseumCategory>().AsNoTracking()
            .Where(x => x.IsActive).OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
            .Select(x => new MuseumCategorySummary(x.Id, x.Code, x.Name)).ToListAsync(token)));

    [HttpGet("management"), Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Manage(CancellationToken token) =>
        Ok(ResponseDto<object>.Success(await db.Set<MuseumCategory>().AsNoTracking()
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.Code, x.Name, x.Description, x.DisplayOrder, x.IsActive, MuseumCount = x.Assignments.Count })
            .ToListAsync(token)));

    public record CategoryChange(
        [Required, StringLength(120)] string Name,
        [StringLength(1000)] string? Description,
        [Range(0, int.MaxValue)] int DisplayOrder,
        bool IsActive,
        [StringLength(80), RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$")] string? Code
    );

    [HttpPost, Authorize(Roles = RoleNames.Admin)]
    public Task<IActionResult> Create(CategoryChange input, CancellationToken token) =>
        db.WriteMuseumCategoriesAsync<IActionResult>(async () =>
    {
        if (string.IsNullOrWhiteSpace(input.Code)) return Invalid("Nhập mã danh mục.");
        if (await db.Set<MuseumCategory>().AnyAsync(x => x.Code == input.Code, token))
            return Invalid("Mã danh mục đã tồn tại.");
        var category = new MuseumCategory { Code = input.Code };
        db.Add(category);
        return await Save(category, input, token);
    }, token);

    [HttpPut("{id:int}"), Authorize(Roles = RoleNames.Admin)]
    public Task<IActionResult> Update(int id, CategoryChange input, CancellationToken token) =>
        db.WriteMuseumCategoriesAsync<IActionResult>(async () =>
    {
        var category = await db.Set<MuseumCategory>().SingleOrDefaultAsync(x => x.Id == id, token);
        if (category is null) return NotFound(ResponseDto<object>.Fail("Không tìm thấy danh mục.", 404));
        if (category.IsActive && !input.IsActive && await db.Set<MuseumCategoryAssignment>()
            .AnyAsync(x => x.CategoryId == id && x.Museum.IsActive &&
                !x.Museum.Categories.Any(other => other.CategoryId != id && other.Category.IsActive), token))
            return Invalid("Hãy gán danh mục khác cho các bảo tàng đang hoạt động trước khi vô hiệu hóa.");

        return await Save(category, input, token);
    }, token);

    private async Task<IActionResult> Save(MuseumCategory category, CategoryChange input, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return Invalid("Nhập tên danh mục.");
        category.Name = input.Name.Trim();
        category.Description = input.Description?.Trim();
        category.DisplayOrder = input.DisplayOrder;
        category.IsActive = input.IsActive;
        category.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(token);
        return Ok(ResponseDto<object>.Success(new { category.Id }));
    }

    private IActionResult Invalid(string message) => BadRequest(ResponseDto<object>.Fail(message));
}
