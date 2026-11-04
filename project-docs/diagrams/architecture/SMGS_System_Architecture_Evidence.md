# SMGS — Bằng chứng kiến trúc hệ thống

Phạm vi khảo sát gồm hai checkout BackEnd và FrontEnd. Trang 1 của sơ đồ mô tả runtime nghiệp vụ. Trang 2 tách các kết nối do client khởi tạo để có thể trình chiếu riêng. Mũi tên đi từ bên gọi sang bên cung cấp, nhãn mô tả cả dữ liệu trả về khi cần. “Implemented” chỉ implementation trong mã nguồn. “Configured” chỉ cấu hình đã tìm thấy.

## Component inventory

Quy ước đường dẫn: `API`, `App`, `Infra` và `Domain` lần lượt là các project SMGS.API, SMGS.Application, SMGS.Infrastructure và SMGS.Domain dưới `BackEnd/Smart_Museum_Guide_System/`. `Web` là `FrontEnd/smgs_web/src/`, `Mobile` là `FrontEnd/smgs_mobile/lib/`.

| Component | Technology | Architecture role | Status | Evidence |
|---|---|---|---|---|
| Visitor app | Flutter, flutter_map, mobile_scanner, model_viewer_plus | Bản đồ, nhận diện, quét QR, xem GLB và nghe thuyết minh | Implemented | FrontEnd/smgs_mobile/pubspec.yaml, Mobile/screens/artifact/artifact_3d_screen.dart |
| Staff portal | React, Vite, fetch | Admin, Staff, Museum Staff, Curator | Implemented | Web/services/auth.js, Web/App.jsx |
| Web hosting | Vercel | Build và serve smgs_web/dist | Configured | FrontEnd/vercel.json |
| Backend nghiệp vụ | ASP.NET Core, JWT bearer | Một web host với nhiều controller, phân quyền bằng role | Implemented | API/Program.cs, API/SMGS.API.csproj, Infra/DependencyInjection.cs |
| Database | EF Core, Npgsql, PostgreSQL | Dữ liệu nghiệp vụ chung | Implemented | Infra/DependencyInjection.cs, Infra/Data/ApplicationDbContext.cs |
| Object storage | Cloudflare R2, AWS SDK for S3 | File media, mô hình GLB và ảnh bản đồ | Implemented | Infra/Services/R2FileStorageService.cs, ArtifactAssetStore.cs, IndoorImageStore.cs |
| Map & Routing Services | OSM raster tiles, OSRM, Photon | Bản đồ nền, tìm đường, tìm địa điểm | Implemented | Mobile/widgets/interactive_heritage_map.dart, Infra/Services/OsrmRoutingService.cs, PlaceSearchService.cs |
| External directions | Google Maps URL | Mở chỉ đường trong trình duyệt | Implemented | Web/components/museums/MuseumMapPreview.jsx |
| TTS, Vision, 3D Scan | HTTP endpoints | Hợp đồng client cho audio, truy hồi ảnh, job dựng mô hình | Configured | FrontEnd/smgs_web/vite.config.js, Web/services/{ttsApi,visionApi,scan3dApi}.js, Mobile/services/{tts_api_service,vision_api_service}.dart |
| AI runtime và training | Python/model | Chưa có mã server AI trong hai checkout để xác nhận topology, model hoặc pipeline train | Unverified | BackEnd/project-docs/AI_VISION_REFERENCE_IMAGES_FE_BE.md mô tả repo AI riêng |
| Payment workflow | ASP.NET Core + PostgreSQL | Tạo lần thanh toán, nhân viên xác nhận kết quả | Implemented | API/Controllers/CommerceController.cs, Infra/Services/CommerceService.cs |
| Payment Gateway | Provider chưa xác định | Tích hợp ngoài trong hình tham chiếu 2 | Planned | Hình 2; StartPayment và ConfirmPayment hiện thao tác DB nội bộ |

## Communication matrix

| Source | Destination | Protocol / Data | Purpose | Evidence |
|---|---|---|---|---|
| Flutter | ASP.NET API | HTTP(S) REST / JSON, file bytes | Đăng nhập, khám phá, tuyến đường, xem nội dung | Mobile/core/network/api_config.dart, Mobile/services/backend_auth_service.dart, discovery_api_service.dart; API/Controllers/ArtifactAssetsController.cs |
| React | ASP.NET API | HTTP(S) REST / JSON, multipart, file bytes | Quản lý nội dung, upload và đọc media | Web/services/mediaApi.js, auth.js; API/Controllers/ArtifactAssetsController.cs |
| ASP.NET API | PostgreSQL | EF Core / PostgreSQL wire protocol | Đọc ghi dữ liệu, metadata, giao dịch | Infra/DependencyInjection.cs (UseNpgsql), Infra/Services/CommerceService.cs |
| ASP.NET API | Cloudflare R2 | HTTPS / S3-compatible API | PUT, HEAD, GET, DELETE theo object key | Infra/Services/R2FileStorageService.cs |
| ASP.NET API | OSRM | HTTP(S) GET / JSON, GeoJSON geometry | Tuyến ngoài trời, profile driving | Infra/Services/OsrmRoutingService.cs, Infra/DependencyInjection.cs |
| ASP.NET API | Photon | HTTPS GET / GeoJSON features | Tìm địa điểm | Infra/Services/PlaceSearchService.cs, Infra/DependencyInjection.cs |
| Flutter | OpenStreetMap tiles | HTTPS GET / PNG | Tải tile z/x/y với flutter_map | Mobile/widgets/interactive_heritage_map.dart |
| Flutter | Vision endpoint | HTTP multipart image / JSON | Gửi ảnh chụp và nhận kết quả top-k | Mobile/services/vision_api_service.dart |
| Flutter | TTS endpoint | HTTP POST JSON / audio/wav | Đọc thuyết minh tiếng Việt | Mobile/services/tts_api_service.dart |
| React | Vision endpoint | HTTP multipart image / JSON | Quản lý ảnh tham chiếu | Web/components/artifacts/ArtifactDetailModal.jsx, Web/services/visionApi.js |
| React | TTS endpoint | HTTP POST JSON / audio/wav | Tổng hợp và phát audio | Web/services/ttsApi.js, Web/components/artifacts/ArtifactDetailModal.jsx |
| React | 3D scan endpoint | HTTP multipart photos / JSON; GET status, POST approve | Gửi ảnh và quản lý job scan | Web/services/scan3dApi.js, Web/components/artifacts/ArtifactDetailModal.jsx |
| React | Google Maps directions | HTTPS / URL có destination | Người dùng mở chỉ đường ngoài portal | Web/components/museums/MuseumMapPreview.jsx |

## Quyết định kiến trúc và hiệu chỉnh hình 2

- **Backend nghiệp vụ là monolith phân lớp.** API, Application, Domain và Infrastructure chạy trong cùng host. DI đăng ký repository cho user, museum, artifact, content, visit, feedback và complaint. Một số service, ví dụ CommerceService và RecognitionService, dùng DbContext trực tiếp. Bộ test có entry point riêng. Chưa thấy application gateway, broker, SignalR/WebSocket hoặc gRPC trong luồng nghiệp vụ.
- **Flutter gọi API trực tiếp.** Vite proxy /api ở môi trường phát triển. Cấu hình Vercel chứng minh build web, chưa xác nhận rollout production hay upstream sản xuất cho /api và /ai-*.
- **Database và storage có nhiệm vụ khác nhau.** Domain/Entities/ArtifactAsset.cs lưu StorageKey, FileName, ContentType, FileSize và các thông số viewer. POST /api/artifacts/{id}/model-3d nhận multipart; MediaValidation kiểm tra GLB. API đọc object và stream qua GET model-3d/file hoặc media/{mediaId}/file. R2 có helper ký URL tải nhưng không thấy caller trong luồng artifact. Web dùng @google/model-viewer, Flutter dùng model_viewer_plus. File GLB tĩnh đi kèm frontend cũng có thể được hiển thị. AWS SDK ở đây kết nối Cloudflare R2.
- **Bản đồ nền, tìm địa điểm và tìm tuyến là ba luồng riêng.** Flutter mặc định dùng tile.openstreetmap.org. Backend gọi Photon (mặc định photon.komoot.io) và OSRM (Osrm:BaseUrl + route/v1/driving). BackEnd/scripts/osrm-local-bridge.mjs có cầu nối loopback dành cho local tới routing.openstreetmap.de/routed-car/. Sơ đồ dùng tên provider để không gán địa chỉ demo cho production. Link Google Maps chỉ mở trang chỉ đường.
- **TTS, Vision và 3D Scan có mã client thực sự.** Proxy Vite mặc định lần lượt dùng cổng 8000, 8001, 8002; Flutter có URL riêng cho TTS/Vision. Scan dùng polling HTTP. TTS trả audio bytes. Luồng QR trong Infra/Services/RecognitionService.cs tra mã trong PostgreSQL. Chưa xác nhận service NLP, inference qua .NET, 3 process FastAPI độc lập hoặc training online.
- **Tài liệu Vision và client hiện tại khác nhau.** BackEnd/project-docs/AI_VISION_REFERENCE_IMAGES_FE_BE.md mô tả .NET làm trung gian, CLIP ViT-B/32 ONNX và schema AI dùng chung PostgreSQL. Client thực tế gọi interface Vision riêng. Sơ đồ không gắn model, DB, vector store hay schema vào server AI khi chưa có implementation để đối chiếu.
- **Thanh toán ngoài chưa có đường gọi.** Staff xác nhận payment attempt qua endpoint có role guard. Chưa thấy payment provider SDK, outbound checkout hoặc webhook. Vì thế Payment Gateway dự kiến chỉ ghi ở inventory.
- **Hạ tầng production còn mở.** Hai checkout có cấu hình CI và Vercel. Chưa có đủ bằng chứng triển khai AWS, ECS/EKS, CloudWatch, backend hosting, database hosting hoặc Redis. Không đưa các dịch vụ này vào các luồng runtime đã xác minh.

## Validation

Hai trang dùng mxCell native cho thành phần, biểu tượng, tiêu đề, boundary và connector. Mỗi cạnh có một dòng trong communication matrix. Chưa chạy smoke test production hay xác minh endpoint AI đang online.

Theo phản hồi của người dùng, trang 2 thể hiện ngay các interface AI đã tích hợp. Python / FastAPI là công nghệ trong hình thiết kế, CLIP ViT-B/32 ONNX là model được nêu trong tài liệu Vision. Chưa xác minh implementation Python hoặc model thực tế được nạp.
