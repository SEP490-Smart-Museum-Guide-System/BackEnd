# Chạy backend local ngày 10/11/2026

Backend đang chạy bằng profile `lan`, lắng nghe cổng 5169. PostgreSQL 18 local đã được bật; database là `smgs_db`. Mật khẩu được truyền qua môi trường tiến trình, không ghi vào tài liệu hoặc appsettings.

## Cập nhật schema đã áp dụng

Database local cũ thiếu các thành phần mà code hiện tại truy vấn. Đã bổ sung theo model EF hiện có:

- `AddUserActiveState.sql`: cột `Users.IsActive`, mặc định true theo cấu hình `ApplicationUser`.
- `AddCatalogReadSchema.sql`: cột `museums.IndoorLayout` với giá trị mặc định `unknown`; các bảng `topics`, `artifact_topics`, `artifact_medias`, `artifact_3d_models` cùng khóa/index tương ứng.

Hai script dùng transaction và chỉ bổ sung phần thiếu. Không xóa bản ghi và không chèn dữ liệu bảo tàng, hiện vật hay media mẫu. Startup Development của BE vẫn gọi seeder tài khoản theo logic sẵn có. Không ghi giả bản migration vào `__EFMigrationsHistory`.

Schema trước khi cập nhật được lưu tại `D:/SEP409/tmp/smgs-schema-before-user-active-20261110.sql`. Đây là bản sao schema, không phải bản sao lưu toàn bộ dữ liệu.

## Kết quả kiểm tra

| Kiểm tra | Kết quả |
| --- | --- |
| Swagger `/swagger/v1/swagger.json` | HTTP 200 |
| GET `/api/museums` | HTTP 200 |
| GET `/api/museums/cities` | HTTP 200 |
| GET `/api/artifacts` | HTTP 200 |
| POST `/api/auth/login`, tài khoản Visitor seed | HTTP 200; không xuất JWT ra log kiểm tra |

Backend được giữ chạy nền. Log local: `D:/SEP409/tmp/smgs-backend-run.out.log` và `D:/SEP409/tmp/smgs-backend-run.err.log`.

## Giới hạn

Cập nhật 11/11: đã bổ sung các bảng tiến độ/tour cần cho hành trình cá nhân và bảng tour phiên bản sau pull `107e825`; có kiểm thử BE→AI→DB trên schema cô lập. Chi tiết và giới hạn mới xem [hành trình cá nhân và đồng bộ BE](PERSONALIZED_TOURS_2026_11_11.md). Các giới hạn dưới đây ghi lại thời điểm kiểm tra ban đầu, không phải kết luận mới cho toàn bộ tour.

- Đây là cập nhật tối thiểu cho startup, đăng nhập và đọc danh mục, chưa phải đồng bộ toàn bộ database theo tất cả phân hệ BE. Snapshot bảng trước cập nhật còn thiếu các bảng tour, bản đồ và commerce mà code hiện tại có thể cần; chưa xác nhận các luồng đó chạy end-to-end.
- Lần kiểm tra kết nối Android cuối cùng, ADB không tìm thấy `emulator-5554`. Chưa xác nhận app mobile đăng nhập trực tiếp sau khi BE được bật. Android emulator dùng API `http://10.0.2.2:5169`; máy Windows dùng `http://localhost:5169`.
- Không lưu mật khẩu kết nối vào source. Khi khởi động lại BE, cần cung cấp cấu hình local đúng qua User Secrets hoặc biến môi trường.
