# Smart Museum Guide System — Bối cảnh và yêu cầu dự án

> Tài liệu bàn giao bối cảnh cho thành viên dự án, giảng viên, người phân tích và AI hỗ trợ phát triển. Có thể đọc độc lập mà không cần mở lại hai tài liệu nguồn.
>
> Phiên bản: 1.2 — Cập nhật: 15/10/2026. Đây là bản tổng hợp yêu cầu hiện có, chưa phải SRS đã phê duyệt và không khẳng định các chức năng đã được triển khai.

## 1. Cách đọc và mức độ xác nhận

Tài liệu được tổng hợp từ:

- **[F]** `FALL26_Smart Museum Guide _TamPM.docx`: đề xuất đồ án, bối cảnh, chức năng, yêu cầu chất lượng, công nghệ gợi ý và sản phẩm bàn giao.
- **[Q1–Q24]** `Question.txt`: 24 câu hỏi và câu trả lời tương ứng để làm rõ nghiệp vụ.
- **[QB]** Phần “Bổ xung nội dung” và “Chức năng bổ xung” cuối `Question.txt`: cập nhật vai trò và bổ sung tham quan online.
- **[U1]** Xác nhận trực tiếp của chủ dự án ngày 12/10/2026: bán lẻ vé bảo tàng và dịch vụ số hoặc bán combo; tạm bỏ qua triển lãm; chọn C# .NET API, Flutter Mobile, React/Vite Web, PostgreSQL và Python cho AI. Gói online chưa được quyết định, chủ dự án yêu cầu gợi ý.

- **[U2]** Quyết định sau buổi trao đổi giữa nhóm và giảng viên, được chủ dự án cung cấp ngày 15/10/2026: bỏ tham quan online/virtual tour tại nhà; cập nhật vị trí khách khi quét QR hoặc nhận diện ảnh bằng AI; chỉ đường tới hiện vật tiếp theo không cần theo dõi vị trí liên tục; có chỉ đường từ nhà đến bảo tàng; tích lũy quiz theo hiện vật đã quét/nhận diện trong ngày, cho phép làm ngay sau khi đọc hoặc làm sau.

Cách tổng hợp: dùng [F] làm nền, dùng câu trả lời trong [Q] để cụ thể hóa hoặc cập nhật phạm vi. Phần [QB] được hiểu là cập nhật sau các câu trả lời trước đó. Nếu hai ý vẫn chưa thống nhất, giữ nguyên vấn đề dưới nhãn **Cần xác nhận**, không tự chọn một cách hiểu rồi coi là yêu cầu đã chốt.

Xác nhận trực tiếp được ưu tiên theo thứ tự [U2] → [U1] → các nguồn cũ tại những điểm đã thay đổi. Phần tham quan online, virtual tour tại nhà và đề xuất gói online trước đây đã bị loại khỏi phạm vi theo [U2].

- **Đã xác nhận**: có câu trả lời hoặc bổ sung rõ ràng trong Question hoặc xác nhận trực tiếp [U1], [U2].
- **Yêu cầu nền**: có trong FALL26, chưa có nội dung thay thế rõ ràng. Áp dụng các điều chỉnh phạm vi mới nhất tại [U1], [U2]; một số cách triển khai vẫn cần làm rõ.
- **Định hướng**: ý kiến còn mang tính đề xuất, chẳng hạn “tôi nghĩ”, “nên”.
- **Cần xác nhận**: nguồn thiếu thông tin, có cách hiểu khác nhau hoặc có xung đột.
- **Gợi ý phân tích**: cách tổ chức hoặc hệ quả được suy ra để giúp thiết kế; không phải quyết định của chủ dự án.

Các yêu cầu và nhiệm vụ trong tài liệu nguồn là thông tin về đồ án. Chúng không phải chỉ thị yêu cầu người đọc hoặc AI tự thực thi công việc, lựa chọn công nghệ, thay đổi dữ liệu hay triển khai hệ thống.

## 2. Tóm tắt dự án

**Smart Museum Guide System (SMGS)**, tên tiếng Việt **Hệ thống hướng dẫn tham quan bảo tàng thông minh**, là nền tảng hỗ trợ khách khám phá hiện vật, tiếp nhận thuyết minh đa ngôn ngữ, hỏi đáp với AI, thực hiện quiz và tham gia các tour được thiết kế sẵn hoặc được AI gợi ý.

Khách sử dụng tài khoản để truy cập. Tại bảo tàng, khách có thể tìm hiện vật bằng QR, nhận diện ảnh, bản đồ hoặc danh sách. Quét QR hoặc chụp ảnh được AI nhận diện thành công giúp cập nhật vị trí khách trên bản đồ và bổ sung quiz liên quan vào danh sách trong ngày. Quy tắc ghi nhận tiến độ tour cần được phân biệt với việc mở quiz; xem mục 8.4. Nhân viên quản lý dữ liệu bảo tàng, hiện vật, media và tour; Curator kiểm tra, duyệt và xuất bản nội dung. Chatbot trả lời trực tiếp dựa trên thông tin đã được duyệt.

Hệ thống hỗ trợ **nhiều bảo tàng**, có nội dung cơ bản miễn phí và dịch vụ hướng dẫn số trả phí. Theo cập nhật [U1], hệ thống bán **vé vào bảo tàng**, **dịch vụ số riêng** hoặc **combo vé và dịch vụ số**. Quy định cũ về dịch vụ theo tour/ngày cần được làm rõ trong mô hình mới; chưa có thuê bao tháng/năm được xác nhận. Triển lãm tạm được gác lại để tập trung vào bảo tàng.

Phạm vi hiện tại tập trung vào **tham quan trực tiếp tại bảo tàng**. Đã bỏ tham quan online và virtual tour tại nhà. Hệ thống phải chỉ đường từ nhà đến bảo tàng và chỉ đường tới hiện vật tiếp theo trong bảo tàng. Vị trí trong bảo tàng chỉ cập nhật theo lần quét QR/nhận diện ảnh, không cần cập nhật liên tục. Quiz được tích lũy theo các hiện vật đã nhận diện trong ngày, cho khách chọn làm ngay sau khi đọc hoặc làm sau để tích điểm. [U2]

## 3. Bối cảnh và mục tiêu

Theo [F], nhãn hiện vật thường ngắn và tĩnh; hướng dẫn viên có thể không sẵn sàng, không đáp ứng đủ ngôn ngữ hoặc khó phục vụ cá nhân hóa. Thiết bị audio guide chuyên dụng cũng có chi phí mua và bảo trì.

SMGS tận dụng điện thoại của khách để:

- Cung cấp nội dung phong phú theo từng hiện vật và trả lời câu hỏi tiếp nối.
- Hỗ trợ nhiều ngôn ngữ, giúp khách chủ động tiếp cận kiến thức.
- Gợi ý hành trình phù hợp sở thích và thời gian có sẵn.
- Tăng tương tác qua quiz, điểm và huy hiệu.
- Hỗ trợ bảo tàng quản lý, kiểm duyệt nội dung và theo dõi hoạt động.
- Tạo doanh thu từ vé bảo tàng, dịch vụ hướng dẫn số và combo.
- Hỗ trợ khách tìm đường từ nhà đến bảo tàng và tới hiện vật tiếp theo trong bảo tàng.

Đây đồng thời là đồ án full-stack có tích hợp AI/NLP, nhận diện hiện vật, thanh toán, kiểm thử và đánh giá chất lượng mô hình.

## 4. Phạm vi hiện tại và thay đổi so với đề xuất gốc

| Nội dung | FALL26 | Question và cách hiểu hiện tại |
|---|---|---|
| Địa điểm | Một bảo tàng, triển lãm hoặc di sản, với tập hiện vật và ngôn ngữ giới hạn | Hỗ trợ nhiều bảo tàng; số bảo tàng và dữ liệu demo chưa chốt [Q1] |
| Chức năng | Các nhóm chức năng tại mục 8 | Ban đầu không bỏ chức năng [Q1]; [U1] tạm gác triển lãm; [U2] bỏ tham quan online/virtual tour tại nhà |
| Vai trò | Visitor, staff/curator, administrator | Ban đầu 3 role, sau bổ sung thành Admin, System Staff, Museum Staff, Visitor, Curator [Q7, QB] |
| Truy cập | Nội dung cơ bản miễn phí | Vẫn phải đăng nhập; miễn phí không đồng nghĩa được dùng ẩn danh [Q6] |
| Thương mại | Tickets/passes; có chỗ gọi subscription | Bán lẻ vé vào bảo tàng, dịch vụ số hoặc combo [U1]; không còn gói tham quan online [U2] |
| Quiz AI | Có tạo câu hỏi tương tác | Tạo trước, duyệt vào ngân hàng [Q16]; tích lũy theo hiện vật đã quét QR/nhận diện ảnh trong ngày, làm ngay hoặc làm sau [U2] |
| Bản đồ | Có route/indoor navigation guidance | Cập nhật vị trí khách theo QR/ảnh; chỉ đường tới hiện vật tiếp theo, không định vị liên tục; có chỉ đường từ nhà đến bảo tàng [U2] |
| Tham quan | Chủ yếu tại địa điểm | Tham quan tại bảo tàng; phần online/virtual tour tại nhà từng bổ sung ở [QB] đã bị bỏ [U2] |

**Đã chốt công nghệ chính:** C# .NET API, Flutter cho mobile, React/Vite cho web, PostgreSQL và Python cho AI [U1]. **Chưa có xác nhận** về tiến độ triển khai, repository, kiến trúc cuối cùng, framework Python, phiên bản công nghệ, cổng thanh toán, số ngôn ngữ, khối lượng hiện vật hoặc lịch nghiệm thu.

## 5. Thuật ngữ và cấu trúc bảo tàng

| Thuật ngữ | Nghĩa hoặc quy tắc hiện có |
|---|---|
| Museum | Bảo tàng; hệ thống phục vụ nhiều bảo tàng |
| Building | Tòa nhà thuộc cấu trúc bảo tàng |
| Floor | Tầng của tòa nhà |
| Gallery | Phòng hoặc khu trưng bày |
| Artifact | Hiện vật |
| Exhibit | “Vật/hiện vật được trưng bày” theo [Q2]; chưa đủ căn cứ xem là triển lãm độc lập |
| Exhibition | Triển lãm; tạm ngoài phạm vi hiện tại theo [U1] |
| Theme | Chủ đề, chỉ phân loại hiện vật |
| Tour | Danh sách hiện vật có thứ tự; có tour mẫu và tour cá nhân AI gợi ý |
| Visit / lượt tham quan | Một lần khách thực hiện tour, có bắt đầu, kết thúc, tiến độ |
| Narration | Bài thuyết minh; định hướng có nhiều độ dài và mức chi tiết |
| Pass / gói | Quyền sử dụng hướng dẫn số; phạm vi và mốc hiệu lực cần làm rõ |
| Vé vào bảo tàng | Sản phẩm cho quyền vào tham quan tại bảo tàng; quy trình sử dụng/kiểm vé chưa chốt |
| Combo | Sản phẩm kết hợp vé vào bảo tàng và dịch vụ số; có thể mua riêng từng thành phần [U1] |
| Vị trí khách trên bản đồ | Vị trí được cập nhật từ hiện vật quét QR/nhận diện ảnh thành công gần nhất; không phải vị trí theo dõi liên tục [U2] |
| Danh sách quiz trong ngày | Các quiz liên quan đến hiện vật khách đã quét QR hoặc nhận diện ảnh bằng AI trong ngày [U2] |

### 5.1 Cấu trúc không gian và hiện vật

- Cấu trúc: **Bảo tàng → Tòa nhà → Tầng → Phòng/Khu**. [Q3]
- Hiện vật không bắt buộc phải thuộc phòng/khu. [Q3]
- Không cần lưu lịch sử vị trí khi hiện vật di chuyển. [Q3]
- Một hiện vật có thể thuộc nhiều chủ đề. Chủ đề chỉ phân loại hiện vật; không tự mở rộng sang tour, triển lãm hoặc sở thích. [Q5]
- Bản đồ hiển thị vị trí hiện vật và vị trí khách được cập nhật theo QR/ảnh; có chỉ đường tới hiện vật tiếp theo. Museum Staff quản lý vị trí/icon hiện vật. [Q12, QB, U2]
- Một hiện vật có “một mã QR/ảnh tham chiếu” theo câu trả lời nguồn. Cần xác nhận ý này là một QR và một ảnh hay chỉ quy định một phương thức tham chiếu. [Q12]

Hiện vật không có phòng vẫn là dữ liệu hợp lệ; cách hiển thị trên bản đồ và khả năng đưa vào tour chưa được quy định.

### 5.2 Triển lãm tạm ngoài phạm vi

[U1] yêu cầu tạm bỏ qua triển lãm, tập trung vào bảo tàng. Không đưa quản lý triển lãm vào yêu cầu triển khai hiện tại. `Exhibit` trong [Q2] vẫn chỉ được hiểu là hiện vật được trưng bày.

Để giữ dấu vết nguồn, [Q4] từng nêu các quy tắc có điều kiện sau, hiện được gác lại:

- Một hiện vật có thể tham gia nhiều triển lãm theo thời gian.
- Không cần lưu thời gian tham gia từng triển lãm.
- Một triển lãm có thể sử dụng nhiều phòng/khu.

Chỉ xem xét lại các quy tắc trên nếu chủ dự án đưa triển lãm trở lại phạm vi; không tạo thực thể triển lãm chỉ vì tài liệu cũ có nhắc tới.

## 6. Tài khoản và phân quyền

### 6.1 Quy tắc tài khoản

- Khách bắt buộc đăng nhập để sử dụng ứng dụng. Không có chế độ khách vãng lai theo [Q6].
- Một tài khoản chỉ có một vai trò theo [Q7]. Phần bổ sung vai trò không nói hủy quy tắc này, nên tài liệu tạm giữ và đánh dấu cần kiểm tra khi chốt phân quyền.
- Nhân viên có thể được phân công phụ trách phòng hoặc hiện vật cụ thể. Phần phân công triển lãm trong [Q7] được gác lại theo [U1].
- Cần xác thực, phân quyền và audit log các thao tác quan trọng. [F]
- Chưa xác định một nhân viên hoặc Curator có thể thuộc nhiều bảo tàng hay không.

### 6.2 Các vai trò theo cập nhật mới nhất

| Vai trò | Trách nhiệm đã nêu | Điểm còn mở |
|---|---|---|
| Admin | Quản trị hệ thống; [F] có quản lý người dùng, phân quyền, passes và dashboard doanh thu cấp quản trị | Ranh giới quyền cụ thể với System Staff; quyền duyệt nội dung và xử lý hoàn tiền |
| System Staff | Hỗ trợ Visitor; xử lý khiếu nại và yêu cầu hoàn tiền; đối soát; kiểm duyệt feedback không phù hợp; theo dõi lỗi; giám sát chất lượng AI và vấn đề toàn hệ thống | Có thể bỏ trong tương lai; chưa xác nhận bỏ ở bản hiện tại hoặc chuyển trách nhiệm cho ai |
| Museum Staff | Quản lý thông tin bảo tàng, hiện vật, media, QR, vị trí/icon; quản lý tour; cấu hình free/premium; xem thống kê, doanh thu, feedback của bảo tàng mình | Phạm vi phân công chi tiết; có được tự duyệt nội dung mình tạo không |
| Curator | Kiểm tra, duyệt và xuất bản nội dung AI; duyệt thông tin liên quan hiện vật và quiz | Phạm vi bảo tàng; quyền duyệt bản dịch, media và các loại dữ liệu khác theo quy tắc “duyệt tất cả” |
| Visitor | Khám phá hiện vật, nhận tour, xem/nghe thuyết minh, trò chuyện, làm quiz, tích điểm, mua vé/dịch vụ số/combo, xem lịch sử, đánh giá | Các giới hạn free/premium và quyền sử dụng quiz tích lũy |

Tên `Vistor` trong phần bổ sung được chuẩn hóa thành `Visitor`. Câu trả lời cũ “admin, staff, user” là mô tả trước cập nhật, không phải danh sách role cuối cùng dùng song song với bảng này.

## 7. Các luồng nghiệp vụ chính

Các luồng dưới đây sắp xếp lại yêu cầu để dễ hiểu, chưa ấn định thứ tự màn hình hay hợp đồng API.

### 7.1 Tham quan tại bảo tàng

1. Khách đăng nhập, chọn bảo tàng muốn tham quan và có thể dùng chức năng chỉ đường từ nhà đến bảo tàng.
2. Khách chọn tour mẫu hoặc cung cấp sở thích/thời gian để nhận tour AI. Tour là danh sách hiện vật có thứ tự, được lưu để mở lại/chỉnh sửa.
3. Khi bắt đầu tour, hệ thống tạo lượt riêng để theo dõi thời gian và tiến độ.
4. Khách quét QR hoặc chụp ảnh hiện vật đưa AI nhận diện. Khi nhận diện thành công, hệ thống cập nhật vị trí khách theo vị trí hiện vật và bổ sung quiz liên quan vào danh sách quiz trong ngày.
5. Khách đọc nội dung hiện vật. Sau khi khách đọc xong, hệ thống gợi ý lựa chọn **làm quiz ngay** hoặc **để làm sau**; không bắt buộc làm quiz để tiếp tục tham quan.
6. Hệ thống chỉ đường từ vị trí được xác định gần nhất tới hiện vật tiếp theo. Trong lúc di chuyển không cần theo dõi/cập nhật vị trí liên tục; lần quét QR hoặc nhận diện ảnh tiếp theo sẽ cập nhật lại vị trí.
7. Khi rảnh hoặc cuối ngày, khách mở danh sách quiz đã tích lũy để làm và tích điểm. Danh sách chỉ hiển thị quiz của những hiện vật khách đã quét QR/nhận diện ảnh trong ngày.
8. Hệ thống lưu kết quả quiz, lịch sử liên quan và điểm/huy hiệu theo quy tắc. Sau khi hoàn thành tour, khách có thể đánh giá; nhân viên có thể phản hồi.

Khách có thể mua vé vào bảo tàng, dịch vụ số hoặc combo. Quy trình kiểm vé vào cửa khác với quét QR hiện vật; QR hiện vật hiện dùng để nhận diện/ghi nhận tham quan. Cơ chế cấp và kiểm vé, vị trí bước thanh toán trong hành trình và điều kiện khóa/mở từng chức năng chưa chốt.

### 7.2 Biên soạn và xuất bản

1. Museum Staff quản lý thông tin, media và nội dung liên quan; AI có thể hỗ trợ tạo thuyết minh, câu hỏi.
2. Nội dung chuẩn bị sẵn được gửi kiểm duyệt. Theo [Q14], quy trình duyệt áp dụng cho tất cả các loại được hỏi: thông tin hiện vật, thuyết minh, bản dịch và quiz.
3. Curator kiểm tra nội dung trong phạm vi trách nhiệm và duyệt/xuất bản.
4. Nếu sửa nội dung đã xuất bản, giữ phiên bản cũ cùng lịch sử người tạo/người duyệt.
5. Trong lúc bản mới chờ duyệt, khách tiếp tục xem bản đang xuất bản.

Tên trạng thái, quy trình từ chối, gửi duyệt lại, thu hồi hoặc khôi phục phiên bản chưa được quy định. Không tự mở rộng “duyệt tất cả” thành mọi giao dịch hoặc mọi thay đổi tài khoản.

### 7.3 Mua vé, dịch vụ, combo và hoàn tiền

1. Khách chọn vé vào bảo tàng, dịch vụ số riêng hoặc combo vé và dịch vụ. Quy tắc gói dịch vụ riêng cho tài khoản vẫn đến từ [Q21]; việc mua vé hộ/nhóm chưa được xác nhận trong phạm vi mới.
2. Thanh toán qua cổng trực tuyến; thất bại được thử lại và giữ tất cả lần giao dịch.
3. Hệ thống cần phân biệt quyền vào cửa và quyền dùng dịch vụ số, đồng thời quản lý hóa đơn có thông tin người mua và lịch sử thanh toán. Cách phát hành vé và kích hoạt quyền chưa chốt.
4. Có hỗ trợ hoàn tiền theo tỷ lệ phần trăm, theo quy tắc do chủ hệ thống đặt ra.
5. System Staff tiếp nhận/xử lý yêu cầu hoàn tiền và đối soát theo mô tả vai trò hiện tại.

Điều kiện hoàn, tỷ lệ, thời hạn, người phê duyệt cuối cùng và việc thu hồi quyền sau hoàn tiền đều chưa chốt. Với combo, cần bổ sung cách xử lý khi vé đã sử dụng nhưng dịch vụ chưa sử dụng, hoặc ngược lại, và có cho hoàn riêng từng phần không.

### 7.4 Làm quiz sau khi tham quan từng hiện vật

Ví dụ: khách nhận diện hiện vật A bằng QR và chọn làm quiz sau; tiếp tục chụp ảnh hiện vật B để AI nhận diện và cũng chọn làm sau. Khi mở mục quiz cuối ngày, khách thấy quiz liên quan A và B, không thấy quiz của hiện vật C chưa được quét/nhận diện. Việc tích lũy là tích lũy quiz có thể làm, không tự cộng điểm trả lời đúng khi chưa làm bài. [U2]

## 8. Yêu cầu chức năng chi tiết

### 8.1 Nhận diện, tìm kiếm và bản đồ

- Nhận diện hiện vật bằng quét QR hoặc nhận diện ảnh. [F]
- Chọn hiện vật từ bản đồ hoặc danh sách. [F]
- Tìm kiếm theo từ khóa, phòng/khu và chủ đề; duyệt hiện vật trưng bày theo phòng/chủ đề. [F]
- Lưu lịch sử nhận diện của khách. [Q12]
- Hiển thị vị trí hiện vật trên bản đồ; quản lý vị trí/icon. [Q12, QB]

**Định vị theo lần nhận diện:** khi quét QR hợp lệ hoặc AI nhận diện được hiện vật từ ảnh khách chụp, cập nhật vị trí khách trên bản đồ theo vị trí hiện vật đó. Đây là vị trí được xác định ở lần nhận diện gần nhất, không phải vị trí di chuyển trực tiếp. [U2]

**Chỉ đường trong bảo tàng:** phải có chỉ đường từ vị trí được xác định gần nhất đến hiện vật tiếp theo; không yêu cầu cập nhật vị trí liên tục như Google Maps. Cách biểu diễn đường đi, dữ liệu lối đi/cầu thang và xử lý hiện vật chưa có vị trí bản đồ chưa chốt. Không tự coi chức năng chỉ đường là chỉ hiển thị hai điểm rời rạc. [U2]

**Chỉ đường từ nhà đến bảo tàng:** là chức năng bắt buộc. Nguồn điểm xuất phát, phương tiện di chuyển, nhà cung cấp bản đồ và cách hiển thị trong ứng dụng hay mở ứng dụng bản đồ ngoài chưa chốt. Yêu cầu này không khôi phục chức năng tham quan online tại nhà đã bỏ. [U2]

### 8.2 Thuyết minh và nội dung đa ngôn ngữ

- Thuyết minh, giải thích hiện vật và trả lời câu hỏi tiếp nối bằng AI. [F]
- Hỗ trợ nhiều ngôn ngữ; tiếng Việt/tiếng Anh là ví dụ trong đề xuất, chưa phải danh sách chính thức. [F]
- Định hướng cho khách chọn độ dài và mức chi tiết của bài thuyết minh, không phân loại theo độ tuổi. Các mức cụ thể chưa chốt. [Q13]
- Có quản lý media và kịch bản thuyết minh. [F, QB]
- Giữ phiên bản, tác giả và người duyệt; tiếp tục phục vụ bản đang xuất bản khi bản mới chờ duyệt. [Q14]

Chưa rõ ảnh/video/audio gắn trực tiếp với hiện vật hay với từng bài/ngôn ngữ; nội dung có được tái sử dụng giữa hiện vật hay không; audio tạo sẵn, thu âm hay dùng TTS. [F] chỉ nêu TTS là tùy chọn.

### 8.3 Chatbot và kiến thức AI

- Quản lý tài liệu nguồn làm cơ sở cho AI. [Q15]
- Chatbot trực tiếp dựa vào thông tin đã được nhân viên duyệt, không cần duyệt trước từng câu trả lời. [Q16]
- Lưu hội thoại và từng tin nhắn để khách xem lại. [Q15]
- Một hội thoại có thể trao đổi về nhiều hiện vật. [Q15]
- Cần đánh giá độ chính xác của AI, thuyết minh, câu hỏi và nhận diện ảnh. [F]

[Q15] trả lời “cần quản lý” cho câu hỏi ghép về tài liệu nguồn và chỉ ra căn cứ câu trả lời. Do đó, quản lý nguồn đã được xác nhận; hình thức trích dẫn hiển thị cho khách cần xác nhận thêm. Chưa chốt dùng RAG, vector database, nhà cung cấp LLM, mô hình thị giác, lưu nguồn theo phiên bản hay cách xử lý khi thiếu kiến thức.

### 8.4 Tour và lượt tham quan

- Có tour mẫu do nhân viên tạo và tour cá nhân do AI gợi ý. [Q8]
- Một tour là danh sách hiện vật có thứ tự. [Q8]
- Tour AI được lưu để mở lại và chỉnh sửa. [Q9]
- Mỗi lần thực hiện tour có một lượt riêng, lưu bắt đầu, kết thúc và tiến độ. [Q9]
- Sở thích được chọn cho từng lần gợi ý, không phải sở thích lâu dài trong hồ sơ. [Q10]
- Thời gian có sẵn là thông tin từng lượt; không cần lưu các lựa chọn đầu vào để giải thích hoặc xem lại gợi ý. [Q10]
- [Q11] xác nhận quét QR ghi nhận đã tham quan và chỉ giữ lần gần nhất cho thông tin này. [U2] bổ sung cả QR và nhận diện ảnh để cập nhật vị trí/mở quiz; việc nhận diện ảnh có đồng thời hoàn thành điểm dừng trong tour hay không chưa được nói rõ, cần chốt khi thiết kế tiến độ. Không hạn chế mở quiz chỉ cho QR.
- Danh sách quiz tích lũy theo các hiện vật nhận diện trong ngày, không yêu cầu làm ngay tại từng điểm dừng. [U2]

Không được hiểu “chỉ giữ lần gần nhất” là xóa mọi lịch sử nhận diện, vì [Q12] yêu cầu lưu lịch sử này. Phạm vi lần gần nhất tính theo tài khoản, lượt hay tour vẫn cần chốt. Chưa rõ khách tự khám phá không chọn tour có tạo lượt hay không, điều kiện hoàn thành tour, việc sửa tour đang thực hiện và một tour có được đi qua nhiều bảo tàng hay không.

### 8.5 Quiz và kết quả

- AI tạo quiz trước; nhân viên duyệt vào ngân hàng câu hỏi. Không tạo câu hỏi chưa duyệt trực tiếp cho khách. [Q16]
- Mỗi quiz chỉ thuộc một hiện vật. Danh sách quiz tích lũy có thể chứa quiz của nhiều hiện vật; không làm thay đổi quy tắc một quiz/một hiện vật. [Q17, U2]
- Quét QR hoặc chụp ảnh được AI nhận diện thành công sẽ bổ sung quiz liên quan vào danh sách quiz trong ngày của khách. Chỉ mở trang từ bản đồ/danh sách không tự mở quiz. [U2]
- Sau khi khách đọc xong nội dung hiện vật, gợi ý làm quiz ngay hoặc để khi rảnh mới làm. Khách được tiếp tục tham quan mà không phải làm ngay. [U2]
- Quiz được cộng dồn trong ngày để khách có thể mở làm vào cuối ngày. Khi mở danh sách, chỉ hiển thị quiz liên quan đến hiện vật đã quét QR/nhận diện ảnh trong ngày đó. [U2]
- Làm sau vẫn được tích điểm trả lời đúng; việc thêm quiz vào danh sách không tự tạo kết quả bài làm hoặc điểm trả lời đúng. [Q18, U2]
- Một câu hỏi được dùng trong nhiều quiz. [Q17]
- Mỗi lần làm nên có 2–3 câu; sau khi trả lời xong có tùy chọn reset để nhận câu hỏi mới. [Q17]
- Có phản hồi tức thì cho đáp án. [F]
- Câu trả lời “lưu cả 2” được hiểu theo câu hỏi gốc là lưu từng câu trả lời của từng lần làm và tổng điểm; cần xác nhận tên gọi/cấu trúc kết quả khi thiết kế. [Q17]

Chưa quy định số lần làm tối đa, câu hỏi lặp, xử lý ngân hàng ít câu, loại câu hỏi, cách chọn câu hoặc ảnh hưởng của reset tới điểm. Cũng chưa chốt cách xác định khách đọc xong, mốc giờ/múi giờ kết thúc ngày, quiz chưa làm có giữ sang ngày sau không, cách xử lý quét lặp và quiz đã làm ngay trong danh sách cuối ngày. Yêu cầu “đến cuối ngày” không tự đồng nghĩa phải xóa quiz hoặc kết quả lúc nửa đêm.

### 8.6 Điểm và huy hiệu

- Điểm đến từ quét hiện vật và trả lời đúng câu hỏi. [Q18] Quiz làm ngay hoặc làm sau đều phục vụ tích điểm theo [U2]. Nhận diện ảnh đã được xác nhận mở quiz/cập nhật vị trí, nhưng điểm riêng cho hành động nhận diện ảnh chưa được chốt.
- Điểm tích lũy vào tài khoản, không chỉ trong lượt/tour. [Q18]
- Một huy hiệu chỉ nhận một lần. Ví dụ quét đủ 10 hiện vật để nhận một huy hiệu, không nhận lại huy hiệu đó. Đây là ví dụ, chưa phải danh mục huy hiệu đầy đủ. [Q18]

Chưa chốt mức điểm, điều kiện chống cộng điểm lặp khi quét/reset quiz, cách đếm hiện vật khác nhau, phạm vi huy hiệu theo bảo tàng hay toàn hệ thống, và sổ ghi nhận các hoạt động tạo điểm/huy hiệu.

### 8.7 Free, premium, gói và thanh toán

- Có truy cập cơ bản miễn phí và nội dung/dịch vụ premium trả phí. Museum Staff cấu hình free/premium. [F, QB]
- Ba hình thức bán đã xác nhận: **vé vào bảo tàng riêng**, **dịch vụ số riêng**, **combo vé và dịch vụ số**. [U1] thay thế phần “chỉ bán hướng dẫn số” ở [Q19].
- [Q19] từng quy định mua một tour tương ứng một ngày, không có thuê bao tháng/năm. Chưa có xác nhận thời hạn này áp dụng thế nào cho từng sản phẩm trong mô hình mới; không tự áp dụng một ngày cho mọi loại vé và combo.
- [Q20] có định hướng “một gói thì nên mở hết full dịch vụ”; phạm vi bảo tàng, tour và danh sách dịch vụ chưa chốt.
- Gói dịch vụ gắn riêng với tài khoản, không mua hộ theo [Q21]. Chưa xác nhận quy tắc mua hộ/nhóm đối với vé vào cửa mới bổ sung.
- Tích hợp cổng thanh toán online; VNPay, MoMo, ZaloPay, thẻ chỉ là ví dụ. [F]
- Lưu mọi lần giao dịch, kể cả thất bại rồi thử lại. [Q22]
- Hỗ trợ hoàn tiền theo phần trăm dựa trên rule hệ thống. [Q22]
- Hóa đơn có thông tin người mua và thông tin đầy đủ theo yêu cầu nguồn; chưa có trường cụ thể hay xác nhận là hóa đơn điện tử theo chuẩn pháp lý nào. [Q22]
- Quản lý pass, hóa đơn, lịch sử giao dịch, báo cáo doanh thu. [F]

**Cần xác nhận trước khi thiết kế quyền truy cập:** dịch vụ số mở một tour, một bảo tàng hay toàn hệ thống? “Một ngày” là ngày lịch hay 24 giờ? Hiệu lực từ thanh toán, kích hoạt hay ngày được chọn? Vé có ngày/khung giờ, loại khách, hạn mức số lượng hay lượt vào lại không? Ai kiểm vé và hệ thống tự phát hành vé hay tích hợp hệ thống bảo tàng? Combo kích hoạt hai thành phần cùng lúc hay riêng?

Combo được xác nhận là hình thức bán kết hợp; điều này chưa có nghĩa giỏ hàng tùy ý nhiều sản phẩm đã được chốt. Không tự thêm mua vé nhóm, mua hộ, chuyển nhượng hoặc gia hạn tự động.

### 8.8 Đánh giá, phản hồi và hỗ trợ

- Khách đánh giá thông tin được cung cấp, tour do hệ thống gợi ý, chatbot và trải nghiệm chung. [Q23]
- Chỉ được đánh giá sau khi hoàn thành tour. [Q23]
- Lưu phản hồi của nhân viên. [Q23]
- Museum Staff xem feedback của bảo tàng mình. [QB]
- System Staff hỗ trợ khách, xử lý khiếu nại và kiểm duyệt feedback không phù hợp. [QB]

Chưa chốt đánh giá gắn vào thực thể nào, một hay nhiều đánh giá mỗi lượt, thang điểm, quyền chỉnh sửa, trạng thái xử lý, điều kiện đối với người dùng miễn phí và việc đánh giá riêng từng hiện vật.

### 8.9 Thống kê và audit

- Admin có dashboard doanh thu; Museum Staff xem thống kê, doanh thu và feedback của bảo tàng mình. [F, QB]
- System Staff theo dõi lỗi vận hành và chất lượng AI trên toàn hệ thống. [QB]
- Cần lưu audit các thao tác quan trọng. [F]
- [Q24] yêu cầu lưu chi tiết nhất có thể nhưng chưa liệt kê báo cáo, sự kiện và thời hạn lưu.

Gợi ý cần thảo luận: số lượt tham quan, hiện vật được xem nhiều, hoàn thành tour, kết quả quiz, sử dụng AI, thanh toán/hoàn tiền, thay đổi nội dung và phân quyền. Đây chưa phải danh sách báo cáo hay audit bắt buộc đã được duyệt. Chưa quy định xử lý lịch sử khi tài khoản hoặc nội dung ngừng hoạt động.

## 9. Yêu cầu phi chức năng

Các yêu cầu sau đến từ [F] và cần chuyển thành tiêu chí đo lường khi viết SRS:

| Nhóm | Yêu cầu nền | Thông số chưa chốt |
|---|---|---|
| Dễ sử dụng | Giao diện mobile trực quan, phù hợp khách nhiều độ tuổi, thuận tiện tại chỗ | Phân chia chức năng mobile/web, mức hỗ trợ tiếp cận, tiêu chí usability |
| Tin cậy | Các chức năng hoạt động ổn định | Uptime, phục hồi lỗi, sao lưu |
| Hiệu năng | AI phản hồi nhanh khi tham quan | Độ trễ tối đa, tải đồng thời, dung lượng |
| Kết nối | Xử lý mạng yếu hoặc gián đoạn, ví dụ cache nội dung quan trọng | Dữ liệu offline, đồng bộ, chức năng bắt buộc online |
| Bảo mật và riêng tư | Bảo vệ dữ liệu khách, giới hạn quyền theo vai trò | Cơ chế đăng nhập, thời hạn lưu, quyền dữ liệu |
| Thanh toán | Dùng cổng uy tín, không lưu dữ liệu thẻ hoặc ngân hàng thô | Cổng thanh toán, môi trường demo/production |
| Độ chính xác | Thuyết minh và quiz đúng kiến thức, có kiểm duyệt và chỉ số chất lượng rõ | Bộ kiểm thử, chỉ số, ngưỡng đạt |

Gợi ý phân tích từ phạm vi nhiều bảo tàng: phải làm rõ phạm vi dữ liệu và quyền theo bảo tàng để Museum Staff chỉ thao tác đúng dữ liệu được giao. Chưa chốt kiến trúc multi-tenant, phân tách database hay mô hình triển khai.

## 10. Khái niệm dữ liệu để bàn giao cho người thiết kế

Đây là bản đồ khái niệm, **không phải ERD hay schema đã chốt**. Việc mỗi khái niệm trở thành một bảng, nhiều bảng hay thành phần của dữ liệu khác còn phụ thuộc thiết kế.

| Nhóm | Khái niệm cần xem xét | Quan hệ/quy tắc đáng chú ý |
|---|---|---|
| Tài khoản | Account, Role, Staff Assignment | Một role/tài khoản theo quy tắc hiện có; có phân công đối tượng |
| Không gian | Museum, Building, Floor, Gallery | Phân cấp bảo tàng → tòa nhà → tầng → phòng |
| Hiện vật | Artifact, Theme, QR, Reference Image, Map Position | Nhiều chủ đề; phòng không bắt buộc; không lưu lịch sử vị trí |
| Nội dung | Narration, Language, Media, Content Version, Review | Nhiều biến thể độ dài/chi tiết; giữ phiên bản và lịch sử duyệt |
| AI | Source Document, Conversation, Message, Recognition History | Kiến thức đã duyệt; hội thoại nhiều hiện vật; lưu lịch sử nhận diện |
| Tour | Tour, Ordered Tour Item, Visit, Progress | Tách kế hoạch tour khỏi lần thực hiện; tour được lưu/chỉnh sửa |
| Quiz | Quiz, Question, Quiz Question, Daily Quiz Eligibility, Attempt, Answer/Result | Quiz gắn hiện vật; danh sách đủ điều kiện trong ngày theo QR/ảnh; làm ngay hoặc sau; lưu kết quả từng lần/từng câu |
| Gamification | Account Points, Badge, Award | Điểm tài khoản; mỗi huy hiệu nhận một lần; sổ điểm chưa chốt |
| Thương mại | Admission Ticket, Service Package/Pass, Combo, Access Right, Order, Payment Attempt, Refund, Invoice | Ba hình thức bán đã chốt; phân biệt quyền vào cửa và dịch vụ; mọi lần thanh toán được giữ |
| Phản hồi | Feedback, Staff Response, Complaint | Đánh giá sau hoàn thành tour; lưu phản hồi nhân viên |
| Vận hành | Audit Event, Report, Error/AI Quality Record | Mức chi tiết và thời hạn lưu chưa chốt |
| Định vị và chỉ đường | Last Recognized Position, Indoor Route, Museum Destination | Vị trí cập nhật theo QR/ảnh; đường tới hiện vật tiếp theo; điểm đến bảo tàng cho chỉ đường từ nhà; mô hình lưu trữ chưa chốt |

Không trộn lẫn các khái niệm sau:

- Tour là kế hoạch; lượt tham quan là một lần thực hiện.
- Truy cập nội dung hiện vật không tự động là đã tham quan.
- Lần tham quan gần nhất và lịch sử nhận diện có yêu cầu lưu khác nhau.
- Bản nội dung đang xuất bản và bản đang sửa/chờ duyệt phải được phân biệt.
- Đơn mua, lần thử thanh toán, quyền sử dụng và hóa đơn có vai trò khác nhau dù mô hình lưu trữ chưa chốt.
- Vé vào cửa, quyền dịch vụ số và combo có ý nghĩa khác nhau. QR kiểm vé nếu có phải được phân biệt với QR hiện vật; cơ chế kiểm vé chưa chốt.
- Câu hỏi trong ngân hàng, tập câu hỏi được phục vụ và lần làm quiz cần phân biệt để lưu đúng kết quả.

## 11. Công nghệ đã chốt và lựa chọn còn mở

| Thành phần | Quyết định hiện tại |
|---|---|
| Backend API | **C# .NET API — đã chốt [U1]**; chưa chỉ định phiên bản |
| Mobile | **Flutter — đã chốt [U1]** |
| Web | **React + Vite — đã chốt [U1]**; web hiện là nền tảng được chọn, không còn chỉ là tùy chọn trong FALL26 |
| Database | **PostgreSQL — đã chốt [U1]** |
| AI | **Python — đã chốt [U1]**; FastAPI/Flask chưa được chọn |
| AI/NLP | LLM phục vụ thuyết minh/hỏi đáp theo yêu cầu nền; nhà cung cấp và mô hình chưa chốt |
| Nhận diện ảnh | Vision model, chưa chỉ định mô hình |
| Audio | Có thể dùng text-to-speech; phương án audio chưa chốt |
| Thanh toán | VNPay, MoMo, ZaloPay và thẻ là các ví dụ trong [F], chưa phải lựa chọn |

Các thành phần sản phẩm dự kiến: mobile Flutter, web React/Vite, backend API C# .NET, phần AI Python, PostgreSQL, module vé/dịch vụ/combo và thanh toán. Chưa chốt vai trò/chức năng cụ thể trên từng client, framework AI, cách giao tiếp .NET–Python, microservices, số dịch vụ, công nghệ bản đồ/chỉ đường, cloud, CI/CD hoặc phương thức triển khai. Các lựa chọn Spring Boot, React Native, Angular, SQL Server/MySQL trong [F] là phương án cũ, không còn là stack hiện tại.

Trong [F], tên “subscription and payment module” cần được hiểu theo phạm vi mới: quản lý vé, quyền dịch vụ số, combo và thanh toán; không mặc định có thuê bao định kỳ.

## 12. Tổ chức đồ án và sản phẩm bàn giao

### 12.1 Thông tin đăng ký

- Dự án trong ngữ cảnh đồ án SEP490, đề xuất FALL26 tại FPT University.
- Giảng viên hướng dẫn ghi trong biểu mẫu: **Phan Minh Tâm**.
- Các trường lớp, thời gian bắt đầu/kết thúc, chuyên ngành chi tiết và một số xác nhận trên biểu mẫu chưa được điền/chọn rõ.

| Thành viên | Mã sinh viên | Vai trò trong nhóm |
|---|---|---|
| Nguyễn Trung Nam | SE180495 | Leader |
| Nguyễn Đăng Khoa | SE182713 | Member |
| Lê Quốc Minh | SE182696 | Member |
| Ông Quốc Đạt | SE182678 | Member |
| Huỳnh Minh Tâm | SE182722 | Member |

Biểu mẫu gốc có số điện thoại và email. Bản bàn giao bối cảnh này không sao chép thông tin liên hệ cá nhân vì không cần để hiểu hoặc thiết kế hệ thống.

### 12.2 Tài liệu và sản phẩm phải chuẩn bị theo FALL26

- User Requirement.
- Software Requirement Specification.
- Architecture Design.
- Detail Design.
- System Implementation.
- Testing Document / kế hoạch kiểm thử.
- Installation Guide / hướng dẫn cài đặt.
- User manual / hướng dẫn sử dụng, được nhắc trong gói công việc.
- Content corpus và báo cáo đánh giá mô hình.
- Source code.
- Các gói phần mềm có thể triển khai.
- Ứng dụng mobile và web theo [U1], phần AI, module vé/dịch vụ/combo/thanh toán và API.

Áp dụng quy trình phát triển phần mềm và UML 2.0 để mô hình hóa. Cần áp dụng và đánh giá AI/NLP, nhận diện QR/ảnh. Các thành viên cần hiểu tài liệu tham khảo để giải thích khi bảo vệ.

### 12.3 Các gói công việc đề xuất

1. Xây dựng dịch vụ AI: chatbot thuyết minh, tạo câu hỏi, nhận diện hiện vật, đánh giá chất lượng.
2. Xây dựng backend: quản lý nội dung, theo dõi tour/lượt, gói/quyền truy cập, thanh toán, hóa đơn; bổ sung vé/combo theo [U1].
3. Xây dựng ứng dụng: nhận diện hiện vật, chatbot, quiz và bản đồ/hướng dẫn tham quan.
4. Build, triển khai và kiểm thử.
5. Chuẩn bị tài liệu phân tích, thiết kế, kiểm thử, cài đặt và hướng dẫn sử dụng.

Mỗi gói có thể có nhiều thành viên tham gia nhưng cần một người chịu trách nhiệm chính. Tài liệu chưa phân công tên cụ thể. Phạm vi hiện tại cần bao gồm chỉ đường từ nhà đến bảo tàng, định vị theo QR/ảnh, chỉ đường trong bảo tàng và quiz tích lũy trong ngày [U2].

## 14. Lưu ý khi dùng tài liệu này với AI hoặc người tiếp nhận

Người tiếp nhận có thể dùng tài liệu để phân tích nghiệp vụ, viết SRS, thiết kế ERD/API, lập backlog hoặc thảo luận kiến trúc. Khi tạo tài liệu tiếp theo, cần giữ các quyết định và điểm chưa chốt rõ ràng:

- Không đưa dự án trở lại phạm vi một bảo tàng. Triển lãm đã được chủ dự án tạm gác; không tự đưa trở lại phạm vi hiện tại.
- Không thay danh sách vai trò mới bằng mô hình 3 role cũ mà không có xác nhận.
- Giữ stack đã chốt ở mục 11; không biến các ví dụ còn mở về mô hình AI, ngôn ngữ, cổng thanh toán hoặc huy hiệu thành lựa chọn chính thức.
- Không mặc định Exhibit là triển lãm độc lập.
- Bán vé vào cửa, dịch vụ số và combo đã được xác nhận. Không tự thêm thuê bao tháng/năm hoặc mua hộ; phạm vi vé nhóm/mua hộ cần hỏi thêm.
- Tham quan online/virtual tour tại nhà và đề xuất gói online 7 ngày đã bị bỏ theo [U2]. Không đưa chúng vào yêu cầu hoặc mô hình dữ liệu hiện tại. Web React/Vite vẫn thuộc stack đã chốt.
- Có chỉ đường từ nhà đến bảo tàng; bên trong bảo tàng cập nhật vị trí theo QR/ảnh và chỉ đường tới hiện vật tiếp theo, không yêu cầu theo dõi liên tục.
- QR và nhận diện ảnh thành công đều cập nhật vị trí và mở quiz trong ngày. Mở trang đơn thuần không mở quiz. Không tự suy ra nhận diện ảnh hoàn thành điểm dừng hoặc được cộng điểm nhận diện khi các quy tắc đó chưa chốt.
- Gợi ý quiz sau khi đọc xong; cho làm ngay hoặc sau. Danh sách cuối ngày chỉ chứa quiz của hiện vật đã quét QR/nhận diện ảnh; không tự yêu cầu hoàn thành tour trước khi được làm quiz.
- Không yêu cầu duyệt trước từng tin nhắn chatbot; bước duyệt áp dụng cho nguồn/nội dung chuẩn bị sẵn.
- Không dùng câu hỏi AI chưa duyệt làm quiz chính thức phục vụ khách.
- Không diễn giải “lưu chi tiết nhất có thể” thành quyền ghi đè các ngoại lệ: không lưu lịch sử vị trí, không cần giữ lựa chọn đầu vào gợi ý, và chỉ lưu lần đã tham quan gần nhất trong phạm vi còn cần chốt.
- Nếu đề xuất phương án cho phần còn thiếu, ghi rõ là đề xuất và tác động của nó; cập nhật phiên bản tài liệu sau khi chủ dự án xác nhận.

## 15. Đối chiếu độ bao phủ nguồn

| Nhóm nguồn | Nội dung đã đưa vào |
|---|---|
| Q1–Q5 | Phạm vi nhiều bảo tàng, thuật ngữ, cấu trúc không gian, triển lãm, chủ đề |
| Q6–Q8 | Đăng nhập, vai trò/phân công, tour mẫu và tour AI |
| Q9–Q12 | Lưu tour, lượt tham quan, sở thích/thời gian, QR, lịch sử nhận diện, bản đồ |
| Q13–Q16 | Biến thể thuyết minh, duyệt/phiên bản, nguồn AI, hội thoại, quiz tạo trước |
| Q17–Q18 | Lần làm quiz, reset, câu hỏi dùng lại, điểm và huy hiệu |
| Q19–Q22 | Quyền hướng dẫn số, gói/ngày, không mua hộ, giao dịch, hoàn tiền, hóa đơn |
| Q23–Q24 | Đánh giá sau tour, phản hồi nhân viên, báo cáo/audit còn cần cụ thể hóa |
| QB | Năm vai trò cập nhật, trách nhiệm từng nhóm nhân sự; phần online/virtual tour lịch sử đã bị [U2] loại bỏ |
| F | Bối cảnh, toàn bộ nhóm chức năng nền, phi chức năng, công nghệ gợi ý, tổ chức và sản phẩm đồ án |
| U1 | Vé/dịch vụ/combo bán riêng hoặc kết hợp; tạm gác triển lãm; stack đã chốt; đề xuất online sau đó bị [U2] loại bỏ |
| U2 | Bỏ tham quan online; định vị theo QR/ảnh; chỉ đường trong bảo tàng không theo dõi liên tục; chỉ đường từ nhà; quiz làm ngay hoặc tích lũy trong ngày |

## 16. Lịch sử cập nhật

| Phiên bản | Ngày | Nội dung |
|---|---|---|
| 1.0 | 12/10/2026 | Tổng hợp FALL26 và Question, phân biệt yêu cầu xác nhận với điểm còn mở |
| 1.1 | 12/10/2026 | Cập nhật xác nhận trực tiếp về vé/dịch vụ/combo, gác triển lãm và stack; thêm đề xuất gói online 7 ngày, chưa phê duyệt |
| 1.2 | 15/10/2026 | Bỏ tham quan online/virtual tour và gói online; cập nhật định vị QR/ảnh, chỉ đường trong/ngoài bảo tàng và quiz tích lũy trong ngày theo quyết định của nhóm với giảng viên |
