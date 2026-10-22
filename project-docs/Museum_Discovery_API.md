# Museum Discovery & Outdoor Navigation — OSM/OSRM

Cập nhật 21/10/2026. Hợp đồng đang dùng bởi `FrontEnd/smgs_mobile`. Không cần Google API key, Google Maps SDK hay Google Cloud Billing.

## Discovery

`GET /api/museums?keyword=Test&cityCode=79&latitude=10.7626&longitude=106.6824&sortBy=distance&page=1&pageSize=20`

- `keyword` tìm trong tên/địa chỉ; `search` là alias khi không truyền keyword.
- `cityCode` là chuỗi mã hai chữ số, lấy từ `GET /api/museums/cities`. Không gửi `HCM`.
- Latitude/longitude phải cùng có hoặc cùng thiếu, hữu hạn, trong [-90,90]/[-180,180]. `sortBy=distance` bắt buộc có cặp tọa độ; `newest` hoặc bỏ sortBy sắp theo ngày tạo.
- Page từ 1 tới 1.000.000, pageSize 1–50, mặc định 20. Lọc và sắp xếp ở PostgreSQL trước phân trang; khoảng cách bằng Haversine, không gọi OSRM để xếp hạng.
- Visitor/anonymous chỉ nhận museum active. Admin có thể dùng isActive hoặc xem tất cả. GET detail `/api/museums/{id:int}` trả 404 cho museum inactive trừ Admin. Cơ chế anonymous của Museum GET hiện tại được giữ; routing áp dụng cùng mức truy cập, không thay đổi auth nền tảng.
- Tọa độ chưa có vẫn là null; distanceKm null nếu không có origin hoặc museum thiếu tọa độ hợp lệ. Khi sort khoảng cách, các bản ghi này nằm cuối; ID là khóa phụ để phân trang ổn định.

```json
{
  "statusCode": 200,
  "message": "Thành công",
  "data": {
    "items": [{
      "id": 7,
      "name": "Bảo tàng mẫu",
      "address": "Địa chỉ mẫu",
      "cityCode": "79",
      "latitude": 10.7769,
      "longitude": 106.7009,
      "distanceKm": 2.3,
      "isActive": true
    }],
    "page": 1,
    "pageSize": 20,
    "totalCount": 1,
    "totalPages": 1
  }
}
```

Ví dụ lược bớt các field Museum cũ (description, openingHours, coverImageUrl, createdAt); chúng vẫn được trả về.

`GET /api/museums/cities` dùng wrapper trên với `data` là mảng `{code,name}`. Catalog BE và seed database dùng cùng nguồn `CityCatalog`. Danh mục mã được đối chiếu với [Báo Điện tử Chính phủ, QĐ 19/2025/QĐ-TTg](https://baochinhphu.vn/bang-danh-muc-va-ma-so-cua-34-tinh-thanh-moi-3321-don-vi-hanh-chinh-cap-xa-moi-102250704153652947.htm). FE không lưu danh mục dự phòng riêng.

POST/PUT Museum nhận thêm cityCode nullable và kiểm tra mã với catalog; tọa độ phải thành cặp. PUT bỏ cityCode/null giữ phân loại đã có để không xóa mã khi caller cũ chưa gửi field mới.

## Route preview

`POST /api/navigation/outdoor/routes` — `Content-Type: application/json`

```json
{
  "museumId": 7,
  "origin": {"latitude": 10.7626, "longitude": 106.6824},
  "travelMode": "DRIVE"
}
```

ID int dương. Origin bắt buộc đủ hai giá trị hợp lệ. Mặc định DRIVE nếu không gửi travelMode; mode khác hoặc null bị từ chối. Tọa độ đích luôn lấy từ museum active trong database.

```json
{
  "statusCode": 200,
  "message": "Thành công",
  "data": {
    "museumId": 7,
    "destination": {"latitude": 10.7769, "longitude": 106.7009},
    "travelMode": "DRIVE",
    "distanceMeters": 3500.5,
    "durationSeconds": 720.25,
    "routePoints": [
      {"latitude": 10.7626, "longitude": 106.6824},
      {"latitude": 10.7769, "longitude": 106.7009}
    ]
  }
}
```

Ví dụ chỉ minh họa định dạng, không phải tuyến đường thật. OSRM GeoJSON trả `[longitude,latitude]`; BE chuẩn hóa thành object lat/lon, FE không giải mã encoded polyline. Khoảng cách/thời gian giữ số thực; Flutter định dạng km và làm tròn phút lên.

Lỗi vẫn dùng `{statusCode,message,data:null}`:

| HTTP | Trường hợp |
| --- | --- |
| 400 | Input/mode không hợp lệ; museum không có cặp tọa độ hợp lệ |
| 404 | Không tồn tại hoặc museum inactive |
| 422 | OSRM NoRoute/NoSegment |
| 429 | Quá giới hạn API hoặc provider quá tải |
| 502 | Network/HTTP provider lỗi, JSON/geometry không hợp lệ |
| 504 | Hết timeout provider |

Client hủy request được truyền qua DB và HttpClient, không chuyển thành lỗi provider. FE hiển thị lỗi và nút thử lại; không sinh tuyến, GPS, km hay phút giả. Google Maps vẫn mở khi GPS bị từ chối hoặc preview thất bại nếu museum có tọa độ.

## Cấu hình và vận hành

- `Osrm:BaseUrl` trong appsettings; environment override `Osrm__BaseUrl`. Mặc định demo `https://router.project-osrm.org/`, timeout 10 giây, response tối đa 8 MiB, không tự retry/redirect. Typed HttpClient tắt URI logging vì URI chứa GPS. Không lưu/cache origin và không log exception chứa URI/payload provider.
- Endpoint route dùng fixed-window limiter 1 request/giây cho cả instance, không queue; API rejection có `Retry-After: 1`. Đây là mức demo, không phải quota từng người dùng. Khi scale nhiều instance, thay bằng shared limiter/gateway và chọn provider/self-host thích hợp.
- [OSRM demo policy](https://github.com/Project-OSRM/osrm-backend/wiki/Demo-server): chỉ dùng hợp lý, phi thương mại, không quá 1 request/giây và không có cam kết uptime. Production không dựa vào demo này. Origin được gửi tới routing provider để tính đường, không được lưu trong SMGS.
- OSRM profile chỉ `driving`, endpoint theo [OSRM Route Service](https://project-osrm.org/docs/v5.9.1/api/). Không bật WALK/BICYCLE/TWO_WHEELER bằng driving.
- Flutter dùng flutter_map 8.3.2 và latlong2. Built-in tile cache giữ HTTP freshness; web dùng browser cache. Tile URL mặc định `https://tile.openstreetmap.org/{z}/{x}/{y}.png`, có thể thay lúc build bằng `--dart-define=OSM_TILE_URL=...` với provider tương thích OSM XYZ và attribution phù hợp.
- Map có định danh User-Agent `com.smgs.museum_guide`, attribution © OpenStreetMap contributors luôn hiển thị kèm link, không bulk/offline download; panBuffer=0 hạn chế tải ô ngoài viewport. Tuân thủ [OSM tile policy](https://operations.osmfoundation.org/policies/tiles/) và chọn dịch vụ tile/self-host phù hợp trước production; public tiles là best-effort.
- Google Maps URL dùng destination + driving + dir_action=navigate; ưu tiên external non-browser app, fallback externalApplication (app hoặc trình duyệt hệ thống), không mở webview trong SMGS. Không cần gọi BE hay có GPS để mở URL.

## Database và triển khai

Ngày 21/10/2026, thư mục migration cũ đã bị xóa và database Development `smgs_db` được xác minh trống (không có bảng trong schema ứng dụng). Đã tạo và áp dụng migration mới `20261021105943_InitialCreate` từ model hiện tại, bao gồm CityCode, bảng cities, Identity và artifact content review.

Kết quả: 15 bảng kể cả migration history, 5 vai trò, 34 tỉnh/thành; không còn migration pending hoặc thay đổi model chưa có migration. Database chưa có tài khoản người dùng. API museums và cities trả HTTP 200 sau khi khởi động lại.

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'
./.local-tools/ef8/dotnet-ef.exe database update --project Smart_Museum_Guide_System/SMGS.Infrastructure --startup-project Smart_Museum_Guide_System/SMGS.API
```

`AddMuseumDiscoveryCities.sql` là script lịch sử cho baseline cũ, không chạy trên database đã áp dụng InitialCreate mới. Với database khác đang có dữ liệu/lịch sử migration cũ, cần đối soát trước khi áp dụng baseline mới; không chạy lại các lệnh tạo bảng.

## Kiểm tra

```powershell
dotnet build Smart_Museum_Guide_System/Smart_Museum_Guide_System.sln
dotnet run --project Smart_Museum_Guide_System/tests/SMGS.DiscoveryChecks
dotnet run --project Smart_Museum_Guide_System/tests/SMGS.DiscoveryChecks -- --database
# Tùy chọn: đúng một request ngoài mạng bằng tọa độ mẫu công khai
dotnet run --project Smart_Museum_Guide_System/tests/SMGS.DiscoveryChecks -- --live-osrm
```

`--database` dùng SMGS_TEST_CONNECTION hoặc config API, chỉ tạo/xóa schema `smgs_discovery_test_<Guid>`, không migrate DB ứng dụng. Chi tiết kết quả và giới hạn kiểm chứng ở `Development_Log.md`.

