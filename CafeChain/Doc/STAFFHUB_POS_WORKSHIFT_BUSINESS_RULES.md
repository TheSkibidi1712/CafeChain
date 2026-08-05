# Nghiệp vụ chuẩn StaffHub và phiên POS WorkShift

> Xem tình huống giao diện và kết quả tại [STAFFHUB_USER_BUSINESS_FLOWS.md](./STAFFHUB_USER_BUSINESS_FLOWS.md).
>
> Xem giải thích dành cho người dùng tại [POS_TERMINAL_USER_GUIDE.md](./POS_TERMINAL_USER_GUIDE.md).

## 1. Phạm vi

- `Shift`: mẫu giờ dự kiến của cửa hàng.
- `StaffShift`: lịch dự kiến đã phân cho nhân viên.
- `WorkShift`: phiên chịu trách nhiệm POS/két, giao dịch và đối soát.
- Mở POS không tạo chấm công, giờ công, tăng ca, tính lương hoặc `StaffShift` giả.
- StaffHub là nơi duy nhất chọn/đăng ký terminal, đánh giá lịch, yêu cầu lý do/OTP và phát mã sang POS. POS chỉ đổi mã và nhập tiền đầu phiên.

## 2. Luồng mở POS chuẩn

```text
StaffHub
→ xác thực Account/Staff/store scope/permission
→ chọn terminal active thuộc store
→ preview StaffShift và WorkShift đang khóa
→ WITHIN_SCHEDULE / LATE_FOR_SCHEDULE / OUTSIDE_SCHEDULE
→ xác nhận, lý do và OTP nếu cần
→ phát exchange code một lần
→ chuyển code bằng URL fragment
→ POS xóa fragment và exchange
→ POS nhập tiền đầu phiên
→ backend mở WorkShift trong transaction
```

Truy cập trực tiếp endpoint mở phiên mà không có exchange context hợp lệ trả `POS_OPEN_CONTEXT_REQUIRED`. Backend không tin `StaffId`, `StoreId`, `TerminalId`, thời gian hoặc `OpenContext` từ React.

## 3. Lịch và OpenContext

Backend tìm lịch hiệu lực trong đúng cửa hàng, hỗ trợ ca qua đêm bằng khoảng thời gian tuyệt đối.

- `WITHIN_SCHEDULE`: luôn hiển thị xác nhận; không cần OTP.
- `LATE_FOR_SCHEDULE`: trễ trên 15 phút cần lý do; trên 30 phút cần thêm OTP theo cấu hình hiện hành.
- `OUTSIDE_SCHEDULE`: cần quyền `POS.WorkShift.OpenOutsideSchedule`, lý do 10–500 ký tự và OTP.
- Mở ngoài lịch không tạo `StaffShift`; `AutoCloseAtUtc = StartTimeUtc + 6 giờ`.
- Một nhân viên ngoài lịch được mở WorkShift riêng trên terminal trống dù cửa hàng đang có WorkShift khác. Không gắn vào phiên người khác, không chuyển đơn/tiền và không sao chép tiền cuối phiên.

## 4. Trạng thái trách nhiệm active

Định nghĩa dùng chung `WorkShiftStatuses.ActiveResponsibility` gồm:

- `OPEN`
- `CLOSING`
- `EXPIRED_PENDING_CLOSE`

`CLOSED` và `RECONCILIATION_REQUIRED` không khóa mở phiên mới. Phiên cũ vẫn được xử lý/đối soát riêng; order hoặc payment đồng bộ muộn giữ nguyên `WorkShiftId` cũ.

Phân loại khi mở mới:

| Xung đột | Error code | Kết quả |
|---|---|---|
| Staff có `OPEN` | `STAFF_ALREADY_HAS_OPEN_SHIFT` | Tiếp tục hoặc đóng phiên cũ |
| Staff có `CLOSING` | `WORKSHIFT_PENDING_CLOSE` | Hoàn tất chốt két |
| Staff có `EXPIRED_PENDING_CLOSE` | `WORKSHIFT_PENDING_CLOSE` | Kiểm đếm và đóng |
| Terminal có bất kỳ trạng thái active nào | `TERMINAL_ALREADY_HAS_OPEN_SHIFT` | Chọn terminal khác |
| Phiên cũ `CLOSED` | không chặn | Tạo WorkShift mới với request/lý do/OTP/tiền mới |
| Phiên cũ `RECONCILIATION_REQUIRED` | không chặn | Phiên cũ tiếp tục đối soát riêng |

`WORKSHIFT_EXPIRED` chỉ dùng khi thao tác trực tiếp trên WorkShift thực sự hết hạn và hành động không còn được phép; không dùng cho preview/mở mới, staff/terminal active, request trùng hoặc exchange code.

## 5. Terminal

StaffHub tải terminal active đúng store. Terminal phải tồn tại, active và cửa hàng của terminal phải active. Đăng ký terminal cũng thực hiện tại StaffHub:

1. Backend sinh/bind terminal ID và RequestKey của luồng.
2. Người dùng nhập tên.
3. OTP được requester/approver đúng store xác nhận với quyền `POS.WorkShift.OverrideTerminal`.
4. Backend consume approval rồi đăng ký và làm mới danh sách.

POS không có endpoint hoặc giao diện đăng ký terminal trực tiếp.

## 6. OTP mở ca

OTP bind action, requester, approver, store, terminal, lịch nguồn/OpenContext, lý do và RequestKey. Tiền đầu phiên không thuộc fingerprint OTP vì chỉ được nhập sau exchange tại POS. OTP phải còn hạn, ở trạng thái approved, chưa dùng, đúng payload, permission và scope; được gắn `WorkShiftId` và consume trong transaction mở thành công.

OTP gồm sáu ký tự theo chính sách hiện hành, hash khi lưu, có giới hạn thử/resend/rate limit và không ghi mã thô vào audit.

## 7. Exchange StaffHub → POS

- TTL đúng 60 giây; raw code chỉ trả một lần, database chỉ lưu SHA-256 hash.
- Context bind account, staff, store, terminal, purpose, OpenContext, lịch nguồn, RequestKey, lý do, OTP và WorkShift khi resume.
- Code truyền trong URL fragment, không truyền JWT qua query string.
- React xóa fragment trước network I/O rồi gọi `POST /api/v1/pos/session/exchange`.
- Code dùng một lần. Context server được giữ đến hết POS token để người dùng nhập tiền đầu phiên.
- Mã lỗi riêng: `POS_EXCHANGE_CODE_EXPIRED`, `POS_EXCHANGE_CODE_ALREADY_USED`, `POS_EXCHANGE_CODE_INVALID`.
- Purpose gồm `OPEN_WORKSHIFT` và `RESUME_WORKSHIFT`; purpose sai bị từ chối.

## 8. Idempotency và concurrency

Mọi mở WorkShift dùng RequestKey từ exchange context. Payload hash gồm actor/action/store/terminal/context và tiền đầu phiên.

- Retry cùng key và payload trả WorkShift đã tạo.
- Cùng key nhưng payload khác trả `DUPLICATE_REQUEST`.
- Kiểm tra staff/terminal active và insert chạy trong transaction serializable.
- Rowversion và filtered unique index `UX_WorkShifts_ActiveStaff`, `UX_WorkShifts_ActiveTerminal` bảo vệ ba trạng thái active.
- Unique/rowversion race được requery để trả lỗi nghiệp vụ hoặc `CONCURRENCY_CONFLICT`.
- Worker hết hạn dùng transaction/row lock; không tự đóng phiên có tiền, order, payment, offline manifest hoặc dữ liệu cần kiểm đếm.

## 9. Giao diện StaffHub khi bị khóa

StaffHub hiển thị mã phiên, terminal, thời gian bắt đầu, trạng thái và hạn:

- `OPEN`: nút “Tiếp tục POS”.
- `CLOSING`: nút “Hoàn tất đóng ca”.
- `EXPIRED_PENDING_CLOSE`: nút “Kiểm đếm và đóng”.

Không gom các trường hợp thành thông báo “Phiên POS hết hạn”.

## 10. Endpoint hiện hành

StaffHub cookie authorization + anti-forgery:

- `POST /StaffHub/PreviewOpenPos`
- `POST /StaffHub/RequestOpenPosOtp`
- `POST /StaffHub/VerifyOperationalOtp`
- `POST /StaffHub/ResendOperationalOtp`
- `POST /StaffHub/RequestTerminalRegistrationOtp`
- `POST /StaffHub/RegisterTerminal`
- `POST /StaffHub/IssuePosToken`
- `POST /StaffHub/IssueResumePosToken`

POS:

- `POST /api/v1/pos/session/exchange`
- `POST /api/v1/pos/shifts/open` — chỉ tiền đầu phiên từ client; identity/context từ token và database.

Public POS `open-assessment` và đăng ký terminal trực tiếp đã bị loại bỏ.

## 11. Error-code matrix

`STAFF_ALREADY_HAS_OPEN_SHIFT`, `TERMINAL_ALREADY_HAS_OPEN_SHIFT`, `WORKSHIFT_PENDING_CLOSE`, `WORKSHIFT_EXPIRED`, `DUPLICATE_REQUEST`, `CONCURRENCY_CONFLICT`, `POS_OPEN_CONTEXT_REQUIRED`, `POS_OPEN_CONTEXT_INVALID`, `POS_EXCHANGE_CODE_EXPIRED`, `POS_EXCHANGE_CODE_ALREADY_USED`, `POS_EXCHANGE_CODE_INVALID`.

## 12. Nghiệm thu tối thiểu

Phải kiểm tra đủ: đúng lịch; ngoài lịch khi store trống; A/Terminal 1 và B/Terminal 2 độc lập; terminal đã dùng; cùng staff mở thêm; expired pending close; phiên cũ closed; double-click; worker đồng thời với open; exchange hết hạn; terminal registration tại StaffHub; từ chối mở POS trực tiếp. Không test nào được kỳ vọng tạo `StaffShift` giả hay sao chép tiền.
