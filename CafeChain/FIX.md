# PROMPT HOÀN CHỈNH CHO CODEX  
# REFACTOR TERMINAL POS, WORKSHIFT, MỞ POS NGOÀI LỊCH VÀ OTP – CAFECHAIN

---

## 1. Vai trò

Bạn hãy đóng vai đồng thời là:

- Senior Software Engineer có ít nhất 20 năm kinh nghiệm.
- Business Analyst chuyên về POS, quản lý ca, két tiền và vận hành chuỗi cửa hàng.
- Solution Architect chuyên ASP.NET Core MVC, EF Core và SQL Server.
- Security Engineer có kinh nghiệm về:
  - Authentication.
  - Authorization.
  - StaffScope.
  - OTP.
  - PIN.
  - Idempotency.
  - Concurrency.
  - Audit log.
  - SignalR.
  - Đồng bộ offline.
- QA Engineer có kinh nghiệm thiết kế:
  - Unit test.
  - Integration test.
  - Concurrency test.
  - Authorization test.
  - Idempotency test.
  - UI state test.
  - Timezone test.

Bạn phải inspect codebase thực tế trước khi sửa.

Không được chỉ đọc prompt rồi tự đoán tên file, tên class, route, entity hoặc method.

Không được tự tạo abstraction hoặc layer mới nếu hệ thống hiện tại đã có cấu trúc tương đương.

---

# 2. Mục tiêu tổng thể

Refactor toàn bộ luồng liên quan đến:

- Store.
- Terminal POS.
- Shift.
- StaffShift.
- WorkShift.
- StaffHub.
- Mở POS bình thường.
- Mở POS ngoài lịch.
- Tiếp tục phiên POS.
- Đóng WorkShift.
- Kiểm đếm két.
- Bàn giao Terminal.
- Current Operator.
- OTP phê duyệt.
- Exchange code từ StaffHub sang POS.
- Request deduplication.
- Session.
- Cookie.
- LocalStorage.
- SignalR.
- Order.
- Payment.
- Đồng bộ offline.
- Audit log.
- Permission.
- StaffScope.

Mục tiêu là:

1. Sửa triệt để lỗi mở POS ngoài lịch nhưng POS lập tức báo phiên hết hạn.
2. Refactor UX gửi và xác nhận OTP.
3. Chuẩn hóa mô hình một Store có nhiều Terminal.
4. Ngăn hai WorkShift active trên cùng một Terminal.
5. Ngăn một nhân viên sở hữu nhiều WorkShift active trái nghiệp vụ.
6. Cho phép nhiều nhân viên thao tác luân phiên trên cùng WorkShift dưới dạng Current Operator.
7. Ghi nhận đúng người thao tác trên từng Order, Payment, hủy, hoàn tiền và thao tác nhạy cảm.
8. Đảm bảo WorkShift, két tiền, Order và Payment không bị trộn giữa các phiên.
9. Chống double-click, request trùng và race condition.
10. Đảm bảo dữ liệu offline luôn giữ đúng WorkShift gốc.
11. Bảo đảm các kiểm tra permission và StaffScope được thực hiện server-side.
12. Giữ giải pháp phù hợp với quy mô 2–5 cửa hàng CafeChain.

---

# 3. Bối cảnh dự án

Đây là dự án CafeChain dành cho khoảng 2–5 cửa hàng cà phê.

Kiến trúc hiện tại phải được tôn trọng:

```text
Controller
→ Service
→ Repository
→ Database
```

Quy tắc kiến trúc:

- Controller chỉ gọi Service.
- Controller không truy cập DbContext trực tiếp.
- Controller không gọi Repository trực tiếp.
- Service không bỏ qua Repository nếu module hiện tại đã sử dụng Repository.
- Repository chịu trách nhiệm truy cập dữ liệu.
- Transaction phải đặt tại layer phù hợp theo kiến trúc hiện tại.
- Không tạo file hoặc abstraction không cần thiết.
- Không đổi tên hàng loạt entity, DTO, route hoặc permission nếu chưa thật sự cần.
- Không sửa các module không liên quan.
- Phải giữ tương thích dữ liệu cũ hoặc cung cấp migration và kế hoạch xử lý rõ ràng.

Hệ thống hiện tại:

- Không dùng WorkShift để chấm công.
- Không dùng WorkShift để tính lương.
- Không dùng WorkShift để tính tăng ca.
- Không được thiết kế thêm bảng công hoặc nghiệp vụ lương.
- WorkShift chỉ quản lý trách nhiệm POS, két tiền, Order, Payment và đối soát.
- StaffHub thực hiện chọn Terminal, kiểm tra lịch, lý do và OTP.
- Sau khi xác nhận hợp lệ, StaffHub phát exchange code và chuyển sang POS.
- POS chỉ yêu cầu nhập tiền đầu phiên khi tạo WorkShift mới.
- Nếu tiếp tục WorkShift hiện có thì không được nhập lại tiền đầu phiên.

---

# 4. Tài liệu và bằng chứng cần dùng

Hãy dùng các nguồn sau làm cơ sở:

1. Tài liệu nghiệp vụ Terminal POS hiện có.
2. Tài liệu nghiệp vụ Terminal POS và WorkShift đã tổng hợp.
3. Ảnh lỗi POS:
   - POS hiển thị `Mở ca thành công`.
   - Đồng thời hiển thị `Phiên POS đã hết hạn và không nhận giao dịch mới`.
   - POS hiển thị thời gian mở ca cũ khoảng `05:51 04/08/2026`.
4. Ảnh luồng StaffHub mở ngoài lịch:
   - Thời gian yêu cầu khoảng `12:45 04/08/2026`.
   - Dự kiến hết hạn `18:45 04/08/2026`.
   - Thời lượng ngoài lịch tối đa 6 giờ.
   - Nhập lý do.
   - Gửi OTP.
   - Xác nhận OTP.
   - Tiếp tục sang POS.

Không được mặc định ảnh đã chỉ ra nguyên nhân.

Ảnh chỉ là triệu chứng.

Phải inspect code và dữ liệu để tìm root cause thật.

---

# 5. Thứ tự ưu tiên bắt buộc

Thực hiện đúng thứ tự:

## P0 – Ưu tiên cao nhất

Sửa lỗi mở POS ngoài lịch nhưng POS lập tức báo hết hạn.

Chỉ được chuyển sang phần khác sau khi đã:

- Tái hiện được lỗi.
- Tìm được root cause.
- Sửa code.
- Có test regression.
- Test đã chạy thành công.
- Xác nhận WorkShift mới dùng đúng WorkShiftId, thời gian mở và thời gian hết hạn.

## P1

Refactor UX gửi, xác nhận và gửi lại OTP tại StaffHub.

## P2

Refactor và hoàn thiện mô hình:

- Terminal.
- WorkShift.
- Responsible Staff.
- Current Operator.
- Bàn giao.
- Tiếp tục phiên.
- Đóng thay.
- Offline.
- Concurrency.
- Audit.
- SignalR.
- Permission.

Không được sửa UI để che lỗi P0.

Không được chỉ ẩn cảnh báo hết hạn.

---

# 6. Phân biệt các khái niệm nghiệp vụ

## 6.1. Store

Là một cửa hàng hoặc chi nhánh CafeChain.

Một Store được có nhiều Terminal.

Ví dụ:

```text
CafeChain Thủ Dầu Một
├─ Quầy chính
├─ Quầy mang đi
├─ POS 01
└─ POS 02
```

---

## 6.2. Shift

Là mẫu giờ làm việc dự kiến.

Ví dụ:

```text
Ca sáng: 06:00–14:00
Ca chiều: 14:00–22:00
```

Shift không phải phiên POS và không giữ két tiền.

---

## 6.3. StaffShift

Là lịch dự kiến phân cho nhân viên.

Ví dụ:

```text
Nhân viên A: Ca sáng
Nhân viên B: Ca sáng
```

Hai nhân viên cùng StaffShift không có nghĩa là hai người được mở chung một Terminal.

Không phải nhân viên nào có StaffShift cũng bắt buộc mở POS.

---

## 6.4. WorkShift

Là phiên chịu trách nhiệm POS và két tiền.

WorkShift có thể chứa:

- StoreId.
- TerminalId.
- ResponsibleStaffId.
- OpenedAtUtc.
- ExpiresAtUtc.
- ClosedAtUtc.
- OpeningCash.
- ExpectedCash.
- CountedCash.
- CashDifference.
- Status.
- OpenMode.
- Reason.
- ApprovedByStaffId.
- RequestKey.
- Order.
- Payment.
- Audit.

WorkShift không phải chấm công.

---

## 6.5. Terminal POS

Là định danh thiết bị hoặc quầy bán hàng.

Ví dụ:

```text
Quầy chính
Quầy mang đi
POS 01
POS 02
Tầng 2 - POS 01
```

Terminal không phải:

- Nhân viên.
- Shift.
- StaffShift.
- WorkShift.
- Chấm công.

Terminal xác định WorkShift đang sử dụng thiết bị hoặc quầy nào.

---

## 6.6. Responsible Staff

Là nhân viên chịu trách nhiệm chính cho WorkShift và két tiền.

Responsible Staff:

- Mở WorkShift.
- Nhập tiền đầu phiên.
- Chịu trách nhiệm chốt hoặc bàn giao két.
- Là chủ sở hữu WorkShift.
- Không được thay đổi trực tiếp giữa phiên.
- Chỉ được chuyển trách nhiệm bằng nghiệp vụ bàn giao có kiểm soát.

---

## 6.7. Current Operator

Là nhân viên đang thao tác trên POS tại thời điểm hiện tại.

Current Operator có thể khác Responsible Staff.

Ví dụ:

```text
WorkShift thuộc nhân viên A.

Order 001:
- CreatedByStaffId = A

Order 002:
- CreatedByStaffId = B

Payment Order 002:
- PaidByStaffId = B

Hủy Order 003:
- CancelledByStaffId = B
- ApprovedByStaffId = Store Manager
```

WorkShift vẫn thuộc A.

---

# 7. Mô hình nghiệp vụ mục tiêu

```text
STORE
│
├─ TERMINAL 1
│  └─ WORKSHIFT A
│     ├─ Responsible Staff: A
│     ├─ Current Operator: A hoặc B
│     ├─ Orders
│     ├─ Payments
│     └─ Cash Drawer
│
└─ TERMINAL 2
   └─ WORKSHIFT B
      ├─ Responsible Staff: C
      ├─ Current Operator: C hoặc D
      ├─ Orders
      ├─ Payments
      └─ Cash Drawer
```

Quy tắc:

```text
1 Store
└─ Có nhiều Terminal

1 Terminal
└─ Tối đa 1 WorkShift active

1 WorkShift
└─ Có 1 Responsible Staff

1 WorkShift
└─ Có thể có nhiều Current Operator luân phiên

1 giao dịch
└─ Ghi đúng người thực hiện
```

---

# 8. Trạng thái WorkShift

Các trạng thái tối thiểu hiện có hoặc tương đương:

```text
OPEN
CLOSING
EXPIRED_PENDING_CLOSE
CLOSED
RECONCILIATION_REQUIRED
```

## Trạng thái khóa Terminal

```text
OPEN
CLOSING
EXPIRED_PENDING_CLOSE
```

## Trạng thái không khóa Terminal theo tài liệu hiện tại

```text
CLOSED
RECONCILIATION_REQUIRED
```

Tuy nhiên, `RECONCILIATION_REQUIRED` chỉ được giải phóng Terminal khi:

```text
Sales stopped
AND
Cash snapshot created
AND
Handover confirmed
```

Không được dùng `RECONCILIATION_REQUIRED` để bỏ qua kiểm đếm.

---

# 9. State machine đề xuất

```text
OPEN
├─ Nhận Order và Payment
├─ Đổi Current Operator
├─ Yêu cầu đóng
└─ Hết thời lượng
     ↓
EXPIRED_PENDING_CLOSE
├─ Không nhận giao dịch mới
├─ Terminal vẫn khóa
└─ Chờ kiểm đếm hoặc đóng thay
```

```text
OPEN
  ↓
CLOSING
├─ Không nhận giao dịch mới
├─ Terminal vẫn khóa
├─ Chờ kiểm đếm
└─ Chờ các request đã được chấp nhận hoàn tất
```

```text
CLOSING
  ↓
CLOSED
├─ Hoàn tất tài chính
└─ Giải phóng Terminal
```

```text
CLOSING hoặc force-close
  ↓
RECONCILIATION_REQUIRED
├─ Không nhận giao dịch mới
├─ Giữ dữ liệu cũ
├─ Chờ đối soát
└─ Chỉ giải phóng Terminal khi đủ điều kiện
```

---

# 10. P0 – Lỗi mở POS ngoài lịch nhưng lập tức hết hạn

## 10.1. Hiện tượng

Tài khoản nhân viên bán hàng không có StaffShift thực hiện:

```text
Đăng nhập StaffHub
        ↓
Chọn mở POS ngoài lịch
        ↓
Chọn Terminal
        ↓
Nhập lý do
        ↓
Gửi OTP
        ↓
OTP được phê duyệt
        ↓
Xác nhận OTP
        ↓
Tiếp tục sang POS
```

Sau đó POS hiển thị đồng thời:

```text
Mở ca thành công.
```

và:

```text
Phiên POS đã hết hạn và không nhận giao dịch mới.
Vui lòng kiểm đếm, chốt két hoặc đóng ngoại lệ.
```

Trong khi:

- Phiên trước đã được người dùng kết thúc.
- Nhân viên hiện tại không có lịch.
- StaffHub cho mở ngoài lịch.
- Dự kiến phiên mới có thời lượng 6 giờ.
- POS lại hiển thị thời gian mở ca cũ.

Đây là lỗi nghiêm trọng vì:

- UI mâu thuẫn.
- WorkShift mới có thể đang bind sai.
- POS có thể lấy nhầm phiên cũ.
- Exchange code có thể chứa WorkShiftId cũ.
- Session hoặc localStorage có thể ghi đè dữ liệu mới.
- Request deduplication có thể trả kết quả cũ.
- Thời gian UTC/local có thể bị tính sai.
- Phiên cũ có thể chưa đóng thật trong database.

---

# 11. Các giả thuyết P0 bắt buộc phải kiểm tra

## 11.1. POS đang lấy nhầm WorkShift cũ

Inspect query lấy WorkShift hiện tại.

Tìm các pattern như:

```text
Lấy WorkShift gần nhất của Staff
Lấy WorkShift gần nhất của Terminal
Lấy bản ghi đầu tiên chưa CLOSED
Lấy bản ghi không có OrderBy rõ ràng
```

POS không được tự suy đoán phiên hiện tại.

POS phải sử dụng chính xác WorkShiftId từ exchange code.

Kiểm tra:

- Query hiện tại.
- Điều kiện lọc status.
- OrderBy.
- Cache.
- Session.
- WorkShift cũ còn active.
- WorkShift đã đóng nhưng vẫn được query.
- WorkShiftId từ exchange code có bị bỏ qua.

---

## 11.2. Exchange code bind sai WorkShift

Exchange code phải bind tối thiểu:

```text
StaffId
StoreId
TerminalId
WorkShiftId
OpenMode
ApprovedOtpRequestId
IssuedAtUtc
ExpiresAtUtc
RequestKey
```

Exchange code không được:

- Chỉ chứa StaffId rồi để POS tự tìm WorkShift.
- Trỏ đến WorkShift cũ.
- Dùng lại payload cũ.
- Dùng lại sau khi phiên trước đóng.
- Bind sai Terminal.
- Bind sai Store.
- Bind sai OTP request.

Khi redeem, POS phải lấy đúng WorkShiftId đã được StaffHub tạo hoặc xác nhận.

---

## 11.3. Session, cookie hoặc localStorage giữ WorkShiftId cũ

Inspect:

- Cookie.
- ASP.NET Session.
- Claims.
- LocalStorage.
- SessionStorage.
- IndexedDB.
- JavaScript state.
- Cache server.
- SignalR state.
- Exchange redemption result.

Khi WorkShift đóng:

- Revoke session liên quan.
- Xóa hoặc vô hiệu hóa WorkShiftId cũ.
- Vô hiệu hóa exchange code cũ.
- Không cho client cũ tạo giao dịch.
- Gửi SignalR khóa POS cũ sau commit.

Khi mở WorkShift mới:

- Trạng thái mới phải thay hoàn toàn trạng thái cũ.
- WorkShiftId cũ trên client không được ghi đè dữ liệu từ server.

---

## 11.4. WorkShift trước chưa đóng thật

Inspect luồng:

```text
OPEN
→ CLOSING
→ CLOSED
```

Kiểm tra:

- Transaction có commit không.
- ClosedAtUtc có được cập nhật không.
- Status có thật sự là CLOSED không.
- Terminal có được giải phóng không.
- Request dedup có trả thành công cũ dù transaction rollback không.
- SignalR có gửi trước commit không.
- UI có báo thành công trước commit không.
- Có exception sau khi cập nhật một phần không.

Nếu phiên cũ vẫn ở:

```text
OPEN
CLOSING
EXPIRED_PENDING_CLOSE
```

thì không được tạo WorkShift mới.

Không được báo mở thành công.

---

## 11.5. Tính hết hạn sai

Chuẩn hóa:

```text
ExpiresAtUtc = OpenedAtUtc + AllowedDuration
```

Với mở ngoài lịch:

```text
AllowedDuration = 6 giờ
```

Không tính từ:

- OpenedAt của phiên cũ.
- StaffShift gần nhất.
- Đầu ngày.
- Giờ trình duyệt.
- DateTime.Now ở nơi này và DateTime.UtcNow ở nơi khác.
- Thời gian local bị đọc như UTC.
- UTC+7 bị cộng hai lần.

Yêu cầu:

- Lưu database theo UTC.
- So sánh bằng UTC.
- Hiển thị theo múi giờ Store hoặc Việt Nam.
- Kiểm tra DateTime.Kind.
- Kiểm tra JSON serialization.
- Kiểm tra SQL datetime/datetime2.
- Không tự cộng trừ 7 giờ ở nhiều layer.

---

## 11.6. RequestKey trả lại kết quả cũ

Inspect Request Deduplication:

- RequestKey có được tạo mới cho lần mở mới không.
- Có tái sử dụng RequestKey của phiên cũ không.
- ResponseBody cũ có WorkShiftId cũ không.
- PayloadHash có đủ context không.
- ActionName có phân biệt mở mới, tiếp tục phiên và mở ngoài lịch không.

Payload hash nên phản ánh tối thiểu:

```text
StaffId
StoreId
TerminalId
OpenMode
ReasonSnapshot hoặc ReasonHash
OtpRequestId
```

Cùng RequestKey nhưng payload khác:

```text
REQUEST_KEY_PAYLOAD_MISMATCH
```

---

## 11.7. Status và ExpiresAt không đồng bộ

Có thể UI đang dùng:

```text
Badge “Đang mở”
→ Status = OPEN
```

và:

```text
Cảnh báo hết hạn
→ CurrentTime > ExpiresAt
```

Backend phải trả trạng thái thống nhất.

Không được để UI vừa hiện `Đang mở` vừa hiện `Đã hết hạn` mà không có state transition rõ ràng.

---

# 12. Nghiệp vụ đúng khi mở POS ngoài lịch

## 12.1. Không có WorkShift active

```text
Xác nhận Terminal active và đang trống
        ↓
Xác nhận Staff không có WorkShift active
        ↓
Kiểm tra lý do
        ↓
Kiểm tra OTP hợp lệ
        ↓
Tạo WorkShift mới
        ↓
OpenedAtUtc = thời gian server
        ↓
ExpiresAtUtc = OpenedAtUtc + 6 giờ
        ↓
Status = OPEN
        ↓
Bind exchange code với WorkShiftId mới
        ↓
POS redeem WorkShiftId mới
        ↓
Yêu cầu nhập tiền đầu phiên
```

Sau khi mở:

- Không báo hết hạn.
- WorkShiftId mới phải khác phiên cũ.
- Thời gian mở là thời điểm mới.
- Thời gian hết hạn đúng 6 giờ sau.
- Tiền đầu phiên là của phiên mới.
- Không lấy dữ liệu phiên cũ.

---

## 12.2. WorkShift trước đã CLOSED

```text
WorkShift trước = CLOSED
        ↓
Không khóa Terminal
        ↓
Cho phép tạo WorkShift mới
```

WorkShift mới phải có:

- WorkShiftId mới.
- RequestKey mới.
- Exchange code mới.
- OpenedAtUtc mới.
- ExpiresAtUtc mới.
- OpeningCash mới.
- Cash snapshot riêng.

Không tái sử dụng WorkShift cũ.

---

## 12.3. WorkShift hiện tại của chính nhân viên vẫn OPEN

Không tạo WorkShift mới.

Trả:

```text
RESUME_EXISTING_WORKSHIFT
```

UI hiển thị:

```text
Tiếp tục phiên đang mở
```

Không yêu cầu nhập lại tiền đầu phiên.

---

## 12.4. WorkShift đang CLOSING

Không tạo WorkShift mới.

Trả:

```text
WORKSHIFT_ALREADY_CLOSING
```

UI hiển thị:

```text
Phiên đang chốt két
```

---

## 12.5. WorkShift đang EXPIRED_PENDING_CLOSE

Không tạo WorkShift mới.

Trả:

```text
EXISTING_WORKSHIFT_REQUIRES_CLOSE
```

UI hiển thị:

```text
Phiên đã hết hạn, cần kiểm đếm và đóng
```

---

## 12.6. Terminal thuộc phiên của người khác

Trả:

```text
ACTIVE_SHIFT_OWNED_BY_ANOTHER_STAFF
```

Không cho chiếm phiên.

---

# 13. Response contract mở POS

Backend phải trả trạng thái rõ ràng, không chỉ trả message.

## Mở mới

```text
OPENED_NEW_WORKSHIFT
```

Dữ liệu:

```text
WorkShiftId
StoreId
TerminalId
OpenedAtUtc
ExpiresAtUtc
RequiresOpeningCash = true
```

## Tiếp tục phiên

```text
RESUME_EXISTING_WORKSHIFT
```

Dữ liệu:

```text
WorkShiftId
StoreId
TerminalId
OpenedAtUtc
ExpiresAtUtc
RequiresOpeningCash = false
```

## Cần đóng phiên cũ

```text
EXISTING_WORKSHIFT_REQUIRES_CLOSE
```

## Terminal thuộc người khác

```text
ACTIVE_SHIFT_OWNED_BY_ANOTHER_STAFF
```

## Terminal đang chốt

```text
WORKSHIFT_ALREADY_CLOSING
```

UI không tự suy luận bằng chuỗi message.

---

# 14. Không được sửa P0 theo cách đối phó

Không được:

- Chỉ ẩn cảnh báo hết hạn.
- Tự cộng 6 giờ mỗi lần tải trang.
- Tự đổi WorkShift cũ về OPEN.
- Tự đóng WorkShift cũ mà chưa kiểm đếm.
- Xóa dữ liệu WorkShift cũ.
- Chỉ xóa localStorage.
- Chỉ sửa JavaScript.
- Chỉ sửa format giờ.
- Bỏ validation hết hạn.
- Cho phiên hết hạn nhận giao dịch.
- Trả thành công trước khi transaction commit.
- Tạo WorkShift mới để né phiên cũ.
- Gán lại WorkShiftId mới vào Order cũ.

---

# 15. P1 – Refactor UX OTP

## 15.1. Mục tiêu

Cải thiện để:

- Người dùng biết OTP đã gửi hay chưa.
- Không bấm gửi nhiều lần.
- Có thông báo rõ khi đúng hoặc sai.
- Form OTP tự ẩn sau khi xác nhận thành công.
- Reload trang không làm mất trạng thái.
- Chống spam.
- Chống double-click.
- Chống brute force.
- Không tin state client.

---

# 16. State machine OTP

```text
IDLE
→ Chưa gửi

SENDING
→ Đang gửi

SENT
→ Đã gửi, chờ mã

VERIFYING
→ Đang xác nhận

VERIFIED
→ Đã xác nhận thành công

INVALID
→ Mã sai

EXPIRED
→ Mã hết hạn

LOCKED
→ Nhập sai quá số lần
```

Backend là nguồn sự thật.

---

# 17. Nút Gửi OTP

## Khi bấm lần đầu

Ngay lập tức:

- Disable nút.
- Đổi text thành `Đang gửi...`.
- Hiển thị loading.
- Chặn double-click.
- Không cho đổi Terminal.
- Không cho sửa lý do khi request đang xử lý.

## Khi thành công

- Chuyển state `SENT`.
- Ẩn hoặc disable nút `Gửi OTP`.
- Không cho bấm lại.
- Hiển thị SweetAlert2 hoặc toast thành công.
- Hiển thị input OTP.
- Hiển thị nút `Xác nhận OTP`.
- Hiển thị nút `Gửi lại OTP` nhưng disable trong cooldown.
- Hiển thị countdown.

Thông báo:

```text
Đã gửi yêu cầu OTP thành công.
Vui lòng nhập mã sau khi được người có quyền phê duyệt.
```

## Khi thất bại

- Khôi phục nút gửi.
- Hiển thị SweetAlert hoặc toast lỗi.
- Không chuyển state SENT.
- Không hiển thị form xác nhận khi request chưa tạo thành công.

---

# 18. Chống gửi OTP trùng

Request gửi OTP phải có RequestKey.

Double-click hoặc retry:

- Không tạo nhiều OTP.
- Không gửi nhiều notification.
- Không tạo nhiều approval request.
- Trả lại OtpRequestId hiện tại.

Nếu đã có OTP còn hiệu lực cho cùng context:

```text
Requester
Store
Terminal
OpenMode
Reason
```

thì trả request hiện tại thay vì tạo mới.

---

# 19. Gửi lại OTP

Nút gửi lại:

- Chỉ có sau khi gửi lần đầu thành công.
- Disable trong cooldown.
- Enable khi countdown kết thúc.
- Không dùng sau VERIFIED.
- Không dùng khi LOCKED.
- Không dùng nếu request bị hủy.
- Không dùng nếu Terminal hoặc Reason thay đổi.

Khi gửi lại:

- OTP cũ mất hiệu lực.
- OTP mới có thời hạn mới.
- Không tạo request nghiệp vụ trùng.
- Ghi audit.
- Hiển thị toast thành công.
- Reset số lần sai theo policy đã chốt.

---

# 20. Xác nhận OTP đúng

Khi bấm:

- Disable nút.
- Hiển thị `Đang xác nhận...`.
- Chặn double-click.
- Trim mã.
- Backend xác nhận.

Khi thành công:

1. Hiển thị SweetAlert hoặc toast nổi bật.
2. Có icon success.
3. Không chỉ ghi dòng nhỏ `OTP đã được phê duyệt`.
4. Ẩn:
   - Input OTP.
   - Nút xác nhận.
   - Nút gửi lại.
   - Nút gửi OTP.
5. Clear giá trị OTP trước khi ẩn.
6. Hiển thị:

```text
✓ OTP đã được xác nhận
```

7. Enable `Tiếp tục sang POS`.
8. Khóa Terminal, OpenMode và Reason.
9. Nếu người dùng sửa context thì OTP cũ phải bị vô hiệu hóa.
10. Không lưu OTP trong DOM, LocalStorage hoặc log.

Thông báo:

```text
Xác nhận OTP thành công.
Bạn có thể tiếp tục sang POS.
```

---

# 21. Xác nhận OTP sai

Khi sai:

- Hiển thị SweetAlert hoặc toast lỗi.
- Có icon error.
- Không ẩn form OTP.
- Không enable nút tiếp tục.
- Enable lại nút xác nhận nếu chưa lock.
- Focus lại input.
- Có thể clear mã sai.
- Không tiết lộ OTP đúng.
- Không tiết lộ approver nếu không có quyền.

Thông báo:

```text
Mã OTP không chính xác.
Vui lòng kiểm tra và thử lại.
```

---

# 22. OTP hết hạn

Khi hết hạn:

- Backend từ chối kể cả mã đúng cũ.
- Hiển thị warning.
- Disable xác nhận.
- Clear mã.
- Cho gửi lại theo cooldown.
- OTP cũ không dùng lại.

Thông báo:

```text
Mã OTP đã hết hạn.
Vui lòng gửi lại yêu cầu OTP.
```

---

# 23. Nhập sai quá nhiều lần

Có giới hạn số lần.

Khi vượt giới hạn:

```text
OTP_VERIFICATION_LOCKED
```

UI:

- Disable hoặc ẩn nút xác nhận.
- Không cho tiếp tục.
- Hiển thị thông báo.
- Chỉ cho request mới sau lockout hoặc theo quyền.

Không log OTP.

---

# 24. Reload trang

Sau reload:

## OTP đang chờ

- Không hiện lại nút gửi như chưa gửi.
- Hiển thị trạng thái đang chờ.
- Countdown lấy từ server.

## OTP đã phê duyệt nhưng chưa verify

- Hiển thị form nhập mã.

## OTP VERIFIED

- Ẩn toàn bộ form OTP.
- Hiển thị trạng thái thành công.
- Cho tiếp tục nếu request còn hiệu lực.

## OTP EXPIRED

- Hiển thị hết hạn.
- Cho gửi lại theo rule.

## Context thay đổi

- Vô hiệu hóa OTP cũ.
- Không cho tiếp tục.

Không dùng LocalStorage làm nguồn sự thật.

---

# 25. Bind OTP với nghiệp vụ

OTP ngoài lịch bind tối thiểu:

```text
OtpRequestId
RequesterStaffId
ApproverStaffId
StoreId
TerminalId
OpenMode = OUT_OF_SCHEDULE
ReasonSnapshot hoặc ReasonHash
RequestedAtUtc
ExpiresAtUtc
RequestKey
UsedAtUtc
Status
```

OTP cho Terminal A không dùng cho Terminal B.

OTP cho Reason cũ không dùng cho Reason mới.

OTP chỉ dùng một lần.

---

# 26. Nút Tiếp tục sang POS

Chỉ enable khi:

```text
Terminal hợp lệ
AND
Reason hợp lệ
AND
OTP request tồn tại
AND
OTP đã phê duyệt
AND
OTP đã verify
AND
OTP chưa hết hạn
AND
Không có WorkShift xung đột
```

Khi bấm:

- Disable ngay.
- Hiển thị loading.
- Dùng RequestKey.
- Không gửi hai request mở POS.
- Không dùng exchange code cũ.
- Redirect một lần.
- Nếu lỗi thì hiển thị toast và phục hồi state phù hợp.

Không tin:

```text
otpVerified = true
```

ở client.

Backend phải kiểm tra lại.

---

# 27. Validation lý do mở ngoài lịch

- Bắt buộc.
- Từ 10 đến 500 ký tự.
- Trim khoảng trắng.
- Không chấp nhận chuỗi chỉ có khoảng trắng.
- Không gửi OTP nếu chưa hợp lệ.
- Sau khi OTP VERIFIED, không cho sửa Reason.
- Nếu sửa Reason thì OTP cũ mất hiệu lực.
- Reason phải được lưu snapshot hoặc hash để bind OTP.

---

# 28. Validation mã OTP

- Bắt buộc khi verify.
- Trim khoảng trắng.
- Validate format theo rule hiện tại.
- Không submit nếu rỗng.
- Không log.
- Không lưu lâu dài trên client.
- Không cho verify sau expiration.
- Không cho dùng lại sau UsedAtUtc.

---

# 29. Nhiều nhân viên cùng ca

## Hai nhân viên, hai Terminal

```text
A → WorkShift A → POS 01
B → WorkShift B → POS 02
```

Cho phép.

Mỗi người có:

- Opening cash riêng.
- Orders riêng.
- Payments riêng.
- Cash drawer riêng.
- Closing riêng.
- Reconciliation riêng.

---

## Hai nhân viên, một Terminal

Không cho hai WorkShift active.

Dùng mô hình:

```text
POS 01
└─ WorkShift thuộc A
   ├─ Current Operator A
   └─ Current Operator B
```

B xác thực bằng PIN cá nhân.

Mỗi thao tác lưu StaffId thực tế.

---

## Nhiều nhân viên hơn số Terminal

Không cần mỗi người có Terminal.

Ví dụ:

```text
A: Thu ngân POS 01
B: Thu ngân POS 02
C: Pha chế
D: Bếp
E: Giao món
```

Chỉ Responsible Staff mở WorkShift.

---

# 30. Chặn một nhân viên mở nhiều WorkShift

Mặc định:

```text
1 Responsible Staff
└─ Tối đa 1 WorkShift active
```

Các trạng thái active:

```text
OPEN
CLOSING
EXPIRED_PENDING_CLOSE
```

Lỗi:

```text
STAFF_ALREADY_HAS_ACTIVE_WORKSHIFT
```

Nếu có ngoại lệ quản lý:

- Permission riêng.
- OTP hoặc manager PIN.
- Reason bắt buộc.
- Audit.
- StoreScope.
- Không bật mặc định.

---

# 31. Tiếp tục WorkShift sau mất kết nối

Trường hợp:

- Đóng trình duyệt.
- Refresh.
- Mất điện.
- Mất mạng.
- POS crash.
- Exchange code cũ hết hạn.

Nếu Terminal có WorkShift active của chính nhân viên:

```text
Tiếp tục phiên đang mở
```

- Không tạo WorkShift mới.
- Không nhập lại opening cash.
- Phát exchange code mới.
- Vẫn WorkShiftId cũ.

Nếu thuộc người khác:

```text
ACTIVE_SHIFT_OWNED_BY_ANOTHER_STAFF
```

---

# 32. Bàn giao

Không sửa ResponsibleStaffId trực tiếp.

Luồng:

```text
A yêu cầu bàn giao
        ↓
Dừng giao dịch mới
        ↓
Kiểm đếm
        ↓
Tạo cash snapshot
        ↓
Đóng WorkShift A
        ↓
Giải phóng Terminal
        ↓
B mở WorkShift mới
```

WorkShift A và B tách riêng.

---

# 33. Đóng WorkShift

```text
Yêu cầu đóng
        ↓
Status = CLOSING
        ↓
Khóa giao dịch mới
        ↓
Đợi request hợp lệ đang xử lý
        ↓
Nhập tiền thực tế
        ↓
Tính chênh lệch
        ↓
Commit transaction
        ↓
Status = CLOSED
        ↓
Revoke session
        ↓
Giải phóng Terminal
        ↓
Gửi SignalR sau commit
```

Không báo thành công trước commit.

---

# 34. Quản lý đóng thay

Chỉ người có permission.

Yêu cầu:

- Reason bắt buộc.
- OTP hoặc manager PIN nếu policy yêu cầu.
- Ghi người kiểm đếm.
- Ghi người phê duyệt.
- Ghi cash.
- Ghi difference.
- Audit.
- Có thể chuyển RECONCILIATION_REQUIRED.
- Chỉ giải phóng sau cash snapshot.

Lỗi:

```text
WORKSHIFT_FORCE_CLOSE_FORBIDDEN
FORCE_CLOSE_REASON_REQUIRED
CASH_SNAPSHOT_REQUIRED
HANDOVER_REQUIRED
```

---

# 35. WorkShift hết hạn

Mở ngoài lịch tối đa 6 giờ.

Khi hết:

```text
OPEN
→ EXPIRED_PENDING_CLOSE
```

Quy tắc:

- Không nhận Order mới.
- Không nhận Payment mới.
- Terminal vẫn khóa.
- Hiển thị yêu cầu chốt.
- Responsible Staff vào được màn hình đóng.
- Manager có thể đóng thay.
- Không tự ghi counted cash.
- Không tự CLOSED.

---

# 36. RECONCILIATION_REQUIRED

Giữ nguyên:

- WorkShiftId.
- StoreId.
- TerminalId.
- ResponsibleStaffId.
- Orders.
- Payments.
- Opening cash.
- Counted cash.
- Difference.
- Audit.

Không nhận giao dịch mới.

Chỉ giải phóng Terminal sau khi:

- Sales stopped.
- Cash snapshot created.
- Tiền vật lý được tách hoặc bàn giao.
- Có xác nhận hợp lệ.

---

# 37. Current Operator

## Đổi Operator

```text
POS thuộc WorkShift A
        ↓
B chọn Đổi người thao tác
        ↓
B nhập PIN
        ↓
Backend xác thực
        ↓
Kiểm tra account
        ↓
Kiểm tra StaffScope
        ↓
Kiểm tra permission
        ↓
Current Operator = B
```

Không tạo WorkShift mới.

Không đổi Responsible Staff.

---

# 38. Dữ liệu người thao tác

Tùy entity hiện có, bổ sung hoặc ánh xạ:

```text
CreatedByStaffId
UpdatedByStaffId
PaidByStaffId
CancelledByStaffId
RefundedByStaffId
ApprovedByStaffId
PerformedByStaffId
```

Luôn giữ:

```text
WorkShiftId
TerminalId
StoreId
```

Không tạo cột trùng nghĩa nếu model đã có.

---

# 39. Thao tác nhạy cảm

Cân nhắc manager approval cho:

- Hủy Order.
- Hoàn tiền.
- Giảm giá vượt ngưỡng.
- Sửa Payment.
- Mở cash drawer không kèm giao dịch.
- Đóng thay.
- Bàn giao cưỡng chế.
- Chuyển Terminal.
- Override Terminal.
- Mở ngoài lịch.

Audit:

```text
PerformedByStaffId
ApprovedByStaffId
Reason
PerformedAtUtc
WorkShiftId
TerminalId
StoreId
RequestKey
```

---

# 40. Bảo mật PIN và OTP

- Không lưu plain text.
- Hash PIN an toàn.
- OTP có expiration.
- OTP dùng một lần.
- Có cooldown.
- Giới hạn sai.
- Lockout.
- Không log.
- Không trả về client.
- Bind request context.
- Không dùng chung PIN cá nhân và PIN quản lý nếu hệ thống đã tách.

---

# 41. Chống double-click và idempotency

Áp dụng cho:

- Mở WorkShift.
- Đóng WorkShift.
- Bàn giao.
- Đăng ký Terminal.
- Chuyển Terminal.
- Payment.
- Hủy Order.
- Gửi OTP.
- Verify OTP.
- Đồng bộ offline.

Dữ liệu dedup tối thiểu:

```text
RequestKey
ActionName
StaffId
StoreId
TerminalId
RequestBodyHash
Status
ReferenceId
ResponseBody
CreatedAt
ExpiredAt
```

Quy tắc:

```text
Chưa tồn tại
→ Xử lý

PROCESSING
→ Không tạo mới

SUCCESS
→ Trả lại kết quả cũ

Cùng key khác payload
→ REQUEST_KEY_PAYLOAD_MISMATCH
```

UI disable chỉ là lớp hỗ trợ.

Backend và database vẫn phải chống trùng.

---

# 42. Concurrency và database constraint

Phải ngăn hai request cùng mở một Terminal.

Không chỉ làm:

```text
Check empty
→ Insert
```

Yêu cầu:

- Transaction.
- Recheck trong transaction.
- Unique filtered index hoặc giải pháp tương đương.
- Chuyển SQL unique violation thành business error.
- Có thể dùng rowversion hoặc locking phù hợp.
- Không dùng lock in-memory làm bảo vệ chính.

Ràng buộc logic:

```text
Một TerminalId
→ Tối đa một WorkShift OPEN/CLOSING/EXPIRED_PENDING_CLOSE
```

```text
Một ResponsibleStaffId
→ Tối đa một WorkShift OPEN/CLOSING/EXPIRED_PENDING_CLOSE
```

Inspect dữ liệu cũ trước migration.

Không tự xóa dữ liệu tài chính.

---

# 43. Order và Payment offline

Client offline lưu tối thiểu:

```text
ClientRequestId
OriginalWorkShiftId
TerminalId
StoreId
OperatorStaffId
CreatedAtDevice
DeviceInstallationId
PayloadHash
```

Ví dụ:

```text
08:00 A tạo Order offline
09:00 A đóng WorkShift
09:05 B mở WorkShift mới
09:10 Order A đồng bộ
```

Order vẫn thuộc WorkShift A.

Không gắn sang WorkShift B.

Backend:

- Chống duplicate.
- Verify WorkShift gốc.
- Verify Terminal và Store.
- Verify Operator.
- Giữ device time và server time riêng.
- Audit giao dịch muộn.
- Có thể chuyển phiên cũ sang reconciliation.
- Không đổi WorkShiftId của Order.

Lỗi:

```text
OFFLINE_WORKSHIFT_NOT_FOUND
OFFLINE_WORKSHIFT_MISMATCH
OFFLINE_TERMINAL_MISMATCH
OFFLINE_DUPLICATE_REQUEST
OFFLINE_TRANSACTION_REQUIRES_RECONCILIATION
```

---

# 44. DeviceInstallationId và Terminal giả

Nếu phù hợp model, bổ sung:

```text
DeviceInstallationId
```

Mục tiêu:

- Phân biệt Terminal logic với installation thực tế.
- Ngăn tạo nhiều Terminal ảo trên một máy.
- Revoke thiết bị.
- Hỗ trợ thay máy.

Không dùng fingerprint xâm phạm hoặc quá cứng.

Dùng token installation do backend cấp:

- Random.
- Revoke được.
- Không nhạy cảm.
- Có RegisteredAtUtc.
- Có LastSeenAtUtc.

Mặc định:

```text
1 active installation
→ 1 active Terminal binding
```

---

# 45. Đăng ký Terminal

Luồng:

```text
Đăng ký Terminal
        ↓
Nhập tên
        ↓
Gửi OTP
        ↓
Người có POS.WorkShift.OverrideTerminal
và đúng StoreScope phê duyệt
        ↓
Verify OTP
        ↓
Tạo Terminal
```

Tên:

- Không rỗng.
- Trim.
- Giới hạn độ dài.
- Không chứa dữ liệu nhạy cảm.
- Không trùng tên active trong cùng Store nếu policy yêu cầu.
- Cảnh báo tên chung chung như Test, ABC, Máy mới.

OTP đăng ký Terminal:

- Expiration.
- Cooldown.
- One-time.
- Bind requester.
- Bind approver.
- Bind Store.
- Bind Terminal payload.
- Bind RequestKey.

---

# 46. Terminal hỏng giữa phiên

## Mặc định

```text
Đóng hoặc force-close phiên cũ
        ↓
Kiểm đếm
        ↓
Mở WorkShift mới ở Terminal khác
```

## Nâng cao

Chỉ làm transfer WorkShift nếu thật sự cần.

Điều kiện:

- Cùng Store.
- Terminal đích active.
- Terminal đích trống.
- Permission.
- Reason.
- OTP hoặc manager approval.
- Revoke Terminal cũ.
- Audit.
- Không mất Order/Payment.
- Không đổi WorkShiftId.

Không chuyển giữa Store.

---

# 47. Chuyển Terminal sang Store khác

Không sửa trực tiếp StoreId nếu có lịch sử.

Luồng:

```text
Không có WorkShift active
        ↓
Vô hiệu hóa assignment cũ
        ↓
Tạo assignment mới
        ↓
Ghi lịch sử
```

Có thể dùng:

```text
TerminalAssignmentHistory
- TerminalId
- FromStoreId
- ToStoreId
- ChangedByStaffId
- Reason
- ChangedAtUtc
```

Order và WorkShift cũ giữ StoreId ban đầu.

---

# 48. Khóa màn hình POS

Sau thời gian không hoạt động:

```text
POS Lock Screen
        ↓
Nhập PIN
        ↓
Khôi phục Current Operator
```

Khóa màn hình:

- Không đóng WorkShift.
- Không đổi Responsible Staff.
- Không reset opening cash.
- Không mất Order.
- Không khóa giữa request payment đang xử lý.

---

# 49. StaffShift kết thúc nhưng WorkShift còn giao dịch

Không tự đóng đúng phút StaffShift kết thúc.

Có thể còn:

- Order đang xử lý.
- Payment đang xử lý.
- Offline data.
- Chưa kiểm đếm.

Luồng:

```text
StaffShift kết thúc
        ↓
Cảnh báo sắp bàn giao
        ↓
Hoàn tất giao dịch đang xử lý
        ↓
Kiểm đếm
        ↓
Đóng hoặc bàn giao
```

Grace period cần cấu hình hoặc BA xác nhận, ví dụ 15–30 phút.

---

# 50. SignalR và session

Sự kiện cân nhắc:

```text
WorkShiftOpened
WorkShiftClosing
WorkShiftClosed
WorkShiftExpired
WorkShiftForceClosed
OperatorChanged
TerminalLocked
TerminalReleased
TerminalDisabled
SessionRevoked
OfflineTransactionReconciled
```

SignalR gửi đúng scope:

- Store.
- Terminal.
- WorkShift.
- Responsible Staff.
- Current Operator khi cần.

Khi đóng hoặc force-close:

- Revoke session.
- POS cũ khóa giao dịch.
- Không cho client cũ tạo Order.
- Gửi SignalR sau database commit.

---

# 51. Permission

Kiểm tra và tái sử dụng permission hiện có.

Các permission tương đương:

```text
POS.Use
POS.WorkShift.Open
POS.WorkShift.Close
POS.WorkShift.View
POS.WorkShift.ForceClose
POS.WorkShift.Handover
POS.WorkShift.OverrideSchedule
POS.WorkShift.OverrideTerminal
POS.Operator.Switch
POS.Operator.ApproveSensitiveAction
POS.Terminal.Register
POS.Terminal.Disable
POS.Terminal.Transfer
```

Không seed trùng Code.

Không chỉ ẩn UI.

Backend authorize lại.

StaffScope:

- Store Manager chỉ đúng Store.
- Role khác không sửa StoreId để mở rộng quyền.
- SystemAdmin global scope nếu đúng thiết kế hiện tại.

---

# 52. Audit log

Ghi cho:

- Đăng ký Terminal.
- Disable Terminal.
- Mở WorkShift.
- Resume.
- Đổi Operator.
- Đóng.
- Force-close.
- Handover.
- Transfer.
- Override schedule.
- Override Terminal.
- Hủy.
- Refund.
- Discount nhạy cảm.
- Offline late sync.
- OTP send.
- OTP resend.
- OTP verify.
- OTP lockout.

Tối thiểu:

```text
Action
StoreId
TerminalId
WorkShiftId
ResponsibleStaffId
PerformedByStaffId
ApprovedByStaffId
Reason
OldValue
NewValue
RequestKey
IpAddress
DeviceInstallationId
CreatedAtUtc
```

Không log:

- PIN.
- OTP.
- Access token.
- Exchange code.
- Password.
- Sensitive payment data.

---

# 53. Error code chuẩn

Ít nhất:

```text
TERMINAL_NOT_FOUND
TERMINAL_INACTIVE
TERMINAL_STORE_MISMATCH
TERMINAL_ALREADY_HAS_OPEN_SHIFT
STAFF_ALREADY_HAS_ACTIVE_WORKSHIFT
TERMINAL_DEVICE_MISMATCH
STORE_INACTIVE
STAFF_SCOPE_FORBIDDEN
ACTIVE_SHIFT_OWNED_BY_ANOTHER_STAFF
WORKSHIFT_ALREADY_CLOSING
EXISTING_WORKSHIFT_REQUIRES_CLOSE
EXPIRED_SHIFT_REQUIRES_CLOSE
HANDOVER_REQUIRED
CASH_SNAPSHOT_REQUIRED
WORKSHIFT_FORCE_CLOSE_FORBIDDEN
FORCE_CLOSE_REASON_REQUIRED
REQUEST_ALREADY_PROCESSING
REQUEST_KEY_PAYLOAD_MISMATCH
OTP_INVALID
OTP_EXPIRED
OTP_ALREADY_USED
OTP_VERIFICATION_LOCKED
OTP_CONTEXT_MISMATCH
OPERATOR_NOT_AUTHORIZED
OFFLINE_WORKSHIFT_NOT_FOUND
OFFLINE_WORKSHIFT_MISMATCH
OFFLINE_TERMINAL_MISMATCH
OFFLINE_DUPLICATE_REQUEST
OFFLINE_TRANSACTION_REQUIRES_RECONCILIATION
```

Mỗi code cần:

- HTTP status.
- Message tiếng Việt.
- Điều kiện phát sinh.
- Client action phù hợp.

Không trả stack trace hoặc SQL error.

---

# 54. Giao diện StaffHub

Hiển thị Terminal:

```text
Đang trống
Đang được bạn sử dụng
Đang được nhân viên khác sử dụng
Đang chốt két
Đã hết hạn, chờ đóng
Không hoạt động
```

Quy tắc:

- Phiên của chính người dùng: `Tiếp tục phiên`.
- Terminal bận bởi người khác: disable.
- Không lộ dữ liệu nhạy cảm.
- Nút đăng ký Terminal theo permission.
- Disable trong request.
- Toast hoặc modal đúng z-index.
- Không redirect sang lỗi kỹ thuật.

Phần OTP phải tuân thủ state machine đã nêu.

---

# 55. Giao diện POS

Hiển thị:

- Store.
- Terminal.
- WorkShift.
- Responsible Staff.
- Current Operator.
- Status.
- OpenedAt.
- ExpiresAt.
- Cảnh báo sắp hết hạn.

Chức năng:

```text
Đổi người thao tác
Khóa màn hình
Tiếp tục phiên
Bàn giao
Đóng WorkShift
```

Khi nhận session revoke:

- Khóa ngay.
- Không giao dịch mới.
- Không tự tạo WorkShift.
- Chuyển về StaffHub hoặc màn hình phù hợp.

---

# 56. Các file và thành phần bắt buộc inspect

Không được chỉ sửa View.

Inspect:

```text
StaffHub View
StaffHub JavaScript
StaffHub Controller
WorkShift Controller
WorkShift Service
WorkShift Repository
OTP Service
OTP Repository
Exchange Code Service
POS Authentication
POS WorkShift Controller
POS WorkShift Service
Current WorkShift Query
Order Service
Payment Service
Request Deduplication
SignalR Hub và client
Cookie
Session
LocalStorage
Database schema
Indexes
Migrations
Tests
```

Không bịa tên file.

Ghi đúng path thực tế sau khi inspect.

---

# 57. Test P0 bắt buộc

## Test P0-1 – Phiên cũ đã CLOSED

1. A mở WorkShift.
2. A kiểm đếm và đóng.
3. DB xác nhận CLOSED.
4. A không có lịch.
5. A mở ngoài lịch.
6. OTP hợp lệ.
7. Tạo WorkShift mới.
8. WorkShiftId khác.
9. POS hiển thị giờ mới.
10. Không báo hết hạn.

## Test P0-2 – 6 giờ

1. Mở lúc 12:45.
2. ExpiresAt là 18:45.
3. Trước 18:45 nhận giao dịch.
4. Sau 18:45 chuyển EXPIRED_PENDING_CLOSE.

## Test P0-3 – Phiên đang OPEN

1. A có OPEN.
2. A mở lần nữa.
3. Không tạo mới.
4. Trả RESUME_EXISTING_WORKSHIFT.
5. Không nhập opening cash lại.

## Test P0-4 – Phiên EXPIRED

1. A có EXPIRED_PENDING_CLOSE.
2. A mở mới.
3. Từ chối.
4. Yêu cầu chốt.
5. Không tạo mới.

## Test P0-5 – LocalStorage cũ

1. Client có WorkShiftId cũ.
2. StaffHub tạo phiên mới.
3. POS dùng WorkShiftId từ exchange code.
4. Client cũ không ghi đè.

## Test P0-6 – RequestKey cũ

1. Phiên cũ đóng.
2. Request mới.
3. RequestKey mới.
4. Không trả ResponseBody cũ.

## Test P0-7 – UTC

1. Lưu UTC.
2. So sánh UTC.
3. Hiển thị UTC+7.
4. Không double-convert.

## Test P0-8 – Race

1. Hai request cùng Terminal.
2. Chỉ một tạo phiên.
3. Request kia nhận lỗi.
4. DB chỉ có một active.

---

# 58. Test OTP bắt buộc

## OTP-1 – Gửi thành công

- Disable ngay.
- Chỉ một request.
- Ẩn hoặc disable nút gửi.
- Toast success.
- Hiển thị input.

## OTP-2 – Double-click

- Một OtpRequestId.
- Một notification.
- Không duplicate.

## OTP-3 – Verify đúng

- SweetAlert/toast success.
- Ẩn form OTP.
- Hiển thị trạng thái verified.
- Enable tiếp tục.

## OTP-4 – Verify sai

- Toast error.
- Không cho tiếp tục.
- Giữ form.
- Focus input.

## OTP-5 – Expired

- Backend từ chối.
- Warning.
- Gửi lại theo cooldown.

## OTP-6 – Resend

- Disable trước cooldown.
- Enable sau cooldown.
- OTP cũ mất hiệu lực.
- Audit.

## OTP-7 – Reload

- State lấy từ server.
- Countdown đúng.
- VERIFIED vẫn ẩn form.

## OTP-8 – Context thay đổi

- Đổi Terminal hoặc Reason.
- OTP cũ vô hiệu hóa.
- Phải verify lại.

## OTP-9 – Brute force

- Lock sau giới hạn.
- Không log OTP.
- Không lộ OTP đúng.

---

# 59. Test nghiệp vụ tổng thể

## Nhiều Terminal

- A mở POS 01.
- B mở POS 02.
- Cả hai thành công.

## Trùng Terminal

- A và B cùng mở POS 01.
- Một thành công.
- Một bị từ chối.

## Một Staff nhiều WorkShift

- A đang active.
- A mở Terminal khác.
- Bị STAFF_ALREADY_HAS_ACTIVE_WORKSHIFT.

## Resume

- A mất kết nối.
- A quay lại.
- Dùng WorkShift cũ.
- Không nhập opening cash.

## Chiếm Terminal

- B không chiếm phiên A.
- Sửa URL vẫn bị từ chối.

## Current Operator

- A Responsible.
- B nhập PIN.
- B tạo Order.
- Order ghi B.
- WorkShift vẫn A.

## Closing

- CLOSING khóa giao dịch mới.
- CLOSED giải phóng Terminal.

## Expired

- EXPIRED khóa Terminal.
- Chỉ giải phóng sau chốt.

## Offline

- Order offline giữ WorkShift gốc.
- Không sang phiên mới.

## Permission

- Không vượt StaffScope.
- Không force-close nếu thiếu quyền.

## PIN/OTP

- Cooldown.
- Lockout.
- Không log secret.

---

# 60. Migration và dữ liệu cũ

Trước migration:

- Tìm Terminal có nhiều active WorkShift.
- Tìm Staff có nhiều active WorkShift.
- Kiểm tra status lưu string hay int.
- Kiểm tra index.
- Kiểm tra FK.
- Kiểm tra Terminal Store relation.
- Kiểm tra dữ liệu time UTC/local.
- Kiểm tra WorkShift thiếu ExpiresAtUtc.
- Kiểm tra WorkShift CLOSED nhưng ClosedAt null.
- Kiểm tra WorkShift active nhưng Terminal inactive.

Phải cung cấp:

1. Query phát hiện.
2. Kế hoạch xử lý.
3. Migration.
4. Rollback plan.
5. Không tự xóa dữ liệu tài chính.
6. Không tự CLOSED nếu chưa có căn cứ.

---

# 61. Không được thực hiện

Không được:

- Biến WorkShift thành chấm công.
- Tính lương.
- Cho hai active WorkShift cùng Terminal.
- Cho một Staff mở nhiều WorkShift mặc định.
- Chỉ chống double-click bằng JS.
- Tin StaffId hoặc StoreId từ client.
- Chuyển Order offline sang phiên mới.
- Thay ResponsibleStaffId trực tiếp để bàn giao.
- Tự CLOSED khi hết hạn chưa kiểm đếm.
- Xóa WorkShift cũ để giải phóng.
- Tạo Terminal giả.
- Lưu PIN hoặc OTP plain text.
- Log token, OTP, PIN, exchange code.
- Trả thành công trước commit.
- Sửa module không liên quan.
- Viết lại toàn bộ hệ thống.
- Bịa tên file.
- Tuyên bố hoàn thành khi chưa chạy test.

---

# 62. Quy trình làm việc Codex bắt buộc

## Bước 1 – Inspect

- Liệt kê file thực tế.
- Xác định luồng từ StaffHub tới POS.
- Xác định WorkShift query.
- Xác định OTP state.
- Xác định Exchange Code.
- Xác định nơi lưu session.
- Xác định schema.
- Xác định test hiện có.

## Bước 2 – Root cause P0

Báo cáo:

- WorkShiftId nào được tạo.
- WorkShiftId nào POS load.
- Tại sao khác nhau.
- Status.
- OpenedAt.
- ExpiresAt.
- Session/cache.
- RequestKey.
- Transaction.

## Bước 3 – Kế hoạch sửa

Chia theo:

```text
Database
Backend
StaffHub
POS
SignalR
Tests
Migration
```

## Bước 4 – Sửa P0

- Code.
- Migration nếu cần.
- Test.
- Chạy test.

## Bước 5 – Sửa OTP

- State machine.
- UI.
- Backend.
- Security.
- Test.

## Bước 6 – P2

- Terminal.
- Current Operator.
- Bàn giao.
- Offline.
- Audit.
- Permission.

## Bước 7 – Báo cáo

Không bỏ qua phần chưa làm.

---

# 63. Kết quả đầu ra bắt buộc

## Phần 1 – Hiện trạng

- Luồng hiện tại.
- Entity liên quan.
- Validation.
- Điểm đúng.
- Điểm sai.
- Lỗ hổng.

## Phần 2 – Root cause P0

Nêu cụ thể:

- Lỗi ở query, exchange code, session, cache, request dedup, transaction hay timezone.
- Vì sao POS lấy giờ cũ.
- Vì sao vừa OPEN vừa expired.
- Dữ liệu DB trước và sau.
- Không ghi chung chung.

## Phần 3 – Thiết kế sau refactor

Sơ đồ:

```text
Store
→ Terminal
→ WorkShift
→ Responsible Staff
→ Current Operator
→ Order/Payment
```

State machine WorkShift và OTP.

## Phần 4 – Danh sách file sửa

Với mỗi file:

- Path thật.
- Class.
- Method.
- Lỗi.
- Sửa gì.
- Tác động.
- Test.

## Phần 5 – Database

- Entity.
- Columns.
- Index.
- Constraint.
- Migration.
- Data cleanup query.
- Rollback.

## Phần 6 – Code

- Controller.
- Service.
- Repository.
- DTO/ViewModel.
- JavaScript.
- View.
- Authorization.
- Audit.
- SignalR.
- Dedup.

## Phần 7 – Error codes

- Code.
- HTTP status.
- Message.
- Điều kiện.
- Client action.

## Phần 8 – Tests

- Unit.
- Integration.
- Concurrency.
- Idempotency.
- UTC.
- OTP UI.
- Authorization.
- Offline.

## Phần 9 – Chưa hoàn thành

- Phần cần BA xác nhận.
- Phần chưa đủ dữ liệu.
- Phần để phase sau.

---

# 64. Các quyết định cần BA xác nhận

Nếu code hiện tại chưa quy định rõ, không tự quyết định âm thầm.

Nêu riêng:

1. Một Staff có bao giờ được sở hữu nhiều WorkShift active không?
   - Đề xuất: Không.

2. Có hỗ trợ Current Operator bằng PIN không?
   - Đề xuất: Có.

3. RECONCILIATION_REQUIRED giải phóng Terminal khi nào?
   - Đề xuất: Sau cash snapshot và handover.

4. Máy hỏng có transfer WorkShift không?
   - Đề xuất ban đầu: Đóng phiên cũ, mở phiên mới.

5. Có áp dụng DeviceInstallationId không?
   - Đề xuất: Có nếu Terminal đại diện máy thật.

6. Auto-lock sau bao lâu?
   - Cần cấu hình.

7. Grace period sau StaffShift?
   - Đề xuất: 15–30 phút.

8. Thao tác nào cần manager approval?
   - Hủy.
   - Refund.
   - Discount lớn.
   - Force-close.
   - Transfer.
   - Override.

---

# 65. Tiêu chí nghiệm thu cuối cùng

Chỉ được xem là hoàn thành khi:

1. Một Store có nhiều Terminal.
2. Hai Staff mở hai Terminal khác nhau.
3. Không có hai active WorkShift cùng Terminal.
4. Không có một Responsible Staff sở hữu nhiều active WorkShift trái rule.
5. Mở ngoài lịch sau OTP thành công.
6. WorkShift mới không hết hạn ngay.
7. Giờ mở mới đúng.
8. ExpiresAt đúng 6 giờ.
9. Phiên CLOSED cũ không được lấy lại.
10. Phiên active cũ chặn mở mới.
11. Exchange code bind đúng WorkShiftId.
12. Session cũ không ghi đè.
13. Không vừa báo success vừa expired.
14. Double-click không tạo trùng.
15. Gửi OTP lần đầu thành công thì nút gửi bị ẩn hoặc disable.
16. Verify đúng có SweetAlert hoặc toast rõ ràng.
17. Verify sai có thông báo lỗi rõ ràng.
18. VERIFIED thì form OTP ẩn.
19. Continue chỉ enable khi backend xác nhận.
20. Resend có cooldown.
21. Reload giữ state từ server.
22. OTP không dùng lại.
23. OTP không dùng cho context khác.
24. Nhân viên resume phiên cũ.
25. Không nhập opening cash lại khi resume.
26. Current Operator ghi đúng người thao tác.
27. Responsible Staff không đổi trái phép.
28. Bàn giao tạo phiên mới.
29. Expired không tự CLOSED.
30. Terminal chỉ giải phóng sau điều kiện tài chính.
31. Offline giữ WorkShift gốc.
32. Database chống race.
33. StaffScope server-side.
34. PIN, OTP, token không lộ.
35. Audit đầy đủ.
36. Test liên quan chạy thành công.
37. Không có regression đối với mở POS bình thường.
38. Không biến WorkShift thành chấm công hoặc tính lương.

---

# 66. Yêu cầu cuối cùng cho Codex

Hãy ưu tiên tính đúng nghiệp vụ và tính toàn vẹn dữ liệu hơn việc sửa nhanh giao diện.

Bắt buộc:

- Inspect trước.
- Tìm root cause trước.
- Sửa P0 trước.
- Test P0 trước.
- Sau đó mới refactor OTP.
- Không che lỗi.
- Không đoán.
- Không bịa file.
- Không bỏ qua transaction.
- Không bỏ qua database constraint.
- Không bỏ qua StaffScope.
- Không bỏ qua audit.
- Không tuyên bố hoàn thành khi chưa kiểm thử.

Giải pháp cuối phải đơn giản, dễ bảo trì, phù hợp quy mô CafeChain nhưng vẫn bảo đảm:

- Đúng WorkShift.
- Đúng Terminal.
- Đúng nhân viên.
- Đúng thời gian.
- Đúng Order.
- Đúng Payment.
- Đúng trách nhiệm két.
