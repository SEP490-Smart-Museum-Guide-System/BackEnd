# Danh sách API BE, phân quyền và giao diện

Ngày đối chiếu mã nguồn: **22/10/2026**. Tổng cộng: **24 endpoint trong 7 controller**.

## 1. Quy ước role và nền tảng

| Role nghiệp vụ | Giá trị role chính xác trong BE/JWT | Giao diện |
|---|---|---|
| Admin — Quản trị viên | `Admin` | Web |
| System Staff — Nhân viên hệ thống | `Staff` | Web |
| Museum Staff — Nhân viên bảo tàng | `Museum Staff` | Web |
| Curator — Giám tuyển | `Curator` | Web |
| Visitor — Khách tham quan | `Visitor` | Mobile |

- BE không có role mang giá trị `System Staff`; khi kiểm tra token/phân quyền phải dùng `Staff`.
- **Tất cả role** trong tài liệu là năm role ở bảng trên.
- **Công khai** nghĩa là mọi role và người chưa đăng nhập đều được gọi API. Khách chưa đăng nhập không phải một role riêng.
- Cột **role sử dụng/quản lý** mô tả đối tượng làm giao diện. Cột **quyền BE hiện tại** mô tả giới hạn thực sự đang được code thực thi.
- Với API dùng chung cho các role nghiệp vụ và Visitor, phân loại giao diện là **Web + Mobile**. Đây là phân loại để triển khai, không có nghĩa cả hai giao diện đã kết nối API. Cột **Trạng thái kết nối FE** ghi nhận lời gọi từ giao diện theo mã nguồn; **đã kết nối** chưa có nghĩa đã kiểm thử chạy thực tế.
- Các quyền đánh dấu **(*)** được lấy từ thuộc tính `Authorize` đang bị comment trong controller; chưa được BE thực thi.

## 2. Xác thực — AuthController

| # | Method và endpoint | Thao tác được phép | Role sử dụng/quản lý | Giao diện | Quyền BE hiện tại | Trạng thái kết nối FE |
|---|---|---|---|---|---|---|
| 1 | `POST /api/auth/register` | Đăng ký tài khoản khách tham quan | Người chưa đăng nhập; tài khoản tạo ra có role `Visitor` | Mobile | Công khai; không phải API tạo tài khoản nhân viên/Admin | Mobile: đã kết nối; Web: chưa kết nối |
| 2 | `POST /api/auth/login` | Đăng nhập, nhận access token và danh sách role | `Admin`, `Staff`, `Museum Staff`, `Curator`, `Visitor` | Web: bốn role nghiệp vụ; Mobile: `Visitor` | Công khai; xác thực email và mật khẩu | Web + Mobile: đã kết nối |

FE web hiện chỉ nhận tài khoản nghiệp vụ; FE Flutter chỉ nhận `Visitor`. Việc chia nền tảng này nằm ở FE, không phải giới hạn role của endpoint đăng nhập.

## 3. Bảo tàng — MuseumsController

| # | Method và endpoint | Thao tác được phép | Role sử dụng/quản lý | Giao diện | Quyền BE hiện tại | Trạng thái kết nối FE |
|---|---|---|---|---|---|---|
| 3 | `GET /api/museums` | Xem danh sách, tìm kiếm, lọc, phân trang bảo tàng | Tất cả role; `Admin` có thêm quyền xem bảo tàng ngừng hoạt động | Mobile | Công khai; người không có role `Admin` chỉ được xem bảo tàng hoạt động | Web + Mobile: đã kết nối |
| 4 | `GET /api/museums/cities` | Lấy danh mục tỉnh/thành cho bộ lọc hoặc biểu mẫu | Tất cả role | Mobile | Công khai | Web + Mobile: đã kết nối |
| 5 | `GET /api/museums/{id}` | Xem chi tiết một bảo tàng | Tất cả role; `Admin` có thêm quyền xem bảo tàng ngừng hoạt động |Mobile | Công khai; trả 404 với bảo tàng ngừng hoạt động nếu người gọi không phải `Admin` | Web + Mobile: chỉ có service, chưa nối màn hình |
| 6 | `POST /api/museums` | Tạo bảo tàng | `Admin` (*) | Web | Đang công khai; kiểm tra role `Admin` bị comment | Web: đã kết nối;  |
| 7 | `PUT /api/museums/{id}` | Cập nhật thông tin bảo tàng | `Admin` | Web | Bắt buộc token có role `Admin` | Web: có gọi nhưng lỗi lấy token |
| 8 | `DELETE /api/museums/{id}` | Vô hiệu hóa bảo tàng, xóa mềm | `Admin` | Web | Bắt buộc token có role `Admin` | Web: có gọi nhưng lỗi lấy token |

## 4. Hiện vật và QR — ArtifactsController

| # | Method và endpoint | Thao tác được phép | Role sử dụng/quản lý | Giao diện | Quyền BE hiện tại | Trạng thái kết nối FE |
|---|---|---|---|---|---|---|
| 9 | `GET /api/artifacts` | Xem danh sách, tìm kiếm, lọc và phân trang hiện vật | Tất cả role | Web + Mobile | Công khai | Web + Mobile: chưa kết nối |
| 10 | `GET /api/artifacts/{id}` | Xem chi tiết hiện vật | Tất cả role | Web + Mobile | Công khai | Web + Mobile: chưa kết nối |
| 11 | `POST /api/artifacts` | Tạo hiện vật | `Admin`, `Museum Staff` (*) | Web | Đang công khai; kiểm tra hai role bị comment | Web: chưa kết nối |
| 12 | `PUT /api/artifacts/{id}` | Cập nhật hiện vật | `Admin`, `Museum Staff` (*) | Web | Đang công khai; kiểm tra hai role bị comment | Web: chưa kết nối |
| 13 | `DELETE /api/artifacts/{id}` | Vô hiệu hóa hiện vật, xóa mềm | `Admin`, `Museum Staff` (*) | Web | Đang công khai; kiểm tra hai role bị comment | Web : chưa kết nối |
| 14 | `GET /api/artifacts/scan/{qrCode}` | Tra cứu bằng mã QR, lấy hiện vật và nội dung đã xuất bản | Tất cả role; `Visitor` dùng trong luồng quét QR | Web + Mobile | Công khai | Mobile: chưa kết nối |
| 15 | `GET /api/artifacts/{id}/qr-image` | Tải ảnh QR định dạng PNG | Tất cả role; nghiệp vụ in/dán QR phù hợp với `Admin`, `Museum Staff` | Web + Mobile; chức năng in/dán QR trên Web | Công khai; BE chưa giới hạn tải ảnh theo role | Web: chưa kết nối |
| 16 | `POST /api/artifacts/{id}/regenerate-qr` | Tạo lại mã QR cho hiện vật | `Admin`, `Museum Staff` (*) | Web | Đang công khai; kiểm tra hai role bị comment | Web: chưa kết nối |

Không tự suy ra rằng các API đọc công khai chỉ dành cho `Visitor`, hoặc API tải ảnh QR chỉ cho phép nhân viên. Nếu muốn giới hạn như vậy cần thay đổi phân quyền BE.

## 5. Gửi và đọc nội dung hiện vật — ArtifactContentSubmissionsController

| # | Method và endpoint | Thao tác được phép | Role sử dụng/quản lý | Giao diện | Quyền BE hiện tại | Trạng thái kết nối FE |
|---|---|---|---|---|---|---|
| 17 | `POST /api/artifacts/{artifactId}/contents` | Gửi nội dung/kết quả AI thành phiên bản mới chờ duyệt | `Admin`, `Museum Staff` | Web | Bắt buộc token có ít nhất một trong hai role | Web: chưa kết nối |
| 18 | `GET /api/artifacts/{artifactId}/contents/published` | Đọc phiên bản đã xuất bản theo `languageCode`, mặc định `vi` | `Admin`, `Staff`, `Museum Staff`, `Curator`, `Visitor` | Web + Mobile | Bắt buộc đăng nhập; không giới hạn role cụ thể | Web: chưa kết nối |

## 6. Kiểm duyệt nội dung — ArtifactContentsController

| # | Method và endpoint | Thao tác được phép | Role sử dụng/quản lý | Giao diện | Quyền BE hiện tại | Trạng thái kết nối FE |
|---|---|---|---|---|---|---|
| 19 | `GET /api/artifact-contents` | Xem danh sách nội dung, lọc theo bảo tàng/hiện vật/trạng thái, phân trang | `Curator` | Web | Bắt buộc token có role `Curator` | Web: chưa kết nối |
| 20 | `GET /api/artifact-contents/{id}` | Xem chi tiết nội dung cần kiểm duyệt | `Curator` | Web | Bắt buộc token có role `Curator` | Web: chưa kết nối |
| 21 | `POST /api/artifact-contents/{id}/approve` | Phê duyệt và xuất bản nội dung | `Curator` | Web | Bắt buộc token có role `Curator` | Web: chưa kết nối |
| 22 | `POST /api/artifact-contents/{id}/reject` | Từ chối nội dung | `Curator` | Web | Bắt buộc token có role `Curator` | Web: chưa kết nối |

Tài khoản chỉ có role `Admin` không được gọi bốn API kiểm duyệt trên. BE không có cơ chế tự động cho `Admin` kế thừa quyền `Curator`.

## 7. Chỉ đường và địa điểm — NavigationController, PlacesController

| # | Method và endpoint | Thao tác được phép | Role sử dụng/quản lý | Giao diện | Quyền BE hiện tại | Trạng thái kết nối FE |
|---|---|---|---|---|---|---|
| 23 | `POST /api/navigation/outdoor/routes` | Tính tuyến đường ngoài trời đến bảo tàng | Tất cả role; `Visitor` dùng trong luồng đi đến bảo tàng | Web + Mobile | Công khai; có giới hạn tần suất gọi | Mobile: đã kết nối;  |
| 24 | `GET /api/places?q=...` | Tìm địa điểm theo từ khóa | Tất cả role | Web + Mobile | Công khai; có giới hạn tần suất gọi | Mobile: đã kết nối;  |

## 8. Phân công giao diện theo role

| Role | Nền tảng | Nhóm chức năng từ các API hiện có |
|---|---|---|
| `Admin` | Web | Đăng nhập; quản lý bảo tàng; quản lý hiện vật/QR theo quyền dự kiến (*); gửi nội dung; đọc nội dung đã xuất bản; dùng các API công khai |
| `Museum Staff` | Web | Đăng nhập; quản lý hiện vật/QR theo quyền dự kiến (*); gửi nội dung; đọc nội dung đã xuất bản; dùng các API công khai |
| `Curator` | Web | Đăng nhập; danh sách/chi tiết nội dung kiểm duyệt; phê duyệt/từ chối; đọc nội dung đã xuất bản; dùng các API công khai |
| System Staff (`Staff`) | Web | Đăng nhập; đọc nội dung đã xuất bản; dùng các API công khai. Chưa có API nghiệp vụ riêng giới hạn cho `Staff` trong 24 endpoint này |
| `Visitor` | Mobile | Đăng ký/đăng nhập; khám phá bảo tàng/hiện vật; tra cứu QR; đọc nội dung đã xuất bản sau đăng nhập; tìm địa điểm và chỉ đường |

Phân công trên không cấp thêm quyền cho FE. Mọi quyền thay đổi dữ liệu cần được BE thực thi, kể cả khi giao diện đã ẩn nút theo role.

## 9. Tình trạng kết nối FE tại thời điểm kiểm tra

| Trạng thái | API (theo số thứ tự ở trên) | Chi tiết |
|---|---|---|
| Đã có luồng gọi từ giao diện | 1, 2, 3, 4, 6, 23, 24 | Đăng ký: Flutter; đăng nhập/danh sách bảo tàng/tỉnh thành: Web và Flutter; tạo bảo tàng: Web; chỉ đường/tìm địa điểm: Flutter |
| Có gọi nhưng lỗi lấy token trên Web | 7, 8 | Auth lưu `smgs_web_session`, service bảo tàng đọc `smgs_web_jwt_token`, nên luồng đăng nhập hiện tại không cung cấp token cho hai API này |
| Chỉ có hàm service, chưa được màn hình gọi | 5 | Cả Web và Flutter |
| Chưa nối API | 9–22, ngoại trừ các số đã liệt kê ở trên | Tổng 14 endpoint: toàn bộ hiện vật/QR và gửi, đọc, kiểm duyệt nội dung |

## 10. Nguồn đối chiếu và giới hạn

- Role: `Smart_Museum_Guide_System/SMGS.Domain/Constants/RoleNames.cs`.
- Endpoint và quyền truy cập: các file trong `Smart_Museum_Guide_System/SMGS.API/Controllers/`.
- Quyền xem bảo tàng ngừng hoạt động: `MuseumsController.cs` và `SMGS.Application/Services/MuseumService.cs`.
- Role mặc định khi đăng ký: `SMGS.Infrastructure/Repositories/UserRepository.cs`.
- Kết nối FE: `FrontEnd/smgs_web/src/services/`, `FrontEnd/smgs_web/src/context/AppContext.jsx`, `FrontEnd/smgs_mobile/lib/services/` và các màn hình gọi tương ứng (đường dẫn FE tính từ thư mục cha của BackEnd).

Tài liệu dựa trên mã nguồn; chưa kiểm thử chạy thực tế bằng từng role. Năm endpoint đánh dấu (*) hiện vẫn cho người chưa đăng nhập thay đổi dữ liệu do phân quyền bị tắt.
