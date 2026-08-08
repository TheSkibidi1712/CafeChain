# Đặc tả nghiệp vụ Terminal POS, WorkShift và OTP

Ngày cập nhật: 06/08/2026.

## 1. Mục đích và phạm vi

Tài liệu này mô tả trách nhiệm vận hành POS, két tiền, terminal, OTP và giao dịch của CafeChain. WorkShift không phải chấm công, không dùng tính lương và không dùng tính tăng ca.

Luồng chuẩn:

```text
Store → Terminal → WorkShift → Responsible Staff
                           └→ Current Operator → Order/Payment
```

## 2. Thuật ngữ

- **Store**: cửa hàng CafeChain; một Store có nhiều Terminal.
- **Terminal**: quầy hoặc thiết bị POS logic thuộc đúng một Store tại một thời điểm.
- **Shift**: mẫu khung giờ dự kiến.
- **StaffShift**: lịch dự kiến của nhân viên; không giữ két và không phải phiên POS.
- **WorkShift**: phiên chịu trách nhiệm POS, két tiền, Order, Payment và đối soát.
- **Responsible Staff**: chủ trách nhiệm WorkShift và két; tối đa một WorkShift active.
- **Current Operator**: nhân viên đang thao tác; có thể khác Responsible Staff nhưng không làm đổi chủ WorkShift.

## 3. Bất biến dữ liệu

- Một Terminal có tối đa một WorkShift thuộc `OPEN`, `CLOSING` hoặc `EXPIRED_PENDING_CLOSE`.
- Một Responsible Staff có tối đa một WorkShift thuộc các trạng thái active trên.
- WorkShift mới luôn có ID mới; không tái sử dụng phiên `CLOSED`.
- Order và Payment luôn giữ nguyên StoreId, TerminalId và WorkShiftId gốc.
- Thời gian lưu và so sánh bằng UTC; giao diện hiển thị theo múi giờ cửa hàng, mặc định Việt Nam.
- Không tin StaffId, StoreId, permission, OTP verified hoặc WorkShiftId do client tự khai báo.
- SignalR chỉ phát sau khi transaction đã commit.

## 4. State machine WorkShift

```text
OPEN
 ├─ yêu cầu đóng → CLOSING
 └─ hết thời lượng → EXPIRED_PENDING_CLOSE

CLOSING
 ├─ kiểm đếm và commit thành công → CLOSED
 └─ ngoại lệ cần xử lý tiếp → RECONCILIATION_REQUIRED

EXPIRED_PENDING_CLOSE
 ├─ không nhận giao dịch mới
 ├─ vẫn khóa Terminal
 └─ kiểm đếm/đóng thay → CLOSED hoặc RECONCILIATION_REQUIRED
```

`RECONCILIATION_REQUIRED` chỉ giải phóng Terminal sau khi bán hàng đã dừng, có cash snapshot và bàn giao được xác nhận. Không tự điền tiền kiểm đếm và không tự `CLOSED` khi hết hạn.

## 5. Mở và tiếp tục POS

### Mở mới

1. StaffHub kiểm tra account, permission, StaffScope, Store và Terminal.
2. Backend xác định ngữ cảnh lịch; ngoài lịch yêu cầu lý do 10–500 ký tự và OTP hợp lệ.
3. Exchange code one-time bind account, staff, store, terminal, purpose, request key, lịch, lý do và OTP.
4. POS redeem exchange code và chỉ gửi tiền đầu phiên.
5. Backend revalidate toàn bộ context trong transaction, tạo WorkShift mới và trả đúng ID vừa commit.
6. Ngoài lịch: `AutoCloseAtUtc = StartTimeUtc + 6 giờ`.

Kết quả mở mới là `OPENED_NEW_WORKSHIFT`, `RequiresOpeningCash = true`.

### Tiếp tục phiên

- Chỉ dùng WorkShift active hiện tại của chính Responsible Staff.
- Không tạo WorkShift mới và không nhập lại tiền đầu phiên.
- Phát exchange code mới bind đúng WorkShiftId.
- Kết quả là `RESUME_EXISTING_WORKSHIFT`, `RequiresOpeningCash = false`.

Phiên `CLOSING` hoặc `EXPIRED_PENDING_CLOSE` không được mở mới. Terminal thuộc người khác không được chiếm bằng sửa URL hoặc payload.

## 6. OTP vận hành

```text
IDLE → SENDING → SENT → VERIFYING
                         ├→ VERIFIED
                         ├→ INVALID
                         ├→ EXPIRED
                         └→ LOCKED
```

- OTP bind requester, approver, store, terminal, action, reason fingerprint và RequestKey.
- OTP có hạn, one-time, cooldown, giới hạn số lần sai và lockout.
- Resend làm OTP cũ mất hiệu lực nhưng không tạo yêu cầu nghiệp vụ trùng.
- Reload lấy state từ server; LocalStorage không phải nguồn sự thật.
- Sau `VERIFIED`, khóa context, xóa mã khỏi input và ẩn form OTP.
- Không log OTP, PIN, access token hoặc exchange code.

## 7. Current Operator và thao tác nhạy cảm

- Đổi operator bằng PIN cá nhân để bàn giao thao tác trên cùng quầy mà không đóng/mở lại két; backend kiểm tra account active, `POS.Operator.Switch` và StaffScope.
- Không sửa `WorkShift.UserId`/Responsible Staff, tiền đầu ca, WorkShiftId hoặc trách nhiệm tài chính. Đây không phải bàn giao két.
- Order lưu người tạo thực tế; Payment lưu người thu; cancel/refund lưu người thực hiện và người phê duyệt khi cần.
- Response authoritative trả `currentOperatorName` và thời điểm đổi; trang Két, header bán hàng và tab cùng terminal đồng bộ qua `WorkShiftChanged`/SignalR.
- PIN là mã cá nhân, chỉ lưu hash và không được chia sẻ, ghi log hay đưa vào audit payload.
- Hủy, refund, giảm giá vượt ngưỡng, force-close, transfer và override phải theo permission/policy hiện hành và có audit.

### 7.1 Xử lý mở ca trễ từ 30 phút

- Luồng này dùng `WorkShiftOpenApprovalRequest`, không dùng OTP.
- Notification của người duyệt trỏ tới `/Admin/AdminWorkShiftOpenApprovals#approval-{id}` và hiển thị **Xem và duyệt yêu cầu**.
- Backend yêu cầu `POS.WorkShift.ApproveLateOpen`, StaffScope đúng Store, antiforgery, RequestKey và row-version.
- `LateApprovalAfterMinutes=30` là mốc bắt đầu cần Manager, tính bao gồm phút 30; `LateScheduledApprovalMaxMinutes=45` là mốc tối đa được duyệt theo lịch cũ.
- `30 <= MinutesLate <= 45`: `APPROVED`, `REJECTED`, `CONVERTED_TO_OUTSIDE_SCHEDULE` đều hợp lệ.
- `MinutesLate > 45`: UI khóa **Duyệt mở ca**, backend chặn direct POST `APPROVED` bằng `LATE_OPEN_REQUIRES_OUTSIDE_SCHEDULE`; chỉ `REJECTED` và `CONVERTED_TO_OUTSIDE_SCHEDULE` hợp lệ.
- Convert chỉ đổi context sang ngoài lịch, không sửa `StaffShift` nguồn và không tạo WorkShift tại màn duyệt.

## 8. Đóng, bàn giao và đối soát

```text
Yêu cầu đóng → CLOSING → khóa giao dịch mới
→ hoàn tất request đã nhận → kiểm đếm → commit
→ revoke session → giải phóng Terminal → phát SignalR
```

Bàn giao mặc định đóng WorkShift A sau kiểm đếm rồi tạo WorkShift B. Máy hỏng mặc định đóng hoặc force-close phiên cũ và mở phiên mới trên Terminal khác; không transfer WorkShift trong giai đoạn đầu.

## 9. Offline và idempotency

Offline payload tối thiểu gồm ClientRequestId, OriginalWorkShiftId, StoreId, TerminalId, OperatorStaffId, device time, DeviceInstallationId và payload hash. Đồng bộ muộn vẫn gắn WorkShift gốc, kể cả khi Terminal đã có phiên mới.

Các thao tác thay đổi dữ liệu dùng RequestKey. Cùng key/cùng payload trả lại kết quả cũ; đang xử lý trả `REQUEST_ALREADY_PROCESSING`; cùng key/khác payload trả `REQUEST_KEY_PAYLOAD_MISMATCH`.

## 10. Quy tắc phản hồi giao diện

- Validation: toast `warning`, highlight field lỗi và focus field đầu tiên; không gửi request.
- Business/server error: toast `error` với message tiếng Việt rõ ràng.
- Thành công: toast `success`.
- Thao tác nguy hiểm: SweetAlert confirm.
- Nút submit disable ngay, có `aria-busy` và nhãn “Đang …”; thất bại phải phục hồi đúng trạng thái trước đó.

Thứ tự chọn message: message nghiệp vụ hợp lệ → ánh xạ error code → validation error → fallback theo hành động. Không hiển thị HTML, stack trace hoặc SQL error.

Fallback chuẩn:

- Create: “Không thể tạo … Dữ liệu chưa được lưu.”
- Update: “Không thể cập nhật … Các thay đổi chưa được lưu.”
- Delete: “Không thể xóa … Dữ liệu có thể đang được sử dụng.”
- Network: “Không thể kết nối máy chủ. Vui lòng kiểm tra mạng và thử lại.”
- HTTP 401/403/409 có hướng xử lý riêng; lỗi 5xx hiển thị correlation ID khi có.

## 11. Permission và audit

Backend kiểm tra permission và StaffScope cho mọi thao tác. Audit tối thiểu lưu action, store, terminal, WorkShift, Responsible Staff, performed/approved staff, reason, request key, thời gian UTC và device installation nếu có.

## 12. Trạng thái triển khai đã được kiểm thử

- Đã scope CSS modal procurement, không còn ghi đè mọi Bootstrap modal.
- Đã chuẩn hóa toast dùng DOM an toàn, hỗ trợ success/warning/error/info và fallback theo status/error code.
- Đã bổ sung mutation guard dùng chung, validation warning và tự động đưa Bootstrap modal/offcanvas ra `#cc-modal-host` nằm trực tiếp dưới `body`.
- Bootstrap tự quản lý backdrop, focus trap, `modal-open` và body overflow; không còn cleanup backdrop cưỡng chế từ JavaScript dùng chung.
- Category, Size, Topping, Drink, Inventory Document, Ingredient, StoreInventory, Supplier và StaffHub đã dùng lifecycle Bootstrap; StoreMenu, Profitability và Supplier detail dùng Bootstrap Offcanvas.
- Create Drink hiển thị summary/toast khi validation thất bại, highlight các field lỗi và luôn khôi phục nút Tạo. UI “Ý tưởng cho AI” đã được bỏ riêng khỏi form Create Drink.
- StaffHub có fallback rõ cho 401, 403, 409 và lỗi hệ thống có mã tra cứu.
- Open mới trả result code bổ sung và summary của đúng WorkShiftId vừa tạo; resume trả WorkShiftId và không yêu cầu opening cash.
- Bộ contract test modal/CRUD UX và regression P0/StaffHub/Current Operator đã chạy thành công 33/33 tại lần cập nhật tài liệu.
- Toàn bộ test không phụ thuộc SQL Server đã đạt 1764/1764; nhóm SQL Server chưa thể nghiệm thu trong phiên này do lỗi xác thực SSPI/kết nối instance.
- OTP security Phase 1 đạt 25/25; các nhóm Phase 2–4 và operational OTP đạt 96/96.
- Luồng đóng ca chỉ dùng một transaction cho khóa WorkShift, kiểm tra/consume OTP và commit tài chính; không mở transaction lồng.
- SeedAll quản lý bao bì theo hai cấp: BOM/tồn kho dùng `pcs`, mua hàng dùng `Thùng`; quy đổi 1 thùng = 1.000 ly/nắp, 2.000 ống hút hoặc 500 túi.
- SeedAll đã chạy hai lần thành công trên database SQL Server sạch sau thay đổi đơn vị bao bì.
- Đã bổ sung nền Current Operator: PIN BCrypt riêng, lockout 15 phút sau 5 lần sai, permission `POS.Operator.Switch`, RequestKey và audit `POS_OPERATOR_CHANGED`.
- WorkShift giữ riêng Responsible Staff và Current Operator; mở phiên mới khởi tạo Current Operator bằng Responsible Staff.
- Order POS online ghi operator thực tế và Terminal gốc; Payment ghi `PaidByStaffId`, StoreId, WorkShiftId và TerminalId.
- Schema `InitialCreate` hợp nhất hiện có `CurrentOperatorStaffId`, `WorkShiftId` và `PaidByStaffId`; contract test cũng hỗ trợ migration gia tăng nếu dự án quay lại chiến lược migration nâng cấp dữ liệu cũ.

## 13. Chưa hoàn thành

- Browser acceptance thực tế ở desktop/mobile cần phiên ứng dụng và tài khoản test đang hoạt động.
- UI thiết lập PIN tại StaffHub và đổi Current Operator tại POS đã được triển khai; danh sách ứng viên được lọc theo cửa hàng, trạng thái tài khoản và permission phía server.
- Lịch sử operator phục vụ offline, DeviceInstallationId, auto-lock và toàn bộ sensitive-action approval chưa được tuyên bố hoàn thành.
- Full SQL Server race, offline late-sync end-to-end và full regression suite phải đạt trước nghiệm thu cuối.

## 14. Contract kiến trúc cuối

### Terminal registration notification

`StaffNotification.OtpChallengeId` là liên kết dữ liệu chuẩn. `Body` chỉ phục vụ trình bày. Terminal name được snapshot tại challenge; requester, approver, confirmer, Store, thời hạn và trạng thái được đọc từ quan hệ typed. List API không bao giờ trả OTP plaintext; reveal yêu cầu đúng recipient, permission và store scope, trả `Cache-Control: no-store`.

Một request nghiệp vụ tạo đúng một notification. Request lặp khi còn hiệu lực trả lại challenge hiện tại; resend sau cooldown rotate OTP trên cùng notification. Notification kết thúc vẫn được giữ để audit với trạng thái ánh xạ `Waiting`, `Used`, `Expired`, `Cancelled`. Mark-one chỉ update đúng notification của recipient hiện tại.

### PosAccessSession

`PosAccessSession` là aggregate bảo mật của lần truy cập POS, không phải WorkShift. Trạng thái gồm `ACTIVE`, `LOGGED_OUT`, `REPLACED`, `REVOKED`, `ADMIN_ENDED`, `EXPIRED`, `TERMINAL_LOCKED`. Unique filtered index bảo đảm một session active trên mỗi Terminal; tạo session thay thế, audit và commit chạy cùng transaction.

JWT chứa session public id/JTI. Validation kiểm tra session active/chưa hết hạn, account/staff/store/Terminal active và scope khớp. SignalR chỉ được phát sau commit; lỗi realtime không rollback dữ liệu đã commit và client hồi phục bằng validation/polling.

Worker `PosAccessSessionExpiryWorker` chủ động persist `ACTIVE → EXPIRED`, ghi audit và publish realtime sau commit, kể cả khi browser không phát sinh request mới. Vì vậy danh sách quản trị và unique active-session contract không phụ thuộc vào lần validation tiếp theo.

Order online không nhận `WorkShiftId` tin cậy từ JSON. Controller lấy session server-side, gán `BoundWorkShiftId` server-only; service đọc chính xác ca theo `(WorkShiftId, StaffId, StoreId)` trước và trong transaction. Các mutation đóng/đối soát/đổi operator cũng từ chối khi ca không khớp session.

### Late-open approval

Trễ từ 30 phút dùng `WorkShiftOpenApprovalRequest`, không dùng OTP. Request có unique `RequestKey`, rowversion, requester/decision-maker, source StaffShift, Terminal, lý do và timestamps. Approve chỉ hợp lệ đến 45 phút; reject khóa luồng, còn convert tạo context ngoài lịch sáu giờ. Create/decide idempotent, audited và publish `LateOpenApprovalChanged` sau commit.

Hủy tại POS chỉ hợp lệ trước khi session bind WorkShift. Exchange context ghi `CancelledAtUtc`, `RequiresOpeningCash=false` và chuyển ticket sang `EXPIRED`, là trạng thái hợp lệ của `RequestDeduplications`; business OTP/approval chưa dùng chuyển `CANCELLED`. Retry hủy trả thành công idempotent, còn session đã có `WorkShiftId` trả conflict.

Worker expiry persist `PENDING → EXPIRED`, resolve toàn bộ notification liên quan và phát SignalR sau commit. Quyền `POS.WorkShift.ApproveLateOpen` và `POS.Session.Manage` chỉ được seed cho Business Owner, Area Manager và Store Manager; Shift Supervisor không được cấp. SignalR cũng dùng group permission theo store tương ứng, không broadcast toàn hệ thống.

### Database baseline

Schema mới được tích hợp vào migration baseline `InitialCreate` theo quyết định của dự án: FK/index OTP notification, `PosAccessSessions`, `WorkShiftOpenApprovalRequests` và các unique filtered index. Database đã tồn tại không được tự ý chạy baseline như migration gia tăng; phải recreate database hoặc dùng script chuyển đổi được kiểm soát riêng.
