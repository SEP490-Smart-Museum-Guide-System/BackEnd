# Tích hợp FE cho vé vào cửa và đơn mua chung

Các đường dẫn bên dưới có tiền tố `/api/commerce`. Danh mục vé và danh mục gói số đang mở bán là công khai. Các thao tác còn lại dùng Bearer token và đọc response ở `data`.

## Danh mục và thanh toán

| Thao tác | API |
|---|---|
| Danh mục vé | `GET /museums/{museumId}/admission-offers` |
| Danh mục gói số | `GET /museums/{museumId}/products` |
| Danh mục vé cho quản lý | `GET /museums/{museumId}/admission-offers/manage` |
| Tạo hoặc sửa cấu hình vé | `POST /museums/{museumId}/admission-offers` hoặc `PUT /museums/{museumId}/admission-offers/{id}` |
| Mua vé hoặc mua cả hai | `POST /museums/{museumId}/admission-orders` |
| Mua riêng gói số | `POST /orders` |
| Thanh toán ưu tiên ví | `POST /orders/{orderId}/payments/sepay` |
| Xem trạng thái giao dịch | `GET /orders/{orderId}/transactions` |

Cấu hình vé nhận `name`, `price` (VND mỗi người) và `isActive`. Khi đặt vé, `productId` là ID trong danh mục vé. Bỏ `digitalProductId` khi chỉ mua vé.

```json
{
  "productId": 31,
  "digitalProductId": 12,
  "visitDate": "2026-12-10",
  "quantity": 5,
  "buyerName": "Nguyen Van A",
  "buyerDetails": {}
}
```

Dùng `data.id` làm mã đơn để thanh toán. `amount` của đơn là tổng tiền. `amount` của response SePay là số còn phải chuyển sau khi dùng ví. Với đơn mua chung, `productKind` là `admission_and_digital`. `seats` của đơn mua chung là số chỗ của gói số. Số lượt vào cửa lấy từ vé.

## QR và check-in

- `GET /orders/{orderId}/admission-ticket` trả vé của người mua sau khi thanh toán. FE dựng QR từ đúng chuỗi `data.code`. Response còn có `visitDate`, `museumId`, `quantity`, `used`, `remaining` và `canCheckIn`.
- `POST /museums/{museumId}/admission/inspect` nhận `{ "code": "..." }` và trả tình trạng vé mà chưa trừ lượt.
- `POST /museums/{museumId}/admission/check-in` nhận `code`, `quantity` và `operationId` (UUID do FE tạo cho mỗi lần xác nhận). Khi gửi lại cùng thao tác, giữ nguyên UUID và số người. Response có thông tin lần xác nhận và số lượt còn lại trong `data.ticket.remaining`.

## Hủy và hoàn tiền

- `GET /orders/{orderId}/items` trả `ticketAmount`, `digitalAmount`, thời điểm hủy và số tiền hoàn của từng phần.
- `POST /orders/{orderId}/items/ticket/cancel` hoặc `POST /orders/{orderId}/items/digital/cancel` hủy từng món trong đơn mua chung đã thanh toán. Vé từng check-in sẽ bị từ chối.
- `POST /orders/{orderId}/cancel` hủy đơn đang chờ thanh toán hoặc đơn mua riêng. Đơn đang chờ được hoàn lại phần đã trừ từ ví.
- Mức hoàn cho đơn đã thanh toán là 100% khi hủy trước ít nhất 3 ngày, 50% khi trước đúng 2 ngày và 0% từ 1 ngày trở xuống. Với đơn mua chung, giá dùng để tính là giá của món đang hủy. Tiền hoàn ghi vào ví.

Backend lưu vé và phần tiền vé ở `admission_tickets`, từng lần xác nhận ở `admission_check_ins` và mốc hủy gói số ở `purchase_orders.DigitalCancelledAt`.
