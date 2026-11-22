# 📘 Tài liệu Tích hợp API & Real-time SignalR: Notification (SMGS)

Tài liệu này hướng dẫn Frontend (Web Dashboard & Mobile App) kết nối hệ thống Thông báo (In-App Notification Center & Real-time WebSockets).

---

## 1. Xác thực (Authentication)

Tất cả các REST API và SignalR Hub của Notification đều yêu cầu đăng nhập (áp dụng cho mọi Role: `Visitor`, `Museum Staff`, `Curator`, `Staff`, `Admin`).
* **REST API Header**:
  ```http
  Authorization: Bearer <access_token>
  ```
* **SignalR Hub**: Truyền `access_token` qua `accessTokenFactory` (tự động đính kèm vào query `?access_token=...` hoặc Header `Authorization`).

---

## 2. Danh sách REST API (`/api/notifications`)

### 2.1. Lấy danh sách thông báo của người dùng hiện tại
* **Endpoint**: `GET /api/notifications`
* **Query Parameters** *(Tất cả đều không bắt buộc)*:
  | Tham số | Kiểu | Mặc định | Mô tả |
  | :--- | :--- | :--- | :--- |
  | `unreadOnly` | `boolean` | `null` | Truyền `true` nếu chỉ muốn lọc các thông báo chưa đọc. |
  | `type` | `string` | `null` | Lọc theo mã loại thông báo (xem bảng mã ở Mục 4). |
  | `page` | `int` | `1` | Số trang hiện tại (`>= 1`). |
  | `pageSize` | `int` | `20` | Số lượng thông báo mỗi trang (`1 - 100`). |

* **Response (`200 OK`)**:
  ```json
  {
    "isSuccess": true,
    "statusCode": 200,
    "message": "Thành công.",
    "data": {
      "items": [
        {
          "id": 15,
          "recipientId": "b5a1e9c0-1234-4567-89ab-cdef01234567",
          "museumId": 1,
          "type": "PAYMENT_SUCCEEDED",
          "title": "Thanh toán thành công: Vé tham quan Bảo tàng",
          "body": "Đơn hàng \"Vé tham quan Bảo tàng\" (50,000 VND) cho ngày tham quan 22/11/2026 đã thanh toán thành công. Vé / Pass của bạn đã sẵn sàng!",
          "referenceType": "PurchaseOrder",
          "referenceId": "3f8b2d1a-9876-4321-ba98-76543210fedc",
          "actionUrl": "/commerce/orders/3f8b2d1a-9876-4321-ba98-76543210fedc/invoice",
          "isRead": false,
          "readAt": null,
          "createdAt": "2026-11-22T06:20:00Z"
        }
      ],
      "page": 1,
      "pageSize": 20,
      "totalCount": 1,
      "totalPages": 1
    }
  }
  ```

---

### 2.2. Lấy số lượng thông báo chưa đọc (Hiển thị Badge trên Icon Chuông 🔔)
* **Endpoint**: `GET /api/notifications/unread-count`
* **Response (`200 OK`)**:
  ```json
  {
    "isSuccess": true,
    "statusCode": 200,
    "message": "Thành công.",
    "data": {
      "unreadCount": 3
    }
  }
  ```

---

### 2.3. Đánh dấu 1 thông báo là Đã đọc
* **Endpoint**: `PATCH /api/notifications/{id}/read`
* **Path Parameter**: `id` (`long`) — ID của thông báo.
* **Hành vi**: Cập nhật `isRead = true`, `readAt = UtcNow`, đồng thời phát sự kiện SignalR `UnreadCountChanged` để cập nhật lại badge trên mọi tab của user.
* **Response (`200 OK`)**:
  ```json
  {
    "isSuccess": true,
    "statusCode": 200,
    "message": "Đã đánh dấu đã đọc.",
    "data": {
      "id": 15,
      "recipientId": "b5a1e9c0-1234-4567-89ab-cdef01234567",
      "museumId": 1,
      "type": "PAYMENT_SUCCEEDED",
      "title": "Thanh toán thành công: Vé tham quan Bảo tàng",
      "body": "...",
      "referenceType": "PurchaseOrder",
      "referenceId": "3f8b2d1a-9876-4321-ba98-76543210fedc",
      "actionUrl": "/commerce/orders/3f8b2d1a-9876-4321-ba98-76543210fedc/invoice",
      "isRead": true,
      "readAt": "2026-11-22T06:25:10Z",
      "createdAt": "2026-11-22T06:20:00Z"
    }
  }
  ```

---

### 2.4. Đánh dấu tất cả thông báo là Đã đọc
* **Endpoint**: `PATCH /api/notifications/read-all`
* **Hành vi**: Đánh dấu toàn bộ thông báo chưa đọc của user hiện tại thành `isRead = true`, đồng thời bắn SignalR `UnreadCountChanged` về `0`.
* **Response (`200 OK`)**:
  ```json
  {
    "isSuccess": true,
    "statusCode": 200,
    "message": "Đã đánh dấu đã đọc 3 thông báo.",
    "data": 3
  }
  ```

---

### 2.5. Xóa 1 thông báo
* **Endpoint**: `DELETE /api/notifications/{id}`
* **Path Parameter**: `id` (`long`) — ID của thông báo.
* **Response (`200 OK`)**:
  ```json
  {
    "isSuccess": true,
    "statusCode": 200,
    "message": "Đã xóa thông báo.",
    "data": null
  }
  ```

---

## 3. Kết nối Real-time với SignalR (`/hubs/notifications`)

* **Hub URL**: `<BASE_API_URL>/hubs/notifications`
* Khi kết nối thành công với JWT hợp lệ, Backend tự động đưa kết nối vào phòng riêng của user (`user:{userId}`).

### 3.1. Các sự kiện (Server $\rightarrow$ Client Events)

| Tên Event | Payload nhận được | Mô tả & Cách xử lý trên FE |
| :--- | :--- | :--- |
| **`ReceiveNotification`** | Object `NotificationResponseDto` (giống 1 item ở mục 2.1) | Bắn xuống ngay khi có thông báo mới. FE hiển thị Toast/Popup góc màn hình và chèn (`prepend`) thông báo mới vào đầu danh sách. |
| **`UnreadCountChanged`** | `{ "unreadCount": number }` | Bắn xuống khi có thông báo mới, hoặc khi user đánh dấu đã đọc / xóa thông báo. FE cập nhật ngay số hiển thị trên badge icon chuông 🔔. |

### 3.2. Code mẫu kết nối trên Web (React / Vue / Next.js / TypeScript)

Cài đặt thư viện chính thức:
```bash
npm install @microsoft/signalr
```

Khởi tạo kết nối:
```typescript
import * as signalR from "@microsoft/signalr";

export function createNotificationHubConnection(baseUrl: string, getAccessToken: () => string) {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl(`${baseUrl}/hubs/notifications`, {
      accessTokenFactory: () => getAccessToken(),
    })
    .withAutomaticReconnect()
    .build();

  // 1. Khi có thông báo mới nổ về
  connection.on("ReceiveNotification", (notification) => {
    console.log("🔔 Thông báo mới:", notification);
    // TODO: Hiển thị Toast(notification.title, notification.body)
    // TODO: Thêm vào đầu danh sách notifications trong state
  });

  // 2. Khi số lượng chưa đọc thay đổi
  connection.on("UnreadCountChanged", (payload: { unreadCount: number }) => {
    console.log("🔴 Số chưa đọc:", payload.unreadCount);
    // TODO: setUnreadCount(payload.unreadCount)
  });

  return connection;
}
```

---

## 4. Bảng mã Loại thông báo (`type`) & Điều hướng Deep-Link

Khi người dùng bấm vào một thông báo trên giao diện:
1. Gọi `PATCH /api/notifications/{id}/read` (nếu `isRead === false`).
2. Dựa vào `type`, `referenceType`, `referenceId` hoặc `actionUrl` để điều hướng tới đúng màn hình chi tiết:

| Mã `type` | Đối tượng nhận | Sự kiện kích hoạt | `referenceType` | `referenceId` | Gợi ý điều hướng (`actionUrl`) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`CONTENT_SUBMITTED`** | `Curator` (Web) | `Museum Staff` / `Admin` gửi bản thuyết minh hiện vật mới chờ duyệt | `ArtifactContent` | `{versionId}` | `/artifact-contents/{versionId}` |
| **`CONTENT_APPROVED`** | `Museum Staff` (Web) | `Curator` phê duyệt (`Approve`) bản thuyết minh hiện vật | `ArtifactContent` | `{versionId}` | `/artifacts/{artifactId}/contents/versions` |
| **`CONTENT_REJECTED`** | `Museum Staff` (Web) | `Curator` từ chối (`Reject`) bản thuyết minh (kèm lý do trong `body`) | `ArtifactContent` | `{versionId}` | `/artifacts/{artifactId}/contents/versions` |
| **`PAYMENT_SUCCEEDED`** | `Visitor` (App/Web) | Thanh toán đơn hàng thành công (SePay Webhook / Ví / Xác nhận) | `PurchaseOrder` | `{orderId}` | `/commerce/orders/{orderId}/invoice` *(hoặc mở trang Vé/Pass)* |
| **`FEEDBACK_RESPONDED`** | `Visitor` (App/Web) | `Museum Staff` trả lời đánh giá chuyến tham quan của khách | `VisitFeedback` | `{visitId}` | `/feedbacks/visit/{visitId}` |
| **`COMPLAINT_CREATED`** | `Staff`, `Admin` (Web) | `Visitor` gửi một khiếu nại / yêu cầu hỗ trợ mới | `Complaint` | `{complaintId}` | `/complaints/{complaintId}` |
| **`COMPLAINT_STATUS_UPDATED`** | `Visitor` (App/Web) | `Staff` cập nhật trạng thái xử lý khiếu nại (`InProgress`, `Resolved`, `Closed`) | `Complaint` | `{complaintId}` | `/complaints/me/{complaintId}` |
