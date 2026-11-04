# Đồng bộ BE dev với remote — 04/11/2026

## Nguyên nhân pull bị chặn

Local `dev` ở `870a94b`, chậm `origin/dev` ba commit:

- `a316c0f`: Cloudflare R2.
- `487a7fb`: sửa bản đồ.
- `4cb34b8`: tài liệu SRS/SDD.

Local có thay đổi chưa commit và file chưa được Git theo dõi. File `MuseumImagesController.cs` local trùng đường dẫn file mới trên remote; solution cũng cùng được bổ sung project kiểm thử. Không thể ghi đè một bên mà vẫn giữ đủ chức năng.

## Đã xử lý

1. Sao lưu tracked + untracked trong stash `codex-before-be-dev-sync-2026-11-04` (hash `f027812e55c7b453ffe101e75f24e5b524422931`). Giữ stash để dự phòng, không tự apply lại vì code đã được phục hồi và ghép.
2. Fast-forward `dev` tới `4cb34b8` từ `origin/dev`; không force push hoặc sửa lịch sử commit.
3. Giữ nguyên `MuseumImagesController` từ remote với endpoint ảnh và chính sách R2/quyền đọc hiện có.
4. Chuyển các endpoint multipart local `POST /api/museums/with-image` và `PUT /api/museums/{id}/with-image` sang `MuseumFormsController`, tránh trùng controller/route.
5. `MuseumImageStore` dùng `IFileStorageService`/Cloudflare R2 hiện có, không còn ghi ảnh mới vào disk local. Key theo mẫu `museums/{museumId}/images/{guid}.{ext}` và URL đọc qua endpoint ảnh của remote.
6. Khi tạo bảo tàng kèm ảnh, lấy ID bảo tàng thực rồi tạo key R2. Ghi metadata, upload ảnh và cập nhật URL trong transaction DB; lỗi dọn object mới và rollback transaction. Ảnh cũ được giữ theo chính sách của remote vì có thể được tham chiếu chung. R2 và DB không phải một distributed transaction; cleanup lỗi vẫn có thể cần đối soát object mồ côi.
7. Giữ các phần local `VisitorDirectoryController`, `VisitorMapCatalog` và test phòng/tầng/chỉ đường.
8. Giữ cả hai project `SMGS.MediaChecks` của remote và `SMGS.MuseumImageChecks` local trong solution; DI đăng ký cả R2 và store hỗ trợ form.

Không phục hồi các migration hoặc file media đã được team chủ động xóa trong commit remote. Không chạy migration/seed hay ghi DB/R2 thật.

## Kiểm tra

- Build toàn bộ solution: 0 warning, 0 error.
- `SMGS.MuseumImageChecks`: HTTP multipart thật qua service/repository thật; EF InMemory và storage fixture; đọc ảnh, từ chối quyền, key đúng phạm vi, từ chối ảnh sai, cleanup khi lưu URL lỗi, lỗi kho trả 503, giữ ảnh cũ và gỡ liên kết.
- `SMGS.MediaChecks`: validation media/3D, quyền, R2 request, đọc file, xử lý lỗi và cleanup — pass với fixture.
- `SMGS.IndoorMapChecks`: graph, tuyến một chiều/xe lăn, liên kết đúng hiện vật/phòng/tầng và yêu cầu đăng nhập — pass.
- `SMGS.ContentChecks`: quyền, nội dung, media/3D, review guards, audit và pagination — pass.
- Không còn file unmerged hoặc dấu conflict. `HEAD...origin/dev` là `0 0` khi hoàn tất đồng bộ.

EF InMemory không chứng minh rollback transaction PostgreSQL thật. Chưa kiểm thử live PostgreSQL/R2/JWT trong lần này.

Các thay đổi local vẫn **chưa commit và chưa stage**, như trước khi đồng bộ; không push. Stash dự phòng vẫn còn. Có thể xem lại bằng:

```powershell
cd D:\SEP409\BackEnd
git status
git log -3 --oneline
git stash list
```
