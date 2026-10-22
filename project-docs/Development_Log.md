# Development log

## 21/10/2026 — Hoàn thiện đăng ký / đăng nhập FE và BE

- Thống nhất response cả hai API có roles thật cùng token, expiration và profile. Register luôn tạo Visitor trong transaction; giữ password nguyên vẹn.
- Flutter dùng service thật, kiểm tra response đầy đủ, từ chối role nghiệp vụ, lưu một session có hạn dùng, xóa session legacy/corrupt và tự logout khi hết hạn. Form kiểm tra xác nhận mật khẩu và hiển thị lỗi BE; quên mật khẩu báo chưa hỗ trợ.
- React bỏ fallback tài khoản/mật khẩu mẫu, boolean mock login và đổi role bằng state. Chỉ chấp nhận bốn role nghiệp vụ rõ ràng; Visitor có link ứng dụng/đăng ký riêng. Profile restore từ session thật, không lấy user mẫu theo email.
- Thêm CORS allow-list và Vite proxy dev/preview. Đã restart BE workspace tại localhost:5169; preflight thực trả 204 với đúng allowed origin.
- DB local thiếu Curator; Development startup đã bổ sung role thiếu bằng cùng deterministic seed definitions của EF, không đổi tài khoản/role đã có. Kiểm tra role initializer lặp lại không tạo trùng. Không tạo hoặc apply migration auth.
- Đã build/sync Visitor Flutter web vào React public/visitor, so hash main.dart.js và rebuild React dist. React preview tại 127.0.0.1:4173 trả 200 cho portal/visitor, proxy `/api/auth/login` trả 400 đúng với body trống.

Kết quả:

- SMGS.AuthChecks unit: PASS.
- SMGS.AuthChecks --database: PASS trên DB local với tài khoản ngẫu nhiên tự xóa; kiểm tra 5 role/Visitor/JWT/rollback.
- SMGS.AuthChecks --http: PASS với server thật và schema PostgreSQL riêng; registration/login/duplicate/password spaces/role escalation input/CORS allow và deny. Child process và schema được dọn sau test.
- Flutter service live test: PASS đăng ký, duplicate, sai mật khẩu, login đúng, restore/logout qua API thật trong schema fixture; không tạo tài khoản thử tồn tại lâu trên DB ứng dụng.
- React live client: PASS Admin login, từ chối Visitor, sai mật khẩu, restore/logout với API thật trong fixture.
- Flutter auth unit/widget: 5/5 PASS; discovery 8/8 PASS; analyzer no issues. Hai test auth cũ đã cập nhật và PASS.
- Full Flutter: 37 PASS, 3 lỗi baseline ngoài auth, 1 live test mặc định skip. Không sửa các module tìm kiếm/hiện vật/chuyển cảnh ngoài task chỉ để làm xanh suite.
- React unit tests: 3/3 PASS (live test tùy chọn đã chạy riêng và PASS); build PASS, lint không có error nhưng còn warning cũ ngoài phần auth.
- Flutter web và APK debug: build PASS. Chi tiết contract/cấu hình ở `Auth_API.md`, FE `docs/AUTH_INTEGRATION.md`.

## 21/10/2026 — Museum Discovery / OpenStreetMap / OSRM

Triển khai sau khi người dùng xác nhận kế hoạch FE/BE. Đọc project context, DBML và source thực tế; kế hoạch FE cũ dựa Google Routes không khớp source BE hiện tại.

### Thay đổi

- Discovery GET thêm city/search alias/GPS/distance sort và validation, lọc active cho Visitor; detail inactive chỉ Admin xem. Giữ wrapper và keyword cũ.
- Bổ sung CityCode nullable, bảng cities và migration seed 34 mã tỉnh/thành; không backfill hoặc apply migration lên DB ứng dụng. PUT cũ bỏ cityCode không xóa mã đã gán.
- Outdoor route chỉ DRIVE, đích từ DB, HttpClientFactory/cancellation/timeout/giới hạn response, xử lý NoRoute/NoSegment/provider lỗi và chuẩn hóa GeoJSON lat/lon.
- Route endpoint giới hạn 1 request/giây/instance cho demo. Tắt typed client URI logging; không lưu/cache GPS. Không đổi authentication nền tảng hoặc module indoor.
- Flutter dùng OSM TileLayer/MarkerLayer/PolylineLayer; list/filter/pagination từ API, GPS chỉ lấy theo thao tác, không fallback vị trí hay đường giả. Preview sheet có bản đồ thật, camera fit, metrics, error/retry và Google Maps mở ngoài độc lập GPS.
- API doc và SQL incremental ở `Museum_Discovery_API.md`, `AddMuseumDiscoveryCities.sql`; kế hoạch chi tiết ở `OSM_OSRM_Implementation_Plan.md`.

### Kết quả xác minh

- `dotnet build Smart_Museum_Guide_System/Smart_Museum_Guide_System.sln --no-restore`: PASS. Clean rebuild còn warning cũ CS8981 về tên migration kj; không thay migration cũ.
- `SMGS.DiscoveryChecks`: PASS cho validation, active scope, mode, DTO, lỗi OSRM, đổi lon/lat, JSON hỏng, network/timeout, cancellation và tương thích cập nhật cityCode.
- `SMGS.DiscoveryChecks --database`: PASS trên PostgreSQL với schema tạm được xóa trong finally; test migration mới bảo toàn museum cũ, 34 mã tỉnh, Haversine thực trong query PostgreSQL, sort trước pagination, tie-break, null last, dateline, antipodal và cancel. Không sửa schema/data ứng dụng.
- `dotnet ef migrations has-pending-model-changes`: không có thay đổi model chưa được migration phản ánh.
- `SMGS.AuthChecks`: PASS.
- `SMGS.ContentChecks`: không compile do fake cũ thiếu `IArtifactContentRepository.GetAllPublishedByArtifactIdAsync(int)` và `IArtifactRepository.GetByQrCodeAsync(string)`. Các interface và file test này không bị thay đổi bởi task.
- Flutter analyzer: không có issue. 8 test mới discovery PASS, gồm màn hình 320px cỡ chữ 1.3, GPS denied, filter/pagination kết hợp, response race, polyline/camera fit và external browser fallback.
- Flutter full suite: 30 PASS, 5 FAIL. Đối chứng chạy trên bản HEAD gốc (archive trong thư mục tạm, không thay checkout) cho 22 PASS và đúng 5 FAIL đó. Tên lỗi ghi trong FE `docs/DEVELOPMENT_LOG.md`.
- Flutter web build và Android debug APK: PASS.
- Live OSRM demo smoke bằng tọa độ công khai: chưa xác minh được do lỗi thiết lập TLS trong môi trường; PowerShell HTTPS request độc lập cũng lỗi SSL. Không bỏ kiểm tra certificate. Mock HTTP và DB integration vẫn PASS.

### Việc còn thuộc môi trường triển khai

- Read-only migration list của DB theo config hiện tại báo tất cả migration Pending. Cần đối soát schema/history trước khi apply vì repository có hai migration tạo bảng trùng. Đã chuẩn bị riêng migration/SQL bổ sung; chưa thực hiện database update.
- Gán cityCode đã xác minh cho museum cũ; không tự suy từ địa chỉ. Dữ liệu chưa gán vẫn xuất hiện trong danh sách chung và map nếu có tọa độ.
- Chưa smoke test GPS thật và chuyển Google Maps trên điện thoại. Repository chưa có iOS target.
- Demo OSRM/public OSM tile không có bảo đảm production; dùng provider/self-host phù hợp, giới hạn phân tán khi scale và attribution tương ứng.

## 21/10/2026 — Chạy migration database Development

- Xác minh cấu hình Development + User Secrets cùng trỏ tới smgs_db đang trống; thư mục migration cũ đã bị xóa trước thao tác này.
- Tạo và chạy 20261021105943_InitialCreate từ model hiện tại. Không drop database/bảng hoặc xóa dữ liệu.
- Xác minh 15 bảng (gồm history), 5 roles, 34 cities, 0 users; không còn pending migration/model changes.
- Khởi động lại API localhost:5169; GET museums và cities trả 200. Chưa tạo tài khoản quản trị hoặc dữ liệu bảo tàng mẫu.

## 22/10/2026 — Tìm địa điểm ngoài DB
Thêm GET /api/places proxy Photon, validate/timeout/rate limit; kiểm thử provider và live HTTP 200. Xem Place_Search.md. Không thay schema DB.

## 22/10/2026 — Sửa lỗi route OSRM trên Windows
Tái hiện TLS HandshakeFailure của Schannel; thêm cầu nối Node HTTPS giới hạn loopback cho local và run-local.ps1 quản lý cùng BE. Hai bảo tàng mẫu trả route thật 200; 2 bridge tests và DiscoveryChecks pass. Xem Local_Routing.md.
