# FE–BE contract: ảnh tham chiếu nhận diện hiện vật

**Trạng thái:** đề xuất tích hợp, chưa có API BE kết nối với portal.  
**FE liên quan:** `FrontEnd/smgs_web`, chi tiết hiện vật → tab **Mã QR & Nhận diện**.  
**AI liên quan:** `AI-System/AI Vision` (CLIP ViT-B/32 ONNX image retrieval).

## 1. Mục tiêu và luồng nghiệp vụ

Curator hoặc nhân viên bảo tàng được phân quyền thêm ảnh đã xác nhận vào bộ ảnh tham chiếu của một hiện vật cụ thể. FE không yêu cầu nhập tag hiện vật cho từng ảnh: người dùng đang ở hồ sơ hiện vật, và BE gắn ảnh với `artifactId` từ route. Nhãn tùy chọn như “mặt trước” chỉ phục vụ quản trị, không phải đầu vào bắt buộc của CLIP hiện tại.

```text
Museum staff/Curator → mở hồ sơ hiện vật → tải ảnh tham chiếu
  → FE gọi BE (JWT/session người dùng)
  → BE xác thực role, museum scope, hiện vật active và file
  → BE lưu file + artifact_reference_images
  → BE gọi AI Vision nội bộ để tạo/lưu embedding
  → FE hiển thị trạng thái chờ index / đã index / lỗi

Flutter khách → gửi ảnh truy vấn qua BE → BE gọi AI Vision
  → AI trả artifactId/candidates → BE kiểm tra phạm vi và trả thông tin hiện vật đã duyệt
```

Ảnh khách gửi để nhận diện là ảnh truy vấn, **không** tự động thêm thành ảnh tham chiếu. Nhân viên phải chủ động duyệt và tải ảnh đó qua luồng quản trị nếu muốn dùng làm mẫu.

## 2. Vị trí và trạng thái FE

- Tại `ArtifactDetailModal`, tab hiện có **Mã QR & Nhận diện**, đặt khu vực **Ảnh tham chiếu nhận diện** dưới khối QR.
- Upload nhiều file, xem thumbnail/tên/trạng thái và gỡ ảnh. FE giới hạn hiển thị tối đa 20 ảnh mỗi hiện vật theo cấu hình AI Vision hiện tại.
- Người có quyền chỉ là curator/nhân viên được BE phân quyền cho museum tương ứng; admin có thể được phép theo policy dự án. FE ẩn/disable control với role chỉ đọc, nhưng BE vẫn phải cưỡng chế quyền.
- Bản FE hiện tại là thiết kế tương tác xem trước: file chỉ tạo object URL trong phiên, trạng thái `Chờ kết nối BE`, chưa upload, chưa lưu bền vững, chưa gọi AI Vision. Không coi trạng thái này là đã index.
- Form sửa hiện vật hướng người dùng sang tab này thay cho nhập danh sách đường dẫn ảnh thủ công.

## 3. DBML hiện có

`BackEnd/project-docs/smgs_schema.dbml` đã định nghĩa:

```dbml
Table artifact_reference_images {
  id int [pk, increment]
  artifact_id int [not null, ref: > artifacts.id]
  image_url varchar [not null]
  embedding_ref varchar [note: 'Tham chiếu vector/đặc trưng phía AI nếu có']
}
```

BE nên quản lý record này và nơi lưu file (object storage hoặc media service). AI Vision không lưu ảnh gốc; service hiện chỉ lưu embedding SQLite cùng artifact ID. Nên bổ sung migration (không chỉ sửa DBML) cho metadata cần thiết như `status`, `created_by`, `created_at`, `updated_at`, `error_message`/`indexed_at` và ràng buộc số ảnh. Có thể lưu model/version trong index service; tránh dùng `embedding_ref` như URL ảnh.

## 4. API BE đề xuất cho portal

Các path dưới đây là **đề xuất**, chưa tồn tại trong repo.

### Liệt kê ảnh

`GET /api/v1/artifacts/{artifactId}/reference-images`

Response:

```json
{
  "items": [
    {
      "id": 731,
      "artifactId": 123,
      "imageUrl": "https://media.example/...",
      "fileName": "front.jpg",
      "status": "indexed",
      "createdAt": "2026-10-22T10:00:00Z",
      "indexedAt": "2026-10-22T10:00:02Z"
    }
  ],
  "limit": 20,
  "count": 1
}
```

### Upload/index ảnh

`POST /api/v1/artifacts/{artifactId}/reference-images`  
`Content-Type: multipart/form-data`, file field `image`.

- Xác thực session/JWT portal. Không nhận AI service key từ FE.
- Xác nhận người dùng có quyền chỉnh sửa hiện vật thuộc museum; hiện vật phải tồn tại và active.
- Chấp nhận JPEG/PNG/WebP, kiểm tra MIME thực tế, kích thước giới hạn và ảnh decode được; khuyến nghị giới hạn upload 10 MB/file.
- Enforce tối đa 20 ảnh đang hoạt động trên artifact (nên cấu hình thay vì hardcode).
- Idempotency hoặc phát hiện file trùng là hữu ích để tránh double-submit.
- Trả `201` với record và `status: "pending"` nếu index async; hoặc `200` với `indexed` nếu xử lý đồng bộ.

Response mẫu:

```json
{
  "id": 731,
  "artifactId": 123,
  "imageUrl": "https://media.example/...",
  "fileName": "front.jpg",
  "status": "pending"
}
```

### Gỡ ảnh

`DELETE /api/v1/artifacts/{artifactId}/reference-images/{referenceImageId}`

BE xác thực quyền, xác nhận ảnh thuộc đúng artifact, xóa/đánh dấu xóa record và media, đồng bộ xóa embedding. Trả `204` khi hoàn tất hoặc `202` nếu đồng bộ xóa bất đồng bộ.

### Mã lỗi nên thống nhất

- `401` chưa đăng nhập; `403` không có role/museum scope.
- `404` artifact hoặc ảnh không tồn tại/không thuộc artifact.
- `409` artifact inactive hoặc đã đủ giới hạn ảnh.
- `413` file quá lớn; `415` định dạng/ảnh không hợp lệ.
- `502` AI Vision không khả dụng nếu BE xử lý đồng bộ; nếu xử lý nền thì giữ `pending`/`failed` và cho retry.

## 5. Tích hợp AI Vision từ BE

Service hiện có trong `AI-System/AI Vision/app/main.py`:

- `POST /api/v1/references?artifact_id={id}` multipart `image`, header nội bộ `X-Service-Key`; thêm embedding và trả `{id, artifactId, status: "indexed"}`.
- `DELETE /api/v1/references/{artifact_id}` xóa **toàn bộ** reference embeddings của artifact, chưa có endpoint xóa một reference riêng.
- `POST /api/v1/recognitions` nhận ảnh truy vấn, trả artifact ID/candidates, similarity và `requiresConfirmation`.

BE giữ `AI_SERVICE_API_KEY` ở secret store/private network; tuyệt đối không đưa key này vào browser/Flutter. Để xóa một ảnh mà không làm mất các ảnh cùng artifact, BE cần chọn một phương án:

1. Nâng AI Vision để hỗ trợ xóa theo `referenceId` (khuyến nghị); lưu `embedding_ref` trong DB.
2. Hoặc xóa toàn bộ embedding của artifact rồi đọc/tái index tất cả ảnh tham chiếu còn lại.

Upload nên tránh trạng thái DB/file/index lệch nhau. Khuyến nghị lưu record `pending`, enqueue index job/outbox, cập nhật `indexed` khi Vision thành công; retry có kiểm soát và đánh dấu `failed` kèm lỗi an toàn để FE hiển thị. Khi xóa artifact hoặc ảnh, đồng bộ media và embedding theo cùng quy trình bù trừ.

## 6. Authorization và ranh giới trách nhiệm

- FE chỉ cung cấp UX; quyền kiểm tra phải ở BE theo authenticated user, role và museum scope.
- System staff/read-only không được thêm hoặc xóa; visitor không được gọi endpoint quản trị.
- Curator/museum staff chỉ quản lý artifact thuộc museum được giao. Quy tắc admin toàn hệ thống cần khớp authorization policy hiện có.
- Service Vision chỉ nhận gọi nội bộ từ BE, không public ra app khách.
- AI trả `artifactId`; BE kiểm tra artifact active/museum scope và truy vấn narration đã duyệt. Không để kết quả AI tự sinh hoặc thay nội dung thuyết minh.
- Ảnh truy vấn từ khách không tạo record `artifact_reference_images`.

## 7. Tiêu chí nghiệm thu nối FE–BE

1. Danh sách ảnh và trạng thái tải từ BE khi mở tab; refresh trang không mất dữ liệu.
2. Người có quyền tải nhiều ảnh hợp lệ; BE lưu record có đúng `artifact_id`, trả trạng thái index và FE cập nhật trạng thái.
3. Người không có quyền bị BE từ chối dù gọi API ngoài FE.
4. Đạt giới hạn 20 ảnh thì FE chặn upload và BE trả lỗi `409` nếu bị bypass.
5. Ảnh gỡ khỏi hồ sơ không còn được Vision so khớp; xóa được đúng ảnh, không xóa nhầm ảnh khác.
6. Query recognition từ Flutter đi qua BE; kết quả không tự thêm ảnh tham chiếu và narration lấy từ nội dung đã duyệt.
7. Khi Vision lỗi/tạm offline, FE nhìn thấy trạng thái lỗi/pending và có thể thử lại; file/record không bị báo `indexed` giả.

## 8. Những phần chưa được triển khai

- Portal FE chưa gọi API; preview hiện tại chỉ ở local state.
- BE endpoints, authorization policy cụ thể, migration/storage, background job/retry chưa triển khai trong phạm vi thay đổi này.
- Flutter camera/query recognition hiện còn mock; chưa nối BE.
- CLIP hiện là pretrained image retrieval, chưa fine-tune bằng bộ ảnh hiện vật SMGS; cần đánh giá bằng ảnh thật và hiệu chỉnh ngưỡng trước triển khai thực tế.
