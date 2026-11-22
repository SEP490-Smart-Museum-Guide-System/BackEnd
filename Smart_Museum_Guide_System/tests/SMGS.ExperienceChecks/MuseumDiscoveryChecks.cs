using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SMGS.API.Controllers;
using SMGS.Application.DTOs.Common;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Data;
using SMGS.Infrastructure.Identity;
using SMGS.Infrastructure.Services;

internal static class MuseumDiscoveryChecks
{
    public static async Task Run(ApplicationDbContext db, Func<ApplicationDbContext> open,
        Func<ApplicationDbContext, RecognitionService> recognition)
    {
        var first = new Museum { Name = "First" };
        var second = new Museum { Name = "Second" };
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "discovery-test" };
        db.AddRange(first, second, user);
        await db.SaveChangesAsync();
        var a = new Artifact { MuseumId = first.Id, Name = "A", QrCode = "A" };
        var b = new Artifact { MuseumId = first.Id, Name = "B", QrCode = "B" };
        var c = new Artifact { MuseumId = first.Id, Name = "C" };
        var d = new Artifact { MuseumId = first.Id, Name = "D", QrCode = "D" };
        var premium = new Artifact { MuseumId = first.Id, Name = "Locked", AccessLevel = "premium", QrCode = "locked" };
        var inactive = new Artifact { MuseumId = first.Id, Name = "Hidden", IsActive = false, QrCode = "hidden" };
        var foreign = new Artifact { MuseumId = second.Id, Name = "Foreign", QrCode = "foreign" };
        db.AddRange(a, b, c, d, premium, inactive, foreign);
        await db.SaveChangesAsync();

        static MuseumDiscoveryController Controller(ApplicationDbContext context) => new(context, new CommerceService(context))
            { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var controller = Controller(db);
        async Task<JsonElement> Summary(int id)
        {
            var result = (OkObjectResult)await controller.Get(id, default);
            return JsonSerializer.SerializeToElement(((ResponseDto<object>)result.Value!).Data, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }

        await recognition(db).RecordQrAsync(new("A", null, Guid.NewGuid()), user.Id);
        await recognition(db).RecordQrAsync(new("A", null, Guid.NewGuid(), "image"), user.Id);
        var empty = await Summary(first.Id);
        if (empty.GetProperty("qrArtifactCount").GetInt32() != 3 || empty.GetProperty("topArtifacts").GetArrayLength() != 0)
            throw new Exception("QR eligibility or exclusion of legacy/image recognition failed.");

        var scan = new SMGS.Application.DTOs.Experience.ScanQrRequest("B", null, Guid.NewGuid(), "qr");
        await recognition(db).RecordQrAsync(scan, user.Id);
        await recognition(db).RecordQrAsync(scan, user.Id);
        var request = new MuseumDiscoveryController.DetailViewRequest(Guid.NewGuid());
        await Task.WhenAll(Enumerable.Range(0, 3).Select(async _ => {
            await using var context = open();
            if (await Controller(context).RecordView(b.Id, request, default) is not NoContentResult)
                throw new Exception("Concurrent retry failed.");
        }));
        if (await db.Set<ArtifactDetailView>().CountAsync() != 1 ||
            await db.Set<RecognitionHistory>().CountAsync(h => h.ConfirmedQrScan) != 1)
            throw new Exception("Retry counted twice.");
        if (await controller.RecordView(c.Id, request, default) is not ConflictObjectResult)
            throw new Exception("RequestId reuse across artifacts accepted.");
        foreach (var artifact in new[] { a, c, d, foreign })
            await controller.RecordView(artifact.Id, new(Guid.NewGuid()), default);
        if (await controller.RecordView(inactive.Id, new(Guid.NewGuid()), default) is not NotFoundObjectResult ||
            await controller.RecordView(premium.Id, new(Guid.NewGuid()), default) is not ObjectResult { StatusCode: 403 })
            throw new Exception("Restricted detail views accepted.");
        var ids = (await Summary(first.Id)).GetProperty("topArtifacts").EnumerateArray().Select(x => x.GetProperty("id").GetInt32());
        if (!ids.SequenceEqual(new[] { b.Id }.Concat(new[] { a.Id, c.Id, d.Id }.Order().Take(2))) ||
            (await Summary(second.Id)).GetProperty("topArtifacts")[0].GetProperty("id").GetInt32() != foreign.Id)
            throw new Exception("Ranking, tie-breaker or museum filter failed.");
        Console.WriteLine("Museum discovery PostgreSQL migration, eligibility, ranking and concurrent retry checks passed.");
    }
}
