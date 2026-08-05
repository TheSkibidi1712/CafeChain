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

- Đổi operator bằng PIN cá nhân, kiểm tra account active, permission và StaffScope.
- Không sửa trực tiếp ResponsibleStaffId.
- Order lưu người tạo; Payment lưu người thu; cancel/refund lưu người thực hiện và người phê duyệt khi cần.
- Hủy, refund, giảm giá vượt ngưỡng, force-close, transfer và override phải theo permission/policy hiện hành và có audit.

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
