# Tìm địa điểm và gợi ý trên bản đồ

Cập nhật 22/10/2026.

- Mặc định màn hình Khám phá bản đồ chọn Mọi địa điểm: tìm toàn cầu qua Photon/OpenStreetMap, không giới hạn museums trong DB, không giới hạn loại địa điểm. Nguồn OSM không đảm bảo mọi địa điểm thực tế đều có dữ liệu.
- Nhập 3–200 ký tự; chờ 700 ms để lấy tối đa 6 gợi ý. Enter tìm lại ngay. Chọn gợi ý để di chuyển bản đồ và đặt marker; xóa ô tìm kiếm bỏ marker. Kết quả cũ không ghi đè từ khóa mới.
- Chỉ đường cho địa điểm ngoài DB mở Google Maps với tọa độ đích. Không gửi địa điểm ngoài DB tới route API yêu cầu museumId và không tạo bản ghi museum giả.
- Chế độ Bảo tàng SMGS giữ tìm kiếm DB, bộ lọc tỉnh/thành, gần tôi, phân trang và route preview OSRM hiện có.

## API

GET /api/places?q=Ben%20Thanh trả wrapper statusCode/message/data. Mỗi phần tử data có name/address/latitude/longitude. Query được encode, tọa độ GeoJSON [lon,lat] được chuẩn hóa. Lỗi input 400, provider quota 429, network/JSON 502, timeout 504. Hủy request được truyền tới provider.

Photon:BaseUrl (environment Photon__BaseUrl) mặc định https://photon.komoot.io/. HttpClient timeout 10s, response tối đa 1 MiB, không log URL bằng HttpClient. Rate limit 2 request/giây trên mỗi instance, không queue. Từ khóa gửi tới Photon; không ghi địa điểm tìm kiếm vào DB. Không có migration mới.

Demo Photon chỉ phù hợp tải thử nghiệm hợp lý, không cam kết uptime; triển khai tải lớn cần provider hoặc Photon tự host và shared limiter. Không dùng public Nominatim cho autocomplete.

Tài liệu provider: https://github.com/komoot/photon và https://github.com/komoot/photon/blob/master/docs/api-v1.md

## Xác minh

- BE DiscoveryChecks: input, query encoding, tọa độ, empty, malformed provider, quota; bộ test discovery/routing hiện có pass.
- Flutter: place_search_test + discovery_test: 11 pass, analyzer không lỗi.
- Gọi thực tế /api/places?q=Ben%20Thanh nhận 200 và có Chợ Bến Thành ngoài DB.
- APK build cho Android emulator: API_BASE_URL=http://10.0.2.2:5169. Điện thoại thật cần build theo IP LAN.
