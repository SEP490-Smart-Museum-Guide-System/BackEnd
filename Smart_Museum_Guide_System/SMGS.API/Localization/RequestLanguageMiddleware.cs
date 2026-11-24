using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SMGS.Infrastructure.Data;

namespace SMGS.API.Localization;

/// <summary>Selects the UI language without changing CurrentCulture, which controls date and number formatting.</summary>
public sealed class RequestLanguageMiddleware(RequestDelegate next)
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
    {
        var requested = Normalize(context.Request.Query["lang"].FirstOrDefault())
            ?? Normalize(context.Request.Headers["Accept-Language"].FirstOrDefault()?.Split(',', ';')[0].Trim());

        if (requested is null && Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            requested = Normalize(await db.Users.AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => user.PreferredLanguage)
                .FirstOrDefaultAsync(context.RequestAborted));
        }

        var uiCulture = requested == "en" ? English : Vietnamese;
        CultureInfo.CurrentUICulture = uiCulture;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.TryAdd("Content-Language", uiCulture.Name);
            return Task.CompletedTask;
        });

        await next(context);
    }

    private static string? Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return null;
        var code = language.Trim().Split('-', '_')[0];
        return code.Equals("en", StringComparison.OrdinalIgnoreCase) ? "en"
            : code.Equals("vi", StringComparison.OrdinalIgnoreCase) ? "vi"
            : null;
    }
}
