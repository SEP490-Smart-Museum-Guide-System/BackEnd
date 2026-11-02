using SMGS.Application.DTOs.IndoorMap;

namespace SMGS.Application.Services;

public static class IndoorGraph
{
    // Legacy qr nodes remain ordinary graph vertices; QR now belongs only to an artifact marker.
    private static readonly string[] NodeTypes = ["corridor", "intersection", "door", "entrance", "stair", "elevator", "qr"];
    private static readonly string[] MarkerTypes = ["artifact", "gallery", "entrance", "exit", "restroom", "information", "stair", "elevator"];

    public static void Validate(MapDraft draft, bool publishing = false)
    {
        if (draft.Nodes is null || draft.Markers is null || draft.Edges is null ||
            draft.Nodes.Count > 2000 || draft.Markers.Count > 2000 || draft.Edges.Count > 6000)
            throw new IndoorMapException("Cấu hình quá lớn hoặc thiếu danh sách điểm/đường.");
        var ids = new HashSet<Guid>();
        void Check(Guid id, string? label, double x, double y)
        {
            if (id == Guid.Empty || !ids.Add(id)) throw new IndoorMapException("Mã điểm bị trùng hoặc không hợp lệ.");
            if (string.IsNullOrWhiteSpace(label) || label.Length > 200) throw new IndoorMapException("Tên điểm phải có từ 1 đến 200 ký tự.");
            if (!double.IsFinite(x) || !double.IsFinite(y) || x < 0 || x > 1 || y < 0 || y > 1)
                throw new IndoorMapException("Điểm phải nằm trong phạm vi sơ đồ.");
        }
        foreach (var n in draft.Nodes)
        {
            if (n is null) throw new IndoorMapException("Điểm dẫn đường không hợp lệ.");
            Check(n.Id, n.Label, n.X, n.Y);
            if (!NodeTypes.Contains(n.Type)) throw new IndoorMapException("Loại điểm dẫn đường không hợp lệ.");
        }
        var nodes = draft.Nodes.ToDictionary(n => n.Id);
        foreach (var m in draft.Markers)
        {
            if (m is null) throw new IndoorMapException("Điểm đánh dấu không hợp lệ.");
            Check(m.Id, m.Label, m.X, m.Y);
            if (m.Type == "qr") throw new IndoorMapException("QR chỉ thuộc hiện vật. Hãy đổi điểm QR riêng sang hiện vật hoặc loại điểm phù hợp trước khi lưu/xuất bản.");
            if (!MarkerTypes.Contains(m.Type) || (m.NodeId.HasValue && !nodes.ContainsKey(m.NodeId.Value)))
                throw new IndoorMapException("Loại điểm hoặc điểm dẫn đường liên kết không hợp lệ.");
            if ((m.ArtifactId.HasValue && m.Type != "artifact") || (m.GalleryId.HasValue && m.Type != "gallery"))
                throw new IndoorMapException("Liên kết hiện vật/phòng không đúng loại điểm.");
            if (publishing && (m.IsDestination || m.Type == "artifact") && m.NodeId is null)
                throw new IndoorMapException("Hiện vật và điểm đến phải liên kết điểm dẫn đường trước khi xuất bản.");
            if (publishing && ((m.Type == "artifact" && m.ArtifactId is null) || (m.Type == "gallery" && m.GalleryId is null)))
                throw new IndoorMapException("Hãy chọn hiện vật/phòng cho điểm đánh dấu trước khi xuất bản.");
        }
        var arcs = new HashSet<(Guid, Guid)>();
        foreach (var e in draft.Edges)
        {
            if (e is null || e.Id == Guid.Empty || !ids.Add(e.Id) || e.FromNodeId == e.ToNodeId ||
                !nodes.ContainsKey(e.FromNodeId) || !nodes.ContainsKey(e.ToNodeId))
                throw new IndoorMapException("Đoạn nối phải có hai điểm khác nhau thuộc sơ đồ.");
            if (!arcs.Add((e.FromNodeId, e.ToNodeId)) || (e.Bidirectional && !arcs.Add((e.ToNodeId, e.FromNodeId))))
                throw new IndoorMapException("Đoạn đường bị trùng hướng di chuyển.");
        }
    }

    public static RouteResult Route(MapDraft draft, int width, int height, Guid from, Guid to, bool accessibleOnly)
    {
        Validate(draft);
        var nodes = draft.Nodes.ToDictionary(n => n.Id);
        if (!nodes.ContainsKey(from) || !nodes.ContainsKey(to)) throw new IndoorMapException("Điểm đầu/cuối không thuộc sơ đồ.");
        var adjacency = nodes.Keys.ToDictionary(id => id, _ => new List<(Guid Id, double Cost)>());
        foreach (var e in draft.Edges.Where(e => !accessibleOnly || e.IsAccessible))
        {
            var a = nodes[e.FromNodeId]; var b = nodes[e.ToNodeId];
            // Pixel distances preserve the image aspect ratio; these are not metres.
            var cost = Math.Sqrt(Math.Pow((a.X - b.X) * width, 2) + Math.Pow((a.Y - b.Y) * height, 2));
            adjacency[a.Id].Add((b.Id, cost));
            if (e.Bidirectional) adjacency[b.Id].Add((a.Id, cost));
        }
        var distances = nodes.Keys.ToDictionary(id => id, _ => double.PositiveInfinity);
        var previous = new Dictionary<Guid, Guid>();
        var queue = new PriorityQueue<Guid, double>();
        distances[from] = 0; queue.Enqueue(from, 0);
        while (queue.TryDequeue(out var current, out var distance))
        {
            if (distance > distances[current]) continue;
            if (current == to) break;
            foreach (var next in adjacency[current])
            {
                var candidate = distance + next.Cost;
                if (candidate >= distances[next.Id]) continue;
                distances[next.Id] = candidate; previous[next.Id] = current;
                queue.Enqueue(next.Id, candidate);
            }
        }
        if (double.IsPositiveInfinity(distances[to])) throw new IndoorMapException("Không tìm được đường đi giữa hai điểm.", 422);
        var path = new List<Guid> { to };
        while (path[^1] != from) path.Add(previous[path[^1]]);
        path.Reverse();
        return new RouteResult(path, distances[to]);
    }

    public static IReadOnlyList<string> Warnings(MapDraft draft)
    {
        if (draft.Nodes.Count == 0) return ["Sơ đồ chưa có mạng lưới dẫn đường."];
        var neighbors = draft.Nodes.ToDictionary(n => n.Id, _ => new List<Guid>());
        foreach (var edge in draft.Edges)
        {
            neighbors[edge.FromNodeId].Add(edge.ToNodeId);
            neighbors[edge.ToNodeId].Add(edge.FromNodeId);
        }
        var seen = new HashSet<Guid>(); var groups = 0;
        foreach (var node in draft.Nodes)
        {
            if (!seen.Add(node.Id)) continue;
            groups++; var queue = new Queue<Guid>(); queue.Enqueue(node.Id);
            while (queue.TryDequeue(out var id))
                foreach (var next in neighbors[id]) if (seen.Add(next)) queue.Enqueue(next);
        }
        var warnings = new List<string>();
        if (groups > 1) warnings.Add($"Có {groups} vùng đường đi tách rời; không thể đi giữa các vùng.");
        if (draft.Edges.Any(e => !e.Bidirectional)) warnings.Add("Có đường một chiều; hãy thử cả hai hướng trước khi xuất bản.");
        return warnings;
    }
}
