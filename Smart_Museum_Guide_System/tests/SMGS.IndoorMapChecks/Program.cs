using Microsoft.AspNetCore.Authorization;
using SkiaSharp;
using SMGS.API.Controllers;
using SMGS.Application.DTOs.IndoorMap;
using SMGS.Application.Services;
using SMGS.Domain.Constants;
using SMGS.Domain.Entities;
using SMGS.Infrastructure.Services;

if (args.Contains("--inspect")) { await SchemaInspection.Run(); return; }

var a = new MapNode { Id = Guid.NewGuid(), Label = "A", X = 0, Y = 0 };
var b = new MapNode { Id = Guid.NewGuid(), Label = "B", X = 1, Y = 0 };
var c = new MapNode { Id = Guid.NewGuid(), Label = "C", X = 1, Y = 1 };
var graph = new MapDraft { Nodes = [a, b, c], Edges = [new() { Id = Guid.NewGuid(), FromNodeId = a.Id, ToNodeId = b.Id, Bidirectional = false }, new() { Id = Guid.NewGuid(), FromNodeId = b.Id, ToNodeId = c.Id, IsAccessible = false }] };
var route = IndoorGraph.Route(graph, 1000, 200, a.Id, c.Id, false);
Check(route.NodeIds.SequenceEqual(new[] { a.Id, b.Id, c.Id }) && route.Cost == 1200, "Dijkstra must use aspect-correct image distances");
Reject(() => IndoorGraph.Route(graph, 1000, 200, c.Id, a.Id, false), "one-way route");
Reject(() => IndoorGraph.Route(graph, 1000, 200, a.Id, c.Id, true), "accessible-only route");
graph.Edges.Add(new() { Id = Guid.NewGuid(), FromNodeId = b.Id, ToNodeId = a.Id });
Reject(() => IndoorGraph.Validate(graph), "overlapping directed edge"); graph.Edges.RemoveAt(2);
a.X = double.NaN; Reject(() => IndoorGraph.Validate(graph), "non-finite coordinate"); a.X = 0;
graph.Markers.Add(new() { Id = Guid.NewGuid(), Label = "QR", Type = "qr" });
Reject(() => IndoorGraph.Validate(graph), "standalone QR without node"); graph.Markers[0].NodeId = a.Id;
Reject(() => IndoorGraph.Validate(graph), "standalone QR even with node");
graph.Markers[0].Type = "artifact"; graph.Markers[0].ArtifactId = 1;
IndoorGraph.Validate(graph, true);
graph.Markers[0].NodeId = null;
Reject(() => IndoorGraph.Validate(graph, true), "artifact needs navigation node even when not marked destination");
graph.Markers[0].NodeId = a.Id;
graph.Nodes.Add(new() { Id = Guid.NewGuid(), Label = "Isolated" });
Check(IndoorGraph.Warnings(graph).Any(w => w.Contains("tách rời")), "disconnected warning");
graph.Markers[0].Type = "artifact"; graph.Markers[0].IsDestination = true;
graph.Markers[0].ArtifactId = null;
Reject(() => IndoorGraph.Validate(graph, true), "unlinked artifact publish");
Reject(() => IndoorImageStore.Decode("<svg/>"u8.ToArray(), ".png"), "fake image");
using (var bitmap = new SKBitmap(20, 10))
using (var image = SKImage.FromBitmap(bitmap))
{
    foreach (var format in new[] { SKEncodedImageFormat.Png, SKEncodedImageFormat.Jpeg })
    {
        using var encoded = image.Encode(format, 90);
        var bytes = encoded.ToArray();
        var ext = format == SKEncodedImageFormat.Png ? ".png" : ".jpg";
        var decoded = IndoorImageStore.Decode(bytes, ext);
        Check(decoded.Width == 20 && decoded.Height == 10, "image dimensions");
        Reject(() => IndoorImageStore.Decode(bytes, ".gif"), "mismatched extension");
        Reject(() => IndoorImageStore.Decode(bytes[..(bytes.Length / 2)], ext), "truncated image");
    }
}
var auth = (AuthorizeAttribute)Attribute.GetCustomAttribute(typeof(IndoorMapsController), typeof(AuthorizeAttribute))!;
Check(auth.Roles!.Contains(RoleNames.MuseumStaff), "staff controller policy");
Check(typeof(IndoorMapsController).GetMethod("Assign")!.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles == RoleNames.Admin, "assignment admin policy");
Console.WriteLine("PASS: graph validation, directed/accessibility routing, aspect ratio, warnings, real image decode and authorization policies.");
if (args.Contains("--database")) await DatabaseChecks.RunAsync();

static void Check(bool valid, string message) { if (!valid) throw new Exception(message); }
static void Reject(Action action, string message)
{
    try { action(); } catch (IndoorMapException) { return; }
    throw new Exception("Expected rejection: " + message);
}
