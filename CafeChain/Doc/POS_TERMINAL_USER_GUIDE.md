# Hướng dẫn Terminal POS

## 1. Terminal POS là gì?

Terminal POS là định danh của một thiết bị hoặc một quầy bán hàng cụ thể trong cửa hàng. Ví dụ:

- `Quầy chính` dành cho máy bán hàng tại quầy thu ngân.
- `Quầy mang đi` dành cho thiết bị xử lý đơn mang đi.
- `Máy POS 02` dành cho thiết bị bán hàng thứ hai.

Terminal không phải là nhân viên, `Shift`, `StaffShift` hay `WorkShift`:

- `Shift` là mẫu giờ dự kiến của cửa hàng.
- `StaffShift` là lịch dự kiến đã phân cho nhân viên.
- `WorkShift` là phiên chịu trách nhiệm POS/két của một nhân viên.
- Terminal là thiết bị/quầy mà WorkShift đang sử dụng.

## 2. Terminal dùng để làm gì?

Terminal giúp backend xác định chính xác quầy nào đang chịu trách nhiệm cho một phiên POS. Terminal được dùng để:

- Gắn WorkShift với đúng thiết bị hoặc quầy bán hàng.
- Ngăn hai nhân viên cùng mở phiên trên một quầy tại cùng thời điểm.
- Gắn order, payment, dữ liệu offline và thông tin két với đúng WorkShift.
- Gửi cập nhật SignalR tới đúng cửa hàng, nhân viên và terminal liên quan.
- Hỗ trợ audit, điều tra sai lệch và đối soát khi có giao dịch đồng bộ muộn.
- Bảo đảm nhân viên không tự chọn terminal thuộc cửa hàng khác.

Terminal chỉ là định danh trách nhiệm thiết bị. Terminal không tự tạo lịch làm việc, không chấm công và không dùng để tính lương.

## 3. Có bắt buộc phải dùng Terminal không?

**Có. Nhân viên bắt buộc phải chọn một terminal active trước khi mở POS.**

Tuy nhiên, không phải lần nào cũng cần đăng ký terminal mới:

- Nếu cửa hàng đã có terminal phù hợp và đang trống, chỉ cần chọn terminal đó.
- Chỉ đăng ký terminal mới khi cửa hàng có thiết bị/quầy mới chưa tồn tại trong danh sách.
- Không tạo terminal mới để né lỗi terminal đang được sử dụng.

Backend không cho mở WorkShift khi terminal không tồn tại, bị vô hiệu hóa, thuộc cửa hàng khác hoặc đang có phiên active.

## 4. Khi nào Terminal được xem là đang sử dụng?

Một terminal đang bị khóa trách nhiệm nếu có WorkShift thuộc một trong các trạng thái:

- `OPEN`: phiên đang bán hàng.
- `CLOSING`: phiên đang chốt két.
- `EXPIRED_PENDING_CLOSE`: phiên đã hết thời lượng nhưng chưa kiểm đếm và đóng.

Nếu chọn terminal đang bị khóa, hệ thống trả:

```text
TERMINAL_ALREADY_HAS_OPEN_SHIFT
```

Người dùng phải chọn terminal khác hoặc chờ phiên cũ được xử lý. Không được gắn nhân viên mới vào WorkShift của người khác, tự chiếm terminal hoặc chuyển đơn/tiền giữa hai WorkShift.

`CLOSED` và `RECONCILIATION_REQUIRED` không thuộc nhóm active đang khóa terminal. Dữ liệu của phiên cũ vẫn giữ nguyên `WorkShiftId` để đối soát riêng.

## 5. Quy trình chọn Terminal khi mở POS

1. Đăng nhập StaffHub.
2. Tại phần `Terminal POS`, chọn terminal phù hợp với quầy đang sử dụng.
3. Bấm `Mở POS`.
4. Backend kiểm tra terminal tồn tại, active, đúng cửa hàng và chưa có WorkShift active.
5. StaffHub tiếp tục đánh giá lịch, lý do và OTP nếu cần.
6. Sau khi được xác nhận, StaffHub phát exchange code và chuyển sang POS.
7. POS chỉ yêu cầu nhập tiền đầu phiên.

Không thể bỏ qua bước chọn terminal bằng cách sửa `TerminalId`, `StoreId` hoặc URL trên trình duyệt; backend luôn kiểm tra lại bằng identity và dữ liệu server.

## 6. Đăng ký Terminal mới

Đăng ký terminal được thực hiện tại StaffHub:

1. Bấm `Đăng ký terminal`.
2. Nhập tên terminal dễ nhận biết.
3. Bấm `Gửi OTP`.
4. Người có quyền `POS.WorkShift.OverrideTerminal` và đúng store scope phê duyệt OTP.
5. Nhập OTP và bấm `Xác nhận và đăng ký`.
6. Sau khi thành công, trang tải lại và terminal xuất hiện trong danh sách.

OTP đăng ký terminal có thời hạn, cooldown gửi lại, dùng một lần và bind với requester, approver, store, terminal cùng RequestKey.

## 7. Cách đặt tên Terminal

Tên nên phản ánh vị trí hoặc mục đích của thiết bị:

- Nên dùng: `Quầy chính`, `Quầy mang đi`, `Tầng 2 - POS 01`.
- Không nên dùng: `Máy mới`, `Test`, `ABC` hoặc tên trùng nhau khó phân biệt.

Không đưa mật khẩu, OTP, tên khách hàng hoặc dữ liệu nhạy cảm vào tên terminal.

## 8. Terminal bị vô hiệu hóa hoặc sai cửa hàng

- `TERMINAL_NOT_FOUND`: terminal chưa được đăng ký.
- `TERMINAL_INACTIVE`: terminal đã bị vô hiệu hóa hoặc cửa hàng không còn active.
- `TERMINAL_STORE_MISMATCH`: terminal thuộc cửa hàng khác.
- `TERMINAL_ALREADY_HAS_OPEN_SHIFT`: terminal đang có WorkShift active.

Không tự đăng ký lại cùng thiết bị ở cửa hàng khác để bỏ qua lỗi. Người có quyền quản lý phải kiểm tra đúng terminal, store scope và phiên đang giữ trách nhiệm.

## 9. Lưu ý bảo mật và vận hành

- Không dùng chung một terminal cho hai quầy đang bán đồng thời.
- Không chia sẻ OTP đăng ký terminal cho người không có trách nhiệm.
- Không sửa hoặc xóa định danh terminal trong trình duyệt để né kiểm soát.
- Trước khi chuyển thiết bị cho nhân viên khác, phải hoàn tất đóng/kiểm đếm WorkShift cũ.
- Order và payment đồng bộ muộn luôn giữ WorkShift cũ; terminal mới không nhận lại doanh thu của phiên trước.
- Khi thiết bị hỏng hoặc thay thế, quản lý cần vô hiệu hóa terminal cũ theo quy trình quản trị thay vì tạo nhiều terminal không kiểm soát.

## 10. Tóm tắt

```text
Chọn terminal active và đang trống: bắt buộc để mở POS.
Đăng ký terminal mới: chỉ cần khi chưa có terminal phù hợp.
Một terminal: tối đa một WorkShift active responsibility.
Terminal: định danh thiết bị/quầy, không phải lịch làm việc hay chấm công.
```
