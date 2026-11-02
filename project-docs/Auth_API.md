# Đăng ký và đăng nhập — hợp đồng FE/BE

Cập nhật 21/10/2026. Flutter Visitor và React nghiệp vụ dùng API thật, không fallback tài khoản mẫu.

## Hai endpoint

`POST /api/auth/register` — tạo tài khoản Visitor:

```json
{"fullName":"Tên của bạn","email":"visitor@example.com","password":"Example123"}
```

`POST /api/auth/login`:

```json
{"email":"visitor@example.com","password":"Example123"}
```

Header `Content-Type: application/json`. Email và tên được trim; mật khẩu giữ nguyên tuyệt đối, kể cả khoảng trắng đầu/cuối. Chính sách Identity hiện tại: tối thiểu 6 ký tự, có chữ thường và số. Đăng ký không nhận role từ client; role luôn là Visitor, được gán trong cùng transaction tạo user. Không cấp tài khoản Staff/Admin qua đăng ký công khai.

Register trả HTTP 201; login trả HTTP 200. Cả hai dùng cùng response:

```json
{
  "statusCode": 200,
  "message": "Đăng nhập thành công.",
  "data": {
    "accessToken": "<JWT>",
    "accessTokenExpiration": "2026-10-21T12:00:00Z",
    "email": "visitor@example.com",
    "fullName": "Tên của bạn",
    "roles": ["Visitor"]
  }
}
```

Expiration chỉ minh họa, server trả thời gian thực. Roles là role đã gán trong Identity. Các role nghiệp vụ: Admin, Museum Staff, Staff, Curator. Lỗi validation/đăng ký trùng/password policy: HTTP 400. Sai email/mật khẩu hoặc tài khoản đang lockout: HTTP 401 với thông báo chung, không cấp JWT. Identity khóa tài khoản sau 5 lần sai theo cấu hình hiện có.

## Cấu hình

- Development dùng `http://localhost:5169`, cho phép CORS có chọn lọc từ localhost/127.0.0.1 port 5173 (Vite), 4173 (preview), 5174 (Flutter web). Không dùng wildcard hay allow credentials cho bearer token.
- Production: cấu hình origin chính xác bằng `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, v.v. và chạy HTTPS. HTTPS redirect chỉ bỏ trong Development để HTTP local/preflight dùng được.
- Development startup bổ sung các role bị thiếu từ cùng seed definitions của EF; không đổi user, password hoặc role đã có. Đã bổ sung role Curator còn thiếu trên DB local khi khởi động lại. Production vẫn provision role qua migration/seed theo quy trình triển khai.
- Task auth không tạo migration và không replay các migration cũ đang Pending. Role IDs/seed data giữ nguyên so với model trước khi tách helper.

## Hành vi FE

- Flutter: register/login chỉ thành công nếu nhận đủ token, hạn dùng, profile và đúng một role hợp lệ. Từ chối tài khoản nghiệp vụ với hướng dẫn dùng cổng web. Không mặc định mọi tài khoản là Visitor.
- React: chỉ cho bốn role nghiệp vụ, không mặc định role Admin khi parse lỗi. Visitor được hướng dẫn dùng `/visitor/`; đăng ký Visitor có link `/visitor/?mode=register`.
- Session lưu một record đầy đủ, khôi phục profile đúng và tự đăng xuất khi hết hạn. Các session cũ thiếu expiry/role hoặc boolean authenticated từ mock không còn được dùng. Đây là kiểm tra phiên để hiển thị UI; BE vẫn xác thực chữ ký/expiry/quyền JWT ở các endpoint được bảo vệ.
- React đã bỏ nút tài khoản mẫu, fallback password mẫu và đổi vai trò bằng local state. Muốn đổi tài khoản phải đăng xuất rồi xác thực lại.
- Network/timeout/HTML từ proxy/API lỗi/response thiếu token đều báo lỗi, không tạo phiên giả. Quên mật khẩu chưa có API; Flutter báo chưa hỗ trợ, không giả báo đã gửi email.

## Kiểm thử

```powershell
dotnet run --project Smart_Museum_Guide_System/tests/SMGS.AuthChecks
dotnet run --project Smart_Museum_Guide_System/tests/SMGS.AuthChecks -- --database --http
```

`--database` dùng tài khoản thử có email ngẫu nhiên và tự xóa để kiểm tra Identity/role/rollback trên DB cấu hình. `--http` khởi động API thật với schema PostgreSQL `smgs_auth_http_test_<Guid>` và JWT key tạm, kiểm tra HTTP register/login/duplicate/wrong password/CORS, dừng child process và xóa schema trong finally.

Để kiểm tra FE client thật với server tạm:

```powershell
dotnet run --project Smart_Museum_Guide_System/tests/SMGS.AuthChecks -- --http --hold
```

Lệnh in AUTH_TEST_BASE_URL, giữ server/schema đến khi nhấn Enter. Dùng URL đó cho các live tests theo hướng dẫn FE, sau đó Enter để dọn schema. Không chạy live tests bằng tài khoản thật; các tài khoản `@auth-test.invalid` chỉ sống trong fixture.
