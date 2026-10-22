using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.OpenApi.Models;
using SMGS.Application.DTOs.Common;
using SMGS.Infrastructure;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = _ => new BadRequestObjectResult(
        ResponseDto<object>.Fail("Dữ liệu yêu cầu không hợp lệ.", 400));
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Smart Museum Guide API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Nhập access token nhận được từ API login."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    });
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>((options, settings) =>
        options.TokenValidationParameters = settings.Value.CreateValidationParameters());
builder.Services.AddAuthorization();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));
builder.Services.AddRateLimiter(options =>
{
    // shortcut: instance-wide demo quota; use a shared limiter when running multiple instances.
    options.AddFixedWindowLimiter("place-search", limiter =>
    {
        limiter.PermitLimit = 2;
        limiter.Window = TimeSpan.FromSeconds(1);
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("outdoor-routing", limiter =>
    {
        // shortcut: one API instance/demo server, use a shared limiter when scaling out.
        limiter.PermitLimit = 1;
        limiter.Window = TimeSpan.FromSeconds(1);
        limiter.QueueLimit = 0;
    });
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        context.HttpContext.Response.Headers.RetryAfter = "1";
        await context.HttpContext.Response.WriteAsJsonAsync(ResponseDto<object>.Fail("Vui lòng thử lại sau.", 429), token);
    };
});

var app = builder.Build();
if (app.Environment.IsDevelopment()) await DataSeeder.EnsureRolesAsync(app.Services);
if (args.Contains("--create-user"))
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("--create-user is only available in Development.");
    await DataSeeder.CreateUserAsync(app.Services, app.Configuration);
    Console.WriteLine("Development account created successfully.");
    return;
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
