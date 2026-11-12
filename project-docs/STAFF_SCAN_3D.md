# Museum Staff: ảnh → GLB → hiện vật

Gateway `ArtifactScanController` dùng JWT Admin/Museum Staff và `CanManageArtifactAsync` trước mọi truy cập. Job trả về phải thuộc đúng artifact ID trước khi tải GLB, lưu hoặc bỏ. AI service key chỉ nằm tại BE/AI.

| BE API | Chức năng |
|---|---|
| GET api/artifacts/{id}/scans | Job chưa lưu/bỏ, tối đa 20 |
| POST api/artifacts/{id}/scans | Multipart images, mode, metadata và referenceObj tùy chọn; trả 202 |
| GET api/artifacts/{id}/scans/{jobId} | Trạng thái/tiến độ |
| GET api/artifacts/{id}/scans/{jobId}/model | Stream GLB có xác thực |
| POST api/artifacts/{id}/scans/{jobId}/save | Ghi model bằng IArtifactAssetService rồi đánh dấu AI job completed |
| POST api/artifacts/{id}/scans/{jobId}/discard | Bỏ kết quả REVIEW_REQUIRED/FAILED |

Lưu model dùng bảng/storage media hiện có; không cần schema GLB mới. Cấu hình `AiScan__BaseUrl` và `AiScan__ServiceKey`. `tools/start-with-scan.ps1` đọc `.env` từ repo AI-System cạnh repo BackEnd, dùng cùng cấu hình DB và đổi host.docker.internal thành localhost cho BE chạy trên host. Không in khóa/mật khẩu. Cần PostgreSQL thực sự chạy và schema BE tương thích trước khi khởi động.

Xem [hướng dẫn đầy đủ, giới hạn CPU worker, và kiểm chứng](<../../AI-System/3D Scan/STAFF_WEB_GUIDE.md>). MediaChecks kiểm tra permission và save thành công/thất bại bằng HTTP/storage mock; không thay thế một lần chạy reconstruction/DB thật.

Gateway đệm GLB tải từ AI vào file tạm có thể seek (tối đa 100 MiB, kiểm tra đủ byte, tự xóa khi đóng) trước khi gọi media validator. Luồng HTTP không seek được nên truyền thẳng sẽ bị validator từ chối; không nới kiểm tra định dạng để bỏ qua lỗi. MediaChecks dùng stream không seek để bắt regression này.
