# NGHIỆP VỤ VÀ HƯỚNG DẪN QUẢN LÝ LỊCH – STAFFHUB

## 1. Mục đích

Module lịch làm việc của CafeChain quản lý **lịch dự kiến**, không phải chấm công và không phải phiên két POS. Hệ thống tách rõ ba nghiệp vụ:

| Khái niệm | Model/Bảng | Ý nghĩa |
| --- | --- | --- |
| Mẫu ca | `Shift` / `Shifts` | Khung giờ dùng lại tại một cửa hàng, ví dụ Ca sáng 06:00–12:00. |
| Lịch nhân viên | `StaffShift` / `StaffShifts` | Nhân viên được dự kiến làm mẫu ca nào vào ngày nào. |
| Phiên vận hành POS | `WorkShift` / `WorkShifts` | Phiên mở/đóng két thực tế, gắn với đơn hàng và đối soát tiền. |

`StaffShift` không lưu giờ vào/ra thực tế, không tính lương và không thay thế `WorkShift`.

## 2. Quyền và phạm vi cửa hàng

| Permission | Chức năng |
| --- | --- |
| `Shift.View` | Mở màn hình và xem lịch trong cửa hàng được cấp phạm vi. |
| `Shift.Create` | Tạo mẫu ca và phân lịch cho nhân viên. |
| `Shift.Update` | Sửa/ngưng mẫu ca và sửa lịch đang hiệu lực. |
| `Shift.Cancel` | Hủy lịch, yêu cầu nhập lý do. |

Permission quyết định người dùng được thực hiện thao tác nào; `StaffScope` quyết định cửa hàng nào được thao tác. Có permission nhưng gửi `targetStoreId`, `StaffId`, `ShiftId` hoặc `StaffShiftId` ngoài phạm vi vẫn bị backend từ chối.

Nhân viên, mẫu ca và lịch phải thuộc cùng cửa hàng. Cửa hàng chính của nhân viên là nơi lịch được quản lý.

## 3. Quản lý mẫu ca

### 3.1 Tạo mẫu ca

1. Chọn cửa hàng trong danh sách được cấp quyền.
2. Mở **Thư viện mẫu ca**.
3. Chọn **Tạo mẫu ca**.
4. Nhập tên, giờ bắt đầu, giờ kết thúc và ghi chú.
5. Chọn **Lưu mẫu ca**.

Nếu giờ kết thúc nhỏ hơn hoặc bằng giờ bắt đầu, hệ thống coi đây là ca qua đêm. Ví dụ `22:00–06:00` kết thúc lúc 06:00 ngày kế tiếp.

### 3.2 Sửa và ngưng mẫu ca

- Chọn **Sửa** để đổi tên, giờ hoặc ghi chú.
- Khi thay đổi giờ, backend kiểm tra lại các lịch `SCHEDULED` đang dùng giờ mặc định. Thay đổi bị từ chối nếu làm một nhân viên có hai lịch giao nhau.
- Chọn **Ngưng** thay vì xóa. Mẫu đã ngưng vẫn được giữ cho lịch sử nhưng không xuất hiện trong dropdown và không thể kéo để phân lịch mới.
- Chọn **Kích hoạt** để cho phép sử dụng lại mẫu.

## 4. Phân lịch cho nhân viên

Màn hình dùng ma trận một tuần: nhân viên theo hàng và ngày theo cột. Một nhân viên có thể có nhiều ca trong cùng ngày nếu các khoảng giờ không giao nhau.

### 4.1 Phân lịch bằng dropdown

1. Tìm hàng của nhân viên và ngày cần phân.
2. Chọn **Phân ca** trong ô ngày.
3. Chọn mẫu ca trong dropdown.
4. Nếu cần giờ khác mẫu, bật **Dùng giờ riêng** và nhập đủ giờ bắt đầu/kết thúc.
5. Chọn **Lưu lịch**.

Khi không bật giờ riêng, hai giá trị tùy chỉnh được gửi là `null`; lịch tiếp tục theo giờ mẫu và sẽ phản ánh thay đổi hợp lệ của mẫu ca sau này.

### 4.2 Phân lịch bằng kéo thả

1. Mở **Thư viện mẫu ca**.
2. Kéo một mẫu đang hoạt động.
3. Thả vào ô nhân viên/ngày mong muốn.
4. Hệ thống mở modal và điền sẵn nhân viên, ngày, mẫu ca.
5. Kiểm tra thông tin, tùy chọn giờ riêng nếu cần, sau đó chọn **Lưu lịch**.

Thao tác thả không tự ghi dữ liệu. Người dùng luôn phải xác nhận trong modal. Chỉ mẫu ca được kéo; lịch đã phân không được kéo sang ngày hoặc nhân viên khác. Trên điện thoại/máy tính bảng, sử dụng cách dropdown.

## 5. Sửa, hủy và khôi phục lịch

### Sửa lịch

- Chỉ lịch `SCHEDULED` được sửa.
- Chọn **Sửa** trên thẻ lịch, thay mẫu ca hoặc giờ riêng rồi lưu.
- Backend kiểm tra lại cửa hàng, trạng thái, giao nhau và `RowVersion`.

### Hủy lịch

- Chọn **Hủy** và nhập lý do bắt buộc.
- Bản ghi chuyển sang `CANCELLED`, không bị xóa.
- Lịch hủy hiển thị mờ, không tham gia kiểm tra giao nhau và không được tính là ca đang hiệu lực.

### Khôi phục

Nếu phân lại đúng nhân viên, mẫu ca và ngày của một bản ghi đã hủy, hệ thống khôi phục bản ghi cũ về `SCHEDULED` và cập nhật giờ riêng thay vì tạo dòng trùng.

## 6. Quy tắc kiểm tra thời gian

- Hệ thống quy đổi lịch thành khoảng `DateTime` tuyệt đối.
- Khi phân hoặc sửa, lịch của ngày trước, ngày hiện tại và ngày sau đều được kiểm tra để phát hiện giao nhau qua nửa đêm.
- Hai lịch liền kề được phép, ví dụ `06:00–10:00` và `10:00–14:00`.
- Hai lịch có bất kỳ phần thời gian giao nhau đều bị từ chối.
- Chỉ trạng thái `SCHEDULED` tham gia kiểm tra.
- Nếu dữ liệu đã được người khác cập nhật, server trả xung đột `409`; giao diện thông báo và tải lại trang để lấy `RowVersion` mới.

## 7. Sử dụng StaffHub mới

StaffHub lấy đúng `StaffId` từ phiên đăng nhập và hiển thị lịch của chính nhân viên.

### Xem lịch

- Trang hiển thị bảy ngày trong tuần và đánh dấu ngày hiện tại.
- Mỗi ngày có thể có nhiều ca.
- Giờ hiển thị ưu tiên giờ riêng; nếu không có thì dùng giờ mẫu.
- Ca qua đêm có nhãn `+1 ngày`.
- Lịch đã hủy vẫn xuất hiện với trạng thái riêng để nhân viên biết thay đổi.
- Dùng **Tuần trước** và **Tuần sau** để chuyển khoảng thời gian.

### Mở POS

- Nút **Mở POS** chỉ xuất hiện khi tài khoản có permission ứng dụng `App.POS`.
- Không được xếp lịch vẫn có thể mở POS; lịch là kế hoạch vận hành, không phải điều kiện chấm công.
- Nếu có lịch `SCHEDULED` đang phủ thời điểm hiện tại và mở phiên muộn theo quy tắc POS, hệ thống có thể yêu cầu OTP phê duyệt.
- StaffHub không có Face ID, check-in, check-out hoặc bộ đếm giờ làm.

### Mật khẩu nhân viên

StaffHub không khóa màn hình để bắt buộc đổi mật khẩu lần đầu. Sau khi đăng nhập thành công, nhân viên được sử dụng lịch và mở POS theo permission; chức năng đổi mật khẩu chủ động vẫn được giữ tại trang hồ sơ.

## 8. Xử lý lỗi thường gặp

| Hiện tượng | Nguyên nhân thường gặp | Cách xử lý |
| --- | --- | --- |
| Không thấy nút Phân ca | Thiếu `Shift.Create`. | Nhờ người quản trị kiểm tra effective permission. |
| Không kéo được mẫu | Thiếu quyền tạo, mẫu đã ngưng hoặc thiết bị cảm ứng. | Dùng dropdown; kiểm tra trạng thái mẫu và permission. |
| Báo trùng lịch | Khoảng giờ giao với lịch `SCHEDULED`, kể cả ca từ ngày trước qua đêm. | Điều chỉnh mẫu/ngày hoặc dùng giờ riêng hợp lệ. |
| Không sửa được lịch | Lịch đã hủy, ngoài phạm vi hoặc dữ liệu đã thay đổi. | Kiểm tra trạng thái/scope và tải lại trang. |
| Không thấy cửa hàng | Cửa hàng không nằm trong `StaffScope` của tài khoản. | Cập nhật phạm vi tại màn hình phân quyền. |
| Không thấy nút POS | Tài khoản không có `App.POS`. | Cấp đúng permission ứng dụng; không phụ thuộc lịch. |

## 9. Nguyên tắc dữ liệu

- Không hard delete mẫu ca hoặc lịch đã phát sinh.
- Không sửa trực tiếp trạng thái/bảng bằng giao diện ngoài workflow.
- Mọi mutation có antiforgery, permission backend, StoreScope và chống thao tác lặp.
- Hủy và thay đổi lịch được ghi `AuditLog` với người thao tác và dữ liệu trước/sau.
- Giao diện chỉ hỗ trợ thao tác; service và repository vẫn là authority cuối cùng của nghiệp vụ.
