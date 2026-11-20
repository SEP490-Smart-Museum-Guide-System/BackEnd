using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMGS.Application.DTOs.Experience;
using SMGS.Application.Interfaces;
using SMGS.Domain.Constants;
using SMGS.Infrastructure.Services;

namespace SMGS.API.Controllers;

[Route("api/museums/{museumId:int}/presence"), Authorize(Roles = RoleNames.Visitor)]
public sealed class MuseumPresenceController(MuseumPresenceService presence) : ExperienceControllerBase
{
    [HttpPost("gps-assessment")]
    public async Task<IActionResult> Gps(int museumId, GpsAssessmentRequest request) => Result(await presence.AssessGps(museumId, request));
    [HttpGet]
    public async Task<IActionResult> Current(int museumId, int? visitId, int? mapId) => Result(await presence.Current(museumId, UserId, visitId, mapId));
    [HttpPost("checkpoints/scan")]
    public async Task<IActionResult> Scan(int museumId, CheckpointScanRequest request) => Result(await presence.Scan(museumId, UserId, request));
    [HttpPost("route")]
    public async Task<IActionResult> Route(int museumId, PresenceRouteRequest request) => Result(await presence.Route(museumId, UserId, request));
}

[Route("api/museums/{museumId:int}/presence/checkpoints"), Authorize(Roles = RoleNames.Admin + "," + RoleNames.MuseumStaff)]
public sealed class MuseumCheckpointController(MuseumPresenceService presence, IQrCodeService qr) : ExperienceControllerBase
{
    [HttpGet("{mapId:int}/{markerId:guid}/qr")]
    public async Task<IActionResult> Qr(int museumId, int mapId, Guid markerId)
    {
        var text = await presence.QrText(museumId, mapId, markerId, UserId, Admin);
        Response.Headers.CacheControl = "private, no-store";
        return File(qr.GeneratePng(text), "image/png", $"location-{mapId}-{markerId:N}.png");
    }
}

[Route("api/museum-presence/checkpoints"), Authorize(Roles = RoleNames.Visitor)]
public sealed class ResolveMuseumCheckpointController(MuseumPresenceService presence) : ExperienceControllerBase
{
    [HttpPost("scan")]
    public async Task<IActionResult> Scan(CheckpointScanRequest request) => Result(await presence.ScanAny(UserId, request));
}
