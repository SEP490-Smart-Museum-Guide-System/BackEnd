# Thiết kế vé vào cửa bảo tàng

## Mua và xuất trình vé

Khách có thể mua riêng vé, mua riêng gói số hoặc mua cả hai của cùng một bảo tàng trong một lần thanh toán. Vé và gói số trong cùng giỏ dùng chung ngày khách chọn. Giá vé bằng đơn giá nhân số người. Tổng tiền thanh toán bằng giá vé cộng giá gói số.

Đơn dùng luồng ưu tiên ví hiện tại. Nếu ví chỉ trả được một phần, SePay thu phần còn lại bằng một QR thanh toán. Sau khi xác nhận đủ tiền, backend cấp một QR vào cửa cho cả nhóm và cấp quyền dịch vụ số theo quy tắc hiện tại.

## Hủy từng món sau thanh toán

Đơn mua chung có hai dòng, mỗi dòng lưu giá tại thời điểm mua, trạng thái, thời điểm hủy và số tiền hoàn. Khách có thể chọn hủy dòng vé hoặc dòng gói số. Hủy vé vô hiệu hóa QR vé. Hủy gói số khóa quyền truy cập và mã chia sẻ của gói. Món còn lại tiếp tục dùng theo lịch đã đặt.

Đề xuất áp dụng chính sách hoàn tiền hiện tại lên giá của đúng món bị hủy và ghi tiền hoàn vào ví. Ví dụ, nếu vé giá 250.000 đ và gói số giá 100.000 đ, khi hủy vé với mức hoàn 50%, ví nhận 125.000 đ và gói số vẫn dùng được. Vé đã có lượt check-in không được hủy. Với gói số, áp dụng điều kiện hủy hiện tại của gói.

Đơn đang chờ thanh toán dùng thao tác hủy cả đơn để giải phóng phần ví đã trừ. Khách có thể tạo đơn mới với món muốn giữ. Sau thanh toán, backend khóa dòng cần hủy khi cập nhật trạng thái và ghi giao dịch hoàn tiền để cùng một dòng không được hoàn hai lần.

## Quét mã và xác nhận vào cửa

Nhân viên được phân công tại bảo tàng quét mã để xem ngày tham quan, số người đã mua, số lượt đã dùng và số lượt còn lại. Backend kiểm tra vé đã thanh toán, đơn chưa hủy, vé thuộc đúng bảo tàng và ngày hiện tại theo giờ Việt Nam trùng ngày trên vé.

Thao tác quét chỉ đọc thông tin. Nhân viên nhập số người thực tế và xác nhận. Ví dụ, vé 5 người được xác nhận cho 3 người thì cùng mã QR còn 2 lượt. Backend kiểm tra và trừ lượt trong cùng giao dịch để hai máy quét không dùng trùng lượt. Yêu cầu xác nhận gửi lại với cùng mã thao tác trả lại kết quả đã ghi nhận.

## Dữ liệu

| Đối tượng | Dữ liệu chính |
|---|---|
| Cấu hình vé | Bảo tàng, tên vé, giá mỗi người, tiền tệ, trạng thái mở bán |
| Đơn hàng | Người mua, bảo tàng, ngày tham quan, tổng tiền và giao dịch thanh toán chung |
| Dòng đơn hàng | Loại vé hoặc gói số, cấu hình đã mua, số lượng, giá tại thời điểm mua, trạng thái hủy và số tiền hoàn |
| Vé đã cấp | Đơn hàng, mã QR khó đoán, tổng lượt và số lượt đã dùng |
| Lần check-in | Vé, số người vào, nhân viên, thời điểm xác nhận và mã thao tác |

Mã QR chỉ mang mã tra cứu. Backend lấy thông tin bảo tàng, ngày và số lượt từ dữ liệu vé. Việc thanh toán lại hoặc nhận lại xác nhận thanh toán không cấp thêm vé cho cùng đơn.

## Phương án đã chọn

Lưu lịch sử từng lần check-in để nhân viên có thể đối chiếu lúc nào và ai đã xác nhận. Phương án chỉ giữ tổng số lượt đã dùng cho biết số lượt còn lại nhưng thiếu thông tin đối chiếu. Vé vào cửa có dữ liệu riêng và dùng chung quy trình thanh toán với dịch vụ số.
