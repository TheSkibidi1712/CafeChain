# Phân tích luồng nghiệp vụ người dùng StaffHub

Nguồn quy tắc chuẩn: [STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md](./STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md).

## 1. Ranh giới nghiệp vụ

StaffHub là cổng vào POS. `Shift` và `StaffShift` chỉ là lịch dự kiến; `WorkShift` là phiên trách nhiệm POS/két. Mở POS không phải chấm công và không tạo `StaffShift` giả.

Thẻ lịch không có phân công hiển thị đúng câu: **“Chưa có lịch — Thời gian nghỉ hoặc chưa được phân ca.”**

`AppLauncher` và `AdminDashboardApp` giữ hành vi điều hướng/permission hiện hành; chúng không tự đánh giá hoặc mở WorkShift.

## 2. Luồng chung

1. Nhân viên đăng nhập StaffHub; backend resolve Account → Staff → store scope → permission.
2. StaffHub tải terminal active của store. Người dùng chọn terminal trước khi preview.
3. Preview kiểm tra terminal, lịch nguồn, WorkShift active của staff và terminal.
4. StaffHub hiển thị quyết định, kể cả `WITHIN_SCHEDULE`.
5. Nếu cần, người dùng nhập lý do và thực hiện OTP tại StaffHub.
6. StaffHub phát exchange code một lần 60 giây và chuyển trong URL fragment.
7. POS xóa fragment, exchange lấy token/context, chỉ nhập tiền đầu phiên rồi gọi open.
8. Backend revalidate và mở WorkShift trong transaction.

## 3. Các tình huống mở

### 3.1 Đúng lịch

- Preview: `WITHIN_SCHEDULE`.
- Không yêu cầu lý do/OTP.
- Terminal trống, staff không có active WorkShift.
- Sau xác nhận, StaffHub phát exchange; POS nhập tiền đầu phiên và mở bình thường.

### 3.2 Mở trễ

- Preview: `LATE_FOR_SCHEDULE`.
- Trễ trên 15 phút: lý do 10–500 ký tự.
- Trễ trên 30 phút: thêm OTP.
- Lý do/OTP nằm ở StaffHub, không xuất hiện trong React POS.

### 3.3 Ngoài lịch khi cửa hàng chưa có phiên

- Preview: `OUTSIDE_SCHEDULE`.
- Cần `POS.WorkShift.OpenOutsideSchedule`, lý do và OTP.
- WorkShift mới có `AutoCloseAtUtc = StartTimeUtc + 6 giờ`.
- Không tạo StaffShift, không sao chép tiền từ phiên trước.

### 3.4 Ngoài lịch khi cửa hàng đã có phiên khác

A đang `OPEN` trên Terminal 1. B không có lịch và chọn Terminal 2:

- B vẫn được `OUTSIDE_SCHEDULE` nếu B chưa có active WorkShift và Terminal 2 trống.
- Sau lý do + OTP, B có WorkShift riêng.
- WorkShift A giữ nguyên; không chuyển order, payment hoặc tiền két.

Nếu B chọn Terminal 1, preview trả `TERMINAL_ALREADY_HAS_OPEN_SHIFT`, không trả `WORKSHIFT_EXPIRED`.

## 4. Phiên cũ của chính nhân viên

### 4.1 `OPEN`

Trả `STAFF_ALREADY_HAS_OPEN_SHIFT` và hiển thị phiên/terminal/bắt đầu/hạn cùng nút “Tiếp tục POS”.

### 4.2 `CLOSING`

Trả `WORKSHIFT_PENDING_CLOSE` và nút “Hoàn tất đóng ca”.

### 4.3 `EXPIRED_PENDING_CLOSE`

Trả `WORKSHIFT_PENDING_CLOSE` và nút “Kiểm đếm và đóng”. Không trả `WORKSHIFT_EXPIRED`.

### 4.4 `CLOSED`

Không khóa. Mở ngoài lịch lần nữa cần RequestKey mới, lý do mới, OTP mới, tiền đầu phiên mới và hạn sáu giờ mới. Không mở lại bản ghi cũ.

### 4.5 `RECONCILIATION_REQUIRED`

Không thuộc active responsibility. Có thể mở phiên mới nếu staff và terminal không có active WorkShift khác. Phiên cũ và dữ liệu đồng bộ muộn vẫn giữ `WorkShiftId` cũ để đối soát.

## 5. Terminal chưa đăng ký

1. Người dùng mở hộp “Đăng ký terminal” trong StaffHub.
2. StaffHub sinh terminal ID và RequestKey, người dùng nhập tên.
3. Gửi/xác nhận/gửi lại OTP; approver phải đúng scope và có `POS.WorkShift.OverrideTerminal`.
4. Backend consume approval rồi tạo terminal active đúng store.
5. Trang làm mới danh sách. POS không có form hay endpoint đăng ký terminal.

Lỗi terminal gồm `TERMINAL_NOT_FOUND`, `TERMINAL_INACTIVE`, `TERMINAL_STORE_MISMATCH`.

## 6. Double-click và request đồng thời

- Hai lần bấm cùng RequestKey/payload chỉ tạo một WorkShift và replay trả kết quả cũ.
- Cùng key khác payload trả `DUPLICATE_REQUEST`.
- Hai request khác key cùng thắng precheck bị unique index/transaction phân giải; request sau trả lỗi active phù hợp hoặc `CONCURRENCY_CONFLICT`.
- Worker chuyển phiên cũ sang `EXPIRED_PENDING_CLOSE` đồng thời với open: request mở bị `WORKSHIFT_PENDING_CLOSE`; không tạo phiên thứ hai.

## 7. Exchange lỗi

| Tình huống | Error code | UX |
|---|---|---|
| Quá 60 giây | `POS_EXCHANGE_CODE_EXPIRED` | Quay lại StaffHub |
| Đã dùng | `POS_EXCHANGE_CODE_ALREADY_USED` | Không exchange lần hai |
| Không tồn tại/sai purpose/context | `POS_EXCHANGE_CODE_INVALID` | Quay lại StaffHub |
| Mở POS trực tiếp/token cũ | `POS_OPEN_CONTEXT_REQUIRED` | Điều hướng StaffHub |

Không trường hợp exchange nào dùng `WORKSHIFT_EXPIRED`.

## 8. Màn hình phiên bị khóa

StaffHub hiển thị mã phiên, terminal, thời gian bắt đầu, trạng thái, thời gian hết hạn và hành động tiếp tục/hoàn tất đóng/kiểm đếm. Terminal do người khác giữ không cho resume phiên đó.

## 9. Đóng và hết hạn

Worker chỉ tự đóng phiên hoàn toàn rỗng theo nghiệp vụ hiện hành. Phiên có tiền, order, payment, offline manifest hoặc dữ liệu cần kiểm đếm chuyển `EXPIRED_PENDING_CLOSE`. `WORKSHIFT_EXPIRED` chỉ áp dụng khi người dùng thao tác trực tiếp trên phiên hết hạn bằng hành động không còn được phép.

## 10. Điều hướng và permission

- `AppLauncher` luôn dùng route hiện hành sau khi người dùng vào được StaffHub.
- Nút Dashboard chỉ hiển thị khi policy `AdminDashboardApp` thành công; backend vẫn kiểm tra authorization.
- Mở POS cần `App.POS` và `POS.WorkShift.Open`.
- Mở ngoài lịch cần `POS.WorkShift.OpenOutsideSchedule`.
- Approver mở trễ/ngoài lịch cần permission duyệt đúng store scope.
- Đăng ký terminal cần `POS.WorkShift.OverrideTerminal`.

## 11. Checklist nghiệm thu

- `WITHIN_SCHEDULE`, `LATE_FOR_SCHEDULE`, `OUTSIDE_SCHEDULE`.
- Hai nhân viên trên hai terminal độc lập.
- Cả ba trạng thái active của staff/terminal.
- `CLOSED` và `RECONCILIATION_REQUIRED` không khóa.
- Double-click, replay, payload mismatch và race.
- Exchange hash-only, TTL, one-time, context/purpose binding.
- Không tạo StaffShift, không copy tiền và ngoài lịch đúng sáu giờ.
