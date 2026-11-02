# Chạy local khi Windows không kết nối TLS được với OSRM

22/10/2026: tái hiện HTTP 502 từ route API. Windows/.NET và curl Schannel lỗi TLS HandshakeFailure với router.project-osrm.org và routing.openstreetmap.de. Node TLS xác minh chứng chỉ bình thường, cùng request nhận 200. Không sửa chứng chỉ, không vô hiệu hóa TLS, không tăng timeout để che lỗi.

Từ thư mục BackEnd chạy:

```powershell
.\run-local.ps1
```

Script khởi động Node bridge trên 127.0.0.1:5168 và BE profile lan trên 5169; Ctrl+C dừng cả hai. Cần Node đã cài. Dừng BE đang chạy trước khi chạy script, tránh trùng port/khóa DLL. Chỉ script này đặt Osrm__BaseUrl=http://127.0.0.1:5168/ cho BE con; appsettings và cấu hình production không đổi. Lệnh dotnet run riêng vẫn dùng provider mặc định và có thể gặp lại lỗi TLS Windows.

Bridge chỉ nhận GET /route/v1/driving/{lon,lat;lon,lat}, validate tọa độ, cố định provider HTTPS https://routing.openstreetmap.de/routed-car/, cố định query GeoJSON. Chỉ bind loopback, không phải open proxy. Timeout 8s bên bridge, 10s bên BE, 15s FE; tối đa 8 MiB và 1 request/giây. Không lưu/log tọa độ. BE bảo toàn HTTP 504 từ upstream. Không tự retry hay sinh đường giả.

Giới hạn: giải pháp tương thích local cho Windows, phụ thuộc dịch vụ demo OSRM; production cần provider có SLA hoặc self-host và TLS hoạt động. Chính sách: https://routing.openstreetmap.de/about.html (tối đa 1 request/giây, không tải nặng).

Kiểm thử: node --test scripts/osrm-local-bridge.test.mjs (2 pass); DiscoveryChecks pass gồm HTTP 504. Live route qua BE với origin mẫu 10.7769,106.7009: museumId=1 HTTP 200, 1103m, 47 điểm, ~2.9s; museumId=2 HTTP 200, 873.3m, 48 điểm, ~2.2s. Đây là tọa độ test công khai, không phải vị trí người dùng.
