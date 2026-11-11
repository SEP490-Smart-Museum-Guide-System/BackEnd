# Hành trình cá nhân tùy chọn và đồng bộ BE — 11/11/2026

## Phạm vi đã làm

Visitor → Hành trình có ba lựa chọn: **tour có sẵn**, **tham quan tự do**, **gợi ý hành trình cá nhân**. Khách có thể bỏ qua cá nhân hóa hoàn toàn, kể cả khi đã mua tour ngoài hệ thống. Phần này không tự mua vé, không tạo đơn hàng/quyền premium và không xác minh tour mua ngoài.

Luồng mới: chọn bảo tàng/sở thích/thời gian → BE lọc dữ liệu thật → dịch vụ gợi ý xếp điểm → xem trước lý do/thời lượng/phòng → lưu riêng hoặc bắt đầu → tiến độ qua các lượt quét QR thật. Chỉ thao tác lưu/bắt đầu mới lưu kế hoạch; xem gợi ý không mở lượt tham quan.

## Đồng bộ commit mới

- BE `dev` đã fast-forward từ `a7a9632` lên **`107e825` — add Personalize tour**. Không tạo merge commit, không rewrite lịch sử, không push.
- Đã hợp nhất xung đột trong `ExperienceDtos.cs`, `VisitDtos.cs`, `VisitService.cs`, `Visit.cs`, `VisitProgressService.cs`.
- Giữ nguyên `TourVersion`, `SavedRoute`, trạng thái kiểm duyệt và logic cố định phiên bản của commit nhóm. `TourVersionId`/`TourVersionNo` vẫn có trong response/tiến độ.
- `SavedRoute` của nhóm là tour đã xuất bản được khách lưu theo một phiên bản. `PersonalizedTour` là danh sách điểm riêng được ghép từ hiện vật hợp lệ. Hai loại không thay thế nhau; một visit không nhận đồng thời `personalizedTourId` và `tourId`/`savedRouteId`.
- Bản code trước pull còn trong stash **`codex personal tour preserved before 107e825`**; hash và patch ở `D:/SEP409/tmp/20261111-personal-tour/`. Đã apply thành công và giữ stash làm dự phòng; không apply lại nếu không cần. Code tích hợp hiện ở working tree, chưa commit.

## Thành phần và dữ liệu

| Thành phần | Thay đổi |
| --- | --- |
| `AI-System/AI Tour` | API nội bộ FastAPI; bộ gợi ý TF-IDF/chủ đề tiếng Việt, ưu tiên thời gian và phòng/tầng; không tải model/GPU |
| BE | `IPersonalizedTourService`, model adapter HttpClient, controller Visitor, bảng `personalized_tours`, `Visit.PersonalizedTourId`, tiến độ theo snapshot |
| Flutter | Biểu mẫu tùy chọn, xem trước, lý do gợi ý, danh sách đã lưu, lưu/bắt đầu; giữ mục tour có sẵn/tự do |
| Launcher | AI stack thêm dịch vụ thứ 5 cổng 8004; launcher BE riêng có thể nối tour mà không cần bật chatbot |

Thuật toán **không dùng LLM/ONNX**. TF-IDF/cosine kết hợp chủ đề và nhóm từ như gốm sứ/đồ đồng; lựa chọn dựa trên nội dung, không sinh lịch sử mới. Không gọi đây là mô hình đã được huấn luyện trên dữ liệu bảo tàng.

BE chỉ gửi hiện vật đang hoạt động, thuộc bảo tàng đã chọn, có nội dung tiếng Việt hiện hành `published` và khách có quyền đọc. Nội dung premium không có quyền bị loại. BE kiểm tra ID/phiên bản, trùng điểm, tổng thời gian và quyền trước khi lưu/bắt đầu; không tin danh sách ID do client tự gửi.

Mỗi Visitor có một bản xem trước trong RAM, hết hạn sau 15 phút hoặc bị thay khi tạo gợi ý mới. Hành trình đã lưu nằm trong PostgreSQL, riêng chủ tài khoản; gợi ý tiếp theo không đổi thứ tự điểm đã lưu. Khởi động lại BE làm mất bản xem trước chưa lưu. Triển khai nhiều replica cần thay cache RAM bằng cache chung trước khi dùng cân bằng tải.

Nội dung hiển thị theo bản xuất bản hiện hành, còn thứ tự điểm theo snapshot. Nội dung/quyền thay đổi có thể khiến việc bắt đầu bị chặn và yêu cầu tạo lại. Tiến độ cá nhân dùng `VisitItem`/QR hiện có; không tự đánh dấu đã đến từ client.

## Hợp đồng API Visitor

| Method | Path | Chức năng |
| --- | --- | --- |
| GET | `/api/personalized-tours/interests?museumId=1` | Chủ đề thật của bảo tàng |
| POST | `/api/personalized-tours/recommendations` | `{museumId, availableMinutes, interests, maxStops?}`; xem trước |
| POST | `/api/personalized-tours` | `{recommendationId}`; lưu idempotent |
| GET | `/api/personalized-tours/me?page=1&pageSize=20` | Hành trình riêng đã lưu |
| GET | `/api/personalized-tours/{guid}` | Chỉ chủ sở hữu được đọc |
| POST | `/api/visits` | `{museumId, personalizedTourId}` để bắt đầu; không kèm loại hành trình khác |

Đăng nhập Visitor/JWT theo hệ thống hiện có. Gợi ý giới hạn 10 yêu cầu/phút/tài khoản, tách quota chatbot. Service key chỉ ở BE/AI, không gửi về Flutter.

## Database và seed local

- DB cũ ban đầu thiếu nhiều bảng hiện hành. Đã áp dụng additive SQL `AddUserActiveState.sql`, `AddCatalogReadSchema.sql`, `AddPersonalizedTourSupport.sql`, sau khi sao lưu. Không xóa dữ liệu hoặc giả lịch sử EF.
- Sau pull, thêm bảng `tour_versions`, `tour_version_items`, `saved_routes`, cột nullable và FK/index qua **`AddTourVersionCompatibility.sql`**. Sao lưu trước bước này: `D:/SEP409/tmp/smgs-before-tour-version-20261111.dump`.
- DB local có demo seed `SeedChatbotArtifactsData.sql`: 1 bảo tàng, 8 hiện vật và nội dung demo. Sửa seed liên kết `artifact_topics` đã khai báo nhưng trước đó chưa được gắn. Đây là dữ liệu demo có cờ xuất bản, không phải xác nhận thẩm định lịch sử thực tế.
- DB mới: dùng chuỗi EF migration hiện có, gồm `20261110094047_Min` của nhóm và **`20261111034943_AddOptionalPersonalizedItineraries`** mới. Snapshot EF đã cập nhật.
- **Không chạy migration `Min` nguyên khối lên DB cũ đang có bảng**: nó là migration tạo toàn bộ schema. Các SQL tương thích local không tự ghi `__EFMigrationsHistory`; khi chuẩn hóa DB dùng chung cần đối chiếu baseline trước, không chạy chồng hai cách thiết lập.
- Mật khẩu DB/khóa không ghi vào source; các bản dump nằm ngoài repo trong `tmp`.

## Chạy trên máy thành viên

Repo đặt cạnh nhau: `BackEnd`, `FrontEnd`, `AI-System`. PostgreSQL/BE cấu hình qua User Secrets hoặc `ConnectionStrings__DefaultConnection` trên từng máy.

```powershell
# Terminal 1, từ AI-System: chạy riêng tour (Python 3.12+)
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -r '.\AI Tour\requirements.txt'
powershell -ExecutionPolicy Bypass -File .\tools\start-local-tour.ps1

# Terminal 2, từ BackEnd/Smart_Museum_Guide_System
powershell -ExecutionPolicy Bypass -File .\tools\start-api-with-personalized-tours.ps1

# Terminal 3, từ FrontEnd/smgs_web
npm run dev -- --host 127.0.0.1 --port 5173
```

Mở `http://127.0.0.1:5173/visitor/`, đăng nhập Visitor rồi chọn **Hành trình**. Có thể chạy toàn bộ AI bằng launcher Docker hiện có `AI-System/tools/start-ai.ps1`, bao gồm tour cổng 8004. Bản Flutter web đã đóng gói trong `smgs_web/public/visitor`; sửa Flutter cần chạy `FrontEnd/tools/update-visitor-web.ps1` trước khi kiểm tra/deploy.

## Kiểm chứng sau khi hợp nhất

| Kiểm tra | Kết quả |
| --- | --- |
| Build BE net8 | Thành công, 0 warning/error |
| AI Tour unittest | 9/9 đạt |
| BE personalized HTTP → AI → PostgreSQL | 22 kiểm tra đạt trên schema tạm riêng, gồm quyền/nội dung, lưu riêng, thay sở thích, QR/hoàn thành |
| Bộ `SMGS.TourChecks` của commit vừa pull | Unit/phân quyền và kiểm thử PostgreSQL `TC-014..TC-026` đạt |
| Flutter: personalized/connected experience/main flows | 24/24 đạt; gồm không tự lưu/mở visit và tour có sẵn giữ payload cũ |
| Flutter release web | Build thành công và cập nhật bundle `/visitor/` |
| Kiểm tra browser mobile 412×915 | Đăng nhập → Hành trình → chọn Gốm sứ/30 phút → nhận 2 điểm thật; nền/chip đã sửa và đọc được |
| Launcher AI | Đạt kiểm tra cấu hình/khóa; chưa xác minh chạy cả stack Docker |
| Compose và EF | Compose config hợp lệ với khóa kiểm tra tạm; EF xác nhận model không có thay đổi chưa nằm trong migration |

Log kiểm tra local: `D:/SEP409/tmp/personal-tour-integration-tests.log`, `versioned-tour-checks.log`, `personal-tour-flutter-tests.log`. Bộ DB dùng schema tạm có tiền tố riêng, dọn sau khi kiểm thử; không ghi lượt quét giả vào dữ liệu ứng dụng.

Ảnh kiểm tra: `D:/SEP409/tmp/personal-tour-option-mobile.jpg`, `D:/SEP409/tmp/personal-tour-result-mobile.jpg`. Trình duyệt đã mở bản đóng gói mới; hành trình đã lưu trước pull vẫn đọc lại được. Ở ca kiểm tra UI sau pull chỉ tạo bản xem trước, không bắt đầu chuyến đi hoặc tự mua vé.

Lệnh chạy lại các bộ kiểm thử (từ đúng thư mục repo/phân hệ):

```powershell
# AI-System/AI Tour
..\.venv\Scripts\python.exe -m unittest discover -s tests -v

# BackEnd/Smart_Museum_Guide_System, sau khi đặt SMGS_TEST_CONNECTION đúng DB local
$env:AI_TOUR_SERVICE_KEY = [IO.File]::ReadAllText('../../AI-System/state/tour-service-key.txt').Trim()
dotnet run --project tests/SMGS.PersonalizedTourChecks
dotnet run --project tests/SMGS.TourChecks -- --database

# FrontEnd/smgs_mobile
flutter test --no-pub test/personalized_tour_test.dart test/connected_experience_test.dart test/main_flows_test.dart
```

Smoke trên demo: sở thích **Gốm sứ**, 30 phút → 2 điểm, 11,5 phút; **Đồ đồng**, 30 phút → 4 điểm, 21,5 phút. Không khẳng định độ chính xác hay hiệu năng production từ bộ seed nhỏ này.

## Giới hạn còn lại

Thời lượng/di chuyển là ước tính theo độ dài nội dung và phòng/tầng, chưa đo khoảng cách thực tế hay tối ưu tuyến theo bản đồ. Điều hướng cần dữ liệu bản đồ đã cấu hình; không tự sinh phòng/node. Tối đa 500 ứng viên mỗi lần; chưa benchmark tải AWS. Chưa triển khai production hoặc kiểm thử với tour bên ngoài thật. Flutter còn hai thông báo analyzer có sẵn ở `sketchfab_viewer_web.dart` (`dart:html` và thư viện chỉ dành cho web).
