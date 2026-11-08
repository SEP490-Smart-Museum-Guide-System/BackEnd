# Chatbot theo hiện vật — 08/11/2026

## Trạng thái thực tế

Đã thay câu trả lời mẫu trên Flutter bằng API BE và dịch vụ ONNX local. Theo lựa chọn của team, không dùng Gemini/cloud provider. Model Qwen2.5-1.5B-Instruct INT4 đã chạy thật qua API ASGI: 10/10 ca smoke đạt các kiểm tra đơn giản, khoảng 7–8 giây/lượt trên CPU i5-12500H. Chưa kiểm thử luồng DB/deploy thật; không tính các con số này thành độ chính xác tổng quát.

CLIP chỉ tìm hiện vật từ ảnh. Chatbot là thành phần riêng: Visitor mở một hiện vật đã xác định, hỏi về nó; BE lấy nội dung tiếng Việt đã xuất bản và gửi ngữ cảnh này cho model văn bản. Không gửi embedding CLIP sang model để suy đoán lịch sử.

## Luồng và quyền

1. Visitor đăng nhập bằng JWT, mở chi tiết hiện vật, bấm **Hỏi hướng dẫn viên**.
2. Flutter gọi `POST /api/artifacts/{artifactId}/guide/messages`, chỉ gửi câu hỏi và tối đa bốn cặp hội thoại hoàn tất gần nhất.
3. BE dùng `IArtifactContentService.GetPublishedAsync(artifactId, "vi", userId)`: chỉ hiện vật đang hoạt động, bảo tàng chưa bị vô hiệu hóa, nội dung hiện hành đã xuất bản và quyền free/premium của Visitor. Kiểm tra lại mỗi lượt; không tin ID bảo tàng, nguồn hoặc quyền do client tự khai.
4. BE gửi tên/tiêu đề/mô tả/narration đã xuất bản cùng câu hỏi cho adapter. Không gửi API key cho FE, không gửi hồ sơ cá nhân hoặc review note của Curator.
5. Model trích đoạn trả lời tiếng Việt từ nguồn, không dùng lịch sử assistant làm bằng chứng. Source guard bắt buộc đáp án nằm trong tên/mô tả/narration sau chuẩn hóa; đáp án không có nguồn → `supported=false`; BE thay bằng thông báo thiếu thông tin cố định. Đây là MVP hỏi đáp trích xuất, chưa phải hội thoại diễn giải tự do.
6. BE tự tạo nguồn tham khảo từ ID phiên bản thật. Flutter hiện nguồn/phiên bản, câu hỏi tiếp nối, nghe câu trả lời qua TTS hiện có, thử lại khi lỗi và cuộc trò chuyện mới. Hội thoại chỉ ở bộ nhớ màn hình; đóng màn/đổi hiện vật/đăng xuất sẽ xóa. Không ghi lịch sử vào DB hoặc localStorage.

## API

Yêu cầu (Bearer JWT role Visitor):

```json
{
  "question": "Hiện vật này dùng để làm gì?",
  "history": [
    { "role": "user", "text": "Đây là loại hiện vật gì?" },
    { "role": "assistant", "text": "Câu trả lời của lượt trước." }
  ]
}
```

Lược đồ thành công:

```json
{
  "statusCode": 200,
  "message": "Thành công",
  "data": {
    "artifactId": 17,
    "answer": "Câu trả lời dựa trên nội dung đã xuất bản.",
    "supported": true,
    "sources": [
      { "contentVersionId": 91, "versionNo": 2, "title": "Tiêu đề nội dung", "languageCode": "vi" }
    ]
  }
}
```

Các số trên chỉ minh họa hợp đồng, không được seed/index vào DB. Câu hỏi 1–1.000 ký tự; lịch sử tối đa 8 message, mỗi message 1–4.000 ký tự, role user/assistant luân phiên thành cặp. Body tối đa 144.000 byte. Nội dung nguồn tối đa 24.000 ký tự; quá dài trả 422 thay vì âm thầm cắt. Đáp án tối đa 4.000 ký tự. Deadline inference 45 giây, BE timeout 55 giây, Flutter 65 giây. Model có thêm budget 2.048 token đầu vào và 384 token đầu ra; quá budget trả 422.

401: chưa đăng nhập; 403: sai role hoặc thiếu quyền premium; 404: không có nội dung phù hợp; 429: quá giới hạn hoặc provider bận; 503: AI chưa cấu hình/lỗi kết nối; 504: timeout; 502: phản hồi model không hợp lệ. UI không đổi các lỗi này thành câu trả lời giả và không đọc lỗi bằng TTS.

Rate limit: 10 câu/phút/user/API instance; cần limiter dùng chung khi scale nhiều replica. Không có retry tự động phát sinh thêm lượt gọi provider.

## Chạy chatbot ONNX local

Model `onnx-community/Qwen2.5-1.5B-Instruct`, revision `d54957e3af1be04546436e874a60fe386041b244`, graph INT4 khoảng 1,79 GB, ONNX Runtime 1.23.2 CPU. CLIP không phải model chatbot. Source, runtime và KV cache nằm trong `AI-System/AI Chat`; không dùng PyTorch hoặc API key cloud.

Terminal AI (model đã tải trên máy, lần đầu khác máy dùng `-DownloadModel`):

```powershell
cd D:\SEP409\AI-System
.\tools\start-local-chat.ps1
```

Terminal BE (DB/JWT cấu hình như trước, chỉ một BE chạy ở cổng 5169):

```powershell
cd D:\SEP409\BackEnd\Smart_Museum_Guide_System
.\tools\start-api-with-local-chat.ps1
```

Script AI tạo khóa nội bộ ngẫu nhiên ở `AI-System/state/chat-service-key.txt` đã gitignore; script BE đọc cùng khóa vào biến môi trường của process. Không in khóa hoặc đưa vào FE. Để dừng, Ctrl+C trong terminal tương ứng. Mở Flutter Visitor `/visitor/` và chọn hiện vật có nội dung tiếng Việt đã xuất bản → **Hỏi hướng dẫn viên**.

Cấu hình BE thủ công: `ArtifactGuide__Enabled=true`, `ArtifactGuide__BaseUrl=http://127.0.0.1:8003/`, `ArtifactGuide__ServiceKey` trùng `AI_CHAT_SERVICE_KEY` của Python. Mặc định BE tắt adapter nếu chưa cấu hình. URL dịch vụ do server cấu hình, không do client gửi. HTTP redirects bị tắt. Không dùng khóa Gemini.

`GET http://127.0.0.1:8003/ready` trả trạng thái model thật. Không cần migration DB cho chatbot. Không đưa weights ONNX lên Git/Vercel: Vercel chỉ phục vụ FE; BE và AI Chat phải chạy trên máy/server có đủ RAM. Khi deploy, cấu hình private service URL và secret tương ứng.

Chi tiết cài Python, Docker và giới hạn: [AI Chat README](../../AI-System/AI%20Chat/README.md). Kết quả model thật và baseline lỗi ban đầu: [báo cáo](../../AI-System/evaluations/chat-2026-11-08/README.md).

## Kiểm thử

```powershell
cd D:\SEP409\BackEnd\Smart_Museum_Guide_System
dotnet run --project tests/SMGS.GuideChecks

cd D:\SEP409\FrontEnd\smgs_mobile
flutter test test/ai_guide_test.dart test/widget_test.dart
flutter analyze --no-pub
```

Test BE dùng kho nội dung và HTTP provider fixture, chạy cả route HTTP thật với auth fixture cô lập: từ chối truy cập, draft/archived/sai hiện vật, mất quyền premium ở câu hỏi sau, giới hạn context/history, thiếu key, lỗi/timeout/quota/JSON sai, không lộ body provider. Các test auth hiện hành kiểm tra JWT thật. Flutter kiểm tra HTTP contract, nguồn, hội thoại tiếp nối, retry, chat mới và bỏ kết quả đến muộn sau đăng xuất.

Kết quả local: 37 kiểm tra BE service/adapter và các kiểm tra HTTP quyền/validation qua; bộ ContentChecks hiện hành qua; 10 test Python AI Chat qua; toàn bộ Flutter 78 pass + 1 skip trước lần chỉnh semantics, 12 test liên quan chạy lại qua sau chỉnh; `flutter analyze` không có vấn đề. Edge chạy 3 ca UI HTTP fixture qua. Model ONNX thật qua 10 ca smoke và service HTTP `/ready` + một câu hỏi qua cổng 8003 trả 200; những ca này không dùng DB nghiệp vụ.

Kiểm thử UI trình duyệt có thể tái hiện riêng:

```powershell
cd D:\SEP409\FrontEnd\smgs_mobile
flutter build web --release --no-pub --target test/browser/guide_harness.dart --output build/guide_harness
cd ..\smgs_web
npx playwright test --config playwright.guide.config.js
```

## Phần còn thiếu trước khi coi là hoàn tất

- Đã có model ONNX local chạy thật; cần kiểm thử thêm với dữ liệu DB và JWT Visitor thật qua BE, cả free/premium và nội dung vừa thay phiên bản.
- Đánh giá thêm tiếng Việt trên nhiều hiện vật. Source guard chỉ xác minh đoạn trích thuộc văn bản, chưa bảo đảm đoạn trích luôn trả lời đúng câu hỏi; chưa có kiểm chứng entailment tự động. Baseline chưa có guard từng bịa niên đại, đã giữ trong báo cáo.
- Hiện đây là hỏi đáp **theo một hiện vật**, chưa có chatbot toàn bảo tàng, tìm kiếm nhiều tài liệu/vector RAG, đặt vé, thao tác dữ liệu hoặc lịch sử chat bền vững.
- Inference local không gửi nội dung/câu hỏi cho cloud. Lần tải weights đầu cần internet. Chưa có chatbot chạy trực tiếp trong điện thoại/browser; đây là local AI server phía BE.
- Chưa có nhập giọng nói hoặc streaming. Nút nghe dùng TTS đã có; phải có TTS hoạt động để phát âm thanh thật.
- CLIP/camera → đúng artifactId là luồng riêng; chatbot không sửa được lỗi nhận diện sai hiện vật.

Chưa commit/push/deploy. Flutter web preview được cập nhật qua `FrontEnd/tools/update-visitor-web.ps1` trước khi đóng gói portal.
