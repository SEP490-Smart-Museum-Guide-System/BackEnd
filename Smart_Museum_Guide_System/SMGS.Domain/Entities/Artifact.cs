namespace SMGS.Domain.Entities;

public class Artifact
{
    public int Id { get; set; }

    /// <summary>Bảo tàng chứa hiện vật (bắt buộc).</summary>
    public int MuseumId { get; set; }

    /// <summary>Phòng trưng bày (không bắt buộc theo tài liệu Q3).</summary>
    public int? GalleryId { get; set; }

    /// <summary>Mã nội bộ do museum staff đặt.</summary>
    public string? Code { get; set; }

    /// <summary>Tên gốc/trung tính của hiện vật.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Mã QR duy nhất để nhận diện hiện vật (khác QR kiểm vé).</summary>
    public string? QrCode { get; set; }

    /// <summary>Node bản đồ nội bộ liên kết với hiện vật.</summary>
    public int? MapNodeId { get; set; }

    /// <summary>Toạ độ X trên mặt bằng tầng.</summary>
    public float? MapX { get; set; }

    /// <summary>Toạ độ Y trên mặt bằng tầng.</summary>
    public float? MapY { get; set; }

    /// <summary>Icon hiển thị trên bản đồ.</summary>
    public string? MapIcon { get; set; }

    /// <summary>Cấp độ truy cập: free hoặc premium.</summary>
    public string AccessLevel { get; set; } = "free";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Museum? Museum { get; set; }
    public Gallery? Gallery { get; set; }
}
