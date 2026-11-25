# Tài liệu Bàn giao API cho Frontend (Mobile Flutter & Web React)
> **Cập nhật:** 25/11/2026 — Lưu lịch sử hội thoại Chatbot (Q15, QB) & Bộ API Dashboard Thống kê (Admin, System Staff, Museum Staff, Curator).

Tất cả các endpoint đều trả về chung cấu trúc wrapper `ResponseDto<T>`:
```json
{
  "statusCode": 200,
  "message": "Thành công",
  "data": { ... },
  "errors": null
}
```
Header xác thực bắt buộc: `Authorization: Bearer <access_token>`

---

## PHẦN 1: LỊCH SỬ HỘI THOẠI CHATBOT (Dành cho Mobile Flutter & Web Portal)

### 1.1. Hỏi đáp Chatbot tại hiện vật (Cập nhật tương thích ngược)
- **Method & Endpoint:** `POST /api/artifacts/{artifactId}/guide/messages`
- **Role:** `Visitor`
- **Điểm mới:**
  - Mọi lượt hỏi đáp thành công đều được **Backend tự động lưu tạm vào Database trong vòng 24 giờ** (`HistoryRetentionHours = 24`, khớp với thời hạn 1 ngày tham quan của vé). Các tin nhắn quá 24 giờ sẽ được hệ thống tự động dọn dẹp (auto-purge TTL).
  - Thêm cờ tùy chọn `"useSavedHistory": true` trong request body. Nếu Mobile truyền `"history": []` kèm `"useSavedHistory": true`, Backend sẽ tự động lấy 4 cặp hỏi-đáp gần nhất (trong vòng 24 giờ) từ DB của khách tại hiện vật đó làm ngữ cảnh mà Mobile không cần tự giữ mảng `history` ở client.
- **Request Body:**
```json
{
  "question": "Hiện vật này tìm thấy ở đâu?",
  "history": [],
  "useSavedHistory": true
}
```
- **Response (`200 OK`):** Giữ nguyên cấu trúc cũ (`artifactId`, `answer`, `supported`, `sources`).

---

### 1.2. Lấy lịch sử chat của khách tại một hiện vật
- **Method & Endpoint:** `GET /api/artifacts/{artifactId}/guide/messages?limit=50`
- **Role:** `Visitor`
- **Mô tả:** Dùng khi khách mở lại màn hình Chatbot của hiện vật `{artifactId}` để hiển thị lại các tin nhắn đã hỏi trước đó (sắp xếp theo thứ tự thời gian từ cũ đến mới).
- **Response `data` (`200 OK`):**
```json
[
  {
    "id": 101,
    "artifactId": 17,
    "artifactName": "Trống đồng Ngọc Lũ",
    "museumId": 1,
    "museumName": "Bảo tàng Lịch sử Quốc gia",
    "question": "Trống đồng có hoa văn gì?",
    "answer": "Mặt trống có ngôi sao 14 cánh ở chính giữa và hình chim Lạc bay ngược chiều kim đồng hồ.",
    "supported": true,
    "sources": [
      {
        "contentVersionId": 91,
        "versionNo": 2,
        "title": "Trống đồng Ngọc Lũ — Bảo vật Quốc gia",
        "languageCode": "vi"
      }
    ],
    "createdAt": "2026-11-25T02:30:00Z"
  }
]
```

---

### 1.3. Xóa lịch sử chat tại một hiện vật ("Cuộc trò chuyện mới")
- **Method & Endpoint:** `DELETE /api/artifacts/{artifactId}/guide/messages`
- **Role:** `Visitor`
- **Mô tả:** Xóa toàn bộ lịch sử hỏi đáp đã lưu của khách tại hiện vật `{artifactId}`.
- **Response `data` (`200 OK`):** Số lượng lượt hỏi đáp đã xóa (kiểu `int`, ví dụ `2`).

---

### 1.4. Danh sách các cuộc hội thoại theo hiện vật của khách
- **Method & Endpoint:** `GET /api/guide/conversations/me?museumId=1&page=1&pageSize=20`
- **Role:** `Visitor`
- **Query Params:**
  - `museumId` *(tùy chọn)*: Lọc theo bảo tàng.
  - `page` *(mặc định `1`)*, `pageSize` *(mặc định `20`, tối đa `100`)*.
- **Mô tả:** Hiển thị màn hình "Lịch sử trò chuyện AI" của khách, gom nhóm theo từng hiện vật đã hỏi, sắp xếp theo thời điểm hỏi gần nhất.
- **Response `data` (`200 OK`):**
```json
{
  "items": [
    {
      "artifactId": 17,
      "artifactName": "Trống đồng Ngọc Lũ",
      "museumId": 1,
      "museumName": "Bảo tàng Lịch sử Quốc gia",
      "exchangeCount": 4,
      "lastQuestion": "Nó tìm thấy ở đâu?",
      "lastAnswer": "Tìm thấy tại xã Như Trác, huyện Lý Nhân, tỉnh Hà Nam.",
      "lastSupported": true,
      "lastAskedAt": "2026-11-25T02:35:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

---

### 1.5. Dòng thời gian toàn bộ tin nhắn hỏi đáp xuyên suốt nhiều hiện vật
- **Method & Endpoint:** `GET /api/guide/messages/me?museumId=1&artifactId=17&page=1&pageSize=20`
- **Role:** `Visitor`
- **Mô tả:** Xem toàn bộ các lượt hỏi đáp của khách dưới dạng dòng thời gian tổng hợp qua nhiều hiện vật (mới nhất lên đầu).
- **Response `data` (`200 OK`):** `PaginatedResponseDto<GuideChatMessageDto>` (mỗi phần tử trong `items` giống mục **1.2**).

---

### 1.6. Nhật ký Hỏi đáp AI để giám sát chất lượng (Dành cho Web Portal)
- **Method & Endpoint:** `GET /api/guide/logs?museumId=1&artifactId=17&supported=false&page=1&pageSize=20`
- **Role:** `Admin`, `Staff` (System Staff), `Museum Staff` *(chỉ xem được bảo tàng/hiện vật mình phụ trách)*.
- **Query Params:**
  - `museumId` *(tùy chọn)*, `artifactId` *(tùy chọn)*
  - `supported` *(tùy chọn: `true` hoặc `false`)* — Truyền `supported=false` để lọc ra các câu hỏi của khách mà tài liệu hiện vật chưa có thông tin trả lời, giúp nhân viên bảo tàng bổ sung nội dung thuyết minh.
- **Response `data` (`200 OK`):**
```json
{
  "items": [
    {
      "id": 105,
      "userId": "44444444-4444-4444-4444-444444444444",
      "artifactId": 20,
      "artifactName": "Tượng Phật Quan Âm",
      "museumId": 2,
      "museumName": "Bảo tàng Mỹ thuật",
      "question": "Bức tượng này nặng bao nhiêu kg?",
      "answer": "Nội dung đã xuất bản của hiện vật chưa có thông tin để trả lời câu hỏi này. Bạn có thể hỏi thêm nhân viên bảo tàng.",
      "supported": false,
      "languageCode": "vi",
      "contentVersionNo": null,
      "createdAt": "2026-11-25T02:40:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1,
  "totalPages": 1,
  "hasPreviousPage": false,
  "hasNextPage": false
}
```

---

## PHẦN 2: BỘ API DASHBOARD THỐNG KÊ (Dành cho Web React)

Cả 3 API Dashboard đều hỗ trợ query lọc theo thời gian tùy chọn:
- `?from=2026-11-01T00:00:00Z&to=2026-11-30T23:59:59Z` (nếu không truyền sẽ thống kê toàn thời gian).

---

### 2.1. Dashboard Quản trị Hệ thống (`Admin`)
- **Method & Endpoint:** `GET /api/dashboard/admin`
- **Role:** `Admin`
- **Response `data` (`200 OK`):**
```json
{
  "overview": {
    "totalMuseums": 2,
    "activeMuseums": 2,
    "totalArtifacts": 15,
    "activeArtifacts": 14,
    "artifactsWith3DModel": 5,
    "premiumArtifacts": 6,
    "totalUsers": 120,
    "activeUsers": 118,
    "usersByRole": [
      { "role": "Admin", "userCount": 1 },
      { "role": "Curator", "userCount": 2 },
      { "role": "Museum Staff", "userCount": 4 },
      { "role": "Staff", "userCount": 2 },
      { "role": "Visitor", "userCount": 111 }
    ]
  },
  "commerce": {
    "totalPaidOrders": 45,
    "totalPendingOrders": 3,
    "totalCancelledOrders": 2,
    "activePassesCount": 18,
    "revenueByCurrency": [
      {
        "currency": "VND",
        "paidOrders": 45,
        "grossRevenue": 6750000,
        "refundedAmount": 150000,
        "netRevenue": 6600000,
        "failedAttempts": 4
      }
    ],
    "salesByProductKind": [
      { "productKind": "admission", "currency": "VND", "paidOrders": 15, "totalSeats": 25, "grossRevenue": 1500000 },
      { "productKind": "combo", "currency": "VND", "paidOrders": 20, "totalSeats": 35, "grossRevenue": 4250000 },
      { "productKind": "digital", "currency": "VND", "paidOrders": 10, "totalSeats": 10, "grossRevenue": 1000000 }
    ],
    "topMuseumsByRevenue": [
      {
        "museumId": 1,
        "museumName": "Bảo tàng Lịch sử Quốc gia",
        "currency": "VND",
        "paidOrders": 30,
        "grossRevenue": 4500000,
        "refundedAmount": 0,
        "netRevenue": 4500000
      }
    ]
  },
  "engagement": {
    "totalVisits": 80,
    "completedVisits": 62,
    "inProgressVisits": 18,
    "guidedVisits": 55,
    "totalRecognitions": 310,
    "qrRecognitions": 210,
    "imageRecognitions": 100,
    "totalQuizAttempts": 140,
    "completedQuizAttempts": 125,
    "averageQuizScore": 84.5,
    "totalChatbotQuestions": 95,
    "supportedChatbotAnswers": 82,
    "unsupportedChatbotAnswers": 13,
    "chatbotSupportRatePercent": 86.32
  },
  "supportAndFeedback": {
    "totalFeedbacks": 40,
    "averageOverallRating": 4.65,
    "averageContentRating": 4.7,
    "averageTourRating": 4.5,
    "averageChatbotRating": 4.6,
    "totalComplaints": 8,
    "complaintsByStatus": [
      { "status": "in_progress", "count": 2 },
      { "status": "new", "count": 1 },
      { "status": "resolved", "count": 5 }
    ]
  }
}
```

---

### 2.2. Dashboard Nhân viên Hệ thống (`System Staff`)
- **Method & Endpoint:** `GET /api/dashboard/staff`
- **Role:** `Staff`, `Admin`
- **Response `data` (`200 OK`):**
```json
{
  "complaints": {
    "totalComplaints": 8,
    "newComplaints": 1,
    "inProgressComplaints": 2,
    "resolvedComplaints": 5,
    "unassignedComplaints": 1,
    "recentPendingComplaints": [
      {
        "id": 12,
        "visitorId": "44444444-4444-4444-4444-444444444444",
        "subject": "Không tải được âm thanh hiện vật 18",
        "status": "new",
        "assignedTo": null,
        "createdAt": "2026-11-25T02:10:00Z"
      }
    ]
  },
  "feedbackModeration": {
    "totalFeedbacks": 40,
    "visibleFeedbacks": 38,
    "hiddenFeedbacks": 2,
    "lowRatedFeedbacksCount": 3,
    "averageOverallRating": 4.65
  },
  "paymentReconciliation": {
    "paidOrdersCount": 45,
    "cancelledOrRefundedOrdersCount": 2,
    "failedPaymentAttemptsCount": 4,
    "refundsByCurrency": [
      { "currency": "VND", "amount": 150000, "count": 1 }
    ],
    "recentFailedPayments": [
      {
        "attemptId": "99999999-9999-9999-9999-999999999999",
        "orderId": "88888888-8888-8888-8888-888888888888",
        "museumId": 1,
        "productName": "Combo Tham quan & Hướng dẫn số",
        "amount": 150000,
        "currency": "VND",
        "failureReason": "Giao dịch quá hạn thanh toán",
        "createdAt": "2026-11-25T01:45:00Z"
      }
    ]
  },
  "aiAndRecognitionQuality": {
    "totalChatbotQuestions": 95,
    "supportedChatbotAnswers": 82,
    "unsupportedChatbotAnswers": 13,
    "chatbotSupportRatePercent": 86.32,
    "recentUnsupportedQuestions": [
      {
        "id": 105,
        "userId": "44444444-4444-4444-4444-444444444444",
        "artifactId": 20,
        "artifactName": "Tượng Phật Quan Âm",
        "museumId": 2,
        "museumName": "Bảo tàng Mỹ thuật",
        "question": "Bức tượng này nặng bao nhiêu kg?",
        "answer": "Nội dung đã xuất bản của hiện vật chưa có thông tin để trả lời câu hỏi này...",
        "supported": false,
        "languageCode": "vi",
        "contentVersionNo": null,
        "createdAt": "2026-11-25T02:40:00Z"
      }
    ],
    "totalRecognitions": 310,
    "qrRecognitions": 210,
    "imageRecognitions": 100,
    "successfulRecognitions": 295,
    "failedRecognitions": 15,
    "recognitionSuccessRatePercent": 95.16
  }
}
```

---

### 2.3. Dashboard Bảo tàng (`Museum Staff` / `Curator` / `Admin`)
- **Method & Endpoint:** `GET /api/dashboard/museums/{museumId}`
- **Role:** `Museum Staff`, `Curator`, `Admin` *(Nhân viên chỉ truy cập được bảo tàng được phân công, nếu khác bảo tàng trả `403 Forbidden`)*.
- **Response `data` (`200 OK`):**
```json
{
  "museumId": 1,
  "museumName": "Bảo tàng Lịch sử Quốc gia",
  "isActive": true,
  "catalog": {
    "buildingsCount": 1,
    "floorsCount": 2,
    "galleriesCount": 4,
    "publishedIndoorMapsCount": 2,
    "totalArtifacts": 10,
    "activeArtifacts": 10,
    "artifactsWith3DModel": 4,
    "freeArtifacts": 6,
    "premiumArtifacts": 4
  },
  "curationPipeline": {
    "pendingArtifactContentsCount": 2,
    "publishedArtifactContentsCount": 12,
    "rejectedArtifactContentsCount": 1,
    "draftTourVersionsCount": 1,
    "submittedTourVersionsCount": 1,
    "approvedTourVersionsCount": 3,
    "rejectedTourVersionsCount": 0,
    "draftQuestionsCount": 4,
    "approvedQuestionsCount": 30,
    "rejectedQuestionsCount": 2,
    "publishedQuizzesCount": 8
  },
  "visitsAndEngagement": {
    "totalVisits": 50,
    "completedVisits": 40,
    "inProgressVisits": 10,
    "guidedVisits": 38,
    "uniqueVisitors": 42,
    "completionRatePercent": 80.0,
    "totalRecognitions": 200,
    "totalChatbotQuestions": 65,
    "supportedChatbotAnswers": 58,
    "unsupportedChatbotAnswers": 7,
    "topScannedArtifacts": [
      { "artifactId": 17, "artifactName": "Trống đồng Ngọc Lũ", "visits": 52 },
      { "artifactId": 18, "artifactName": "Thạp đồng Đào Thịnh", "visits": 41 }
    ],
    "topChattedArtifacts": [
      { "artifactId": 17, "artifactName": "Trống đồng Ngọc Lũ", "visits": 28 }
    ]
  },
  "commerce": {
    "totalPaidOrders": 30,
    "activePassesCount": 12,
    "revenueByCurrency": [
      {
        "currency": "VND",
        "paidOrders": 30,
        "grossRevenue": 4500000,
        "refundedAmount": 0,
        "netRevenue": 4500000,
        "failedAttempts": 2
      }
    ],
    "salesByProductKind": [
      { "productKind": "combo", "currency": "VND", "paidOrders": 20, "totalSeats": 35, "grossRevenue": 3500000 },
      { "productKind": "admission", "currency": "VND", "paidOrders": 10, "totalSeats": 15, "grossRevenue": 1000000 }
    ]
  },
  "feedback": {
    "totalFeedbacks": 25,
    "unrespondedFeedbacksCount": 4,
    "averageOverallRating": 4.72,
    "averageContentRating": 4.8,
    "averageTourRating": 4.6,
    "averageChatbotRating": 4.7
  }
}
```
