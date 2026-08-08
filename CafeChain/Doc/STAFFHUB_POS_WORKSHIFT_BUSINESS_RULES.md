# Nghiệp vụ chuẩn StaffHub, POS và WorkShift

> Hướng dẫn thao tác kiểm thử: [STAFFHUB_USER_BUSINESS_FLOWS.md](./STAFFHUB_USER_BUSINESS_FLOWS.md).
>
> Hướng dẫn terminal dành cho người dùng: [POS_TERMINAL_USER_GUIDE.md](./POS_TERMINAL_USER_GUIDE.md).

## 1. Ranh giới dữ liệu

- `Shift` là mẫu giờ; `StaffShift` là lịch dự kiến; `WorkShift` là phiên chịu trách nhiệm POS/két.
- Mở POS không tạo chấm công, tăng ca, lương hoặc `StaffShift` giả.
- StaffHub quyết định terminal, lịch, lý do và OTP. Tiền đầu phiên luôn nhập ở POS.
- Mọi thời điểm UTC trên wire (`StartTimeUtc`, `AutoCloseAtUtc`, `ServerNowUtc`...) phải là ISO-8601 có `Z`. SQL `datetime2` được gắn `DateTimeKind.Utc` trước khi serialize; client vẫn parse phòng thủ chuỗi cũ thiếu offset.

## 2. Hai nhánh mở ca và Exchange Code

AppLauncher chỉ khởi động Vite rồi điều hướng tới `/StaffHub?openPos=1`; nếu đã biết terminal thì thêm `terminalId`. StaffHub tự mở modal. POS không giữ một form lý do/OTP thứ hai.

### 2.1 Ca bình thường

`WITHIN_SCHEDULE` và không mở sớm:

1. StaffHub preview và phát context `OPEN_WORKSHIFT` chưa có `WorkShiftId`.
2. POS exchange, nhập tiền đầu phiên và gọi `POST /api/v1/pos/shifts/open`.
3. WorkShift được commit tại POS. `shiftId` trong response open là nguồn sự thật.

### 2.2 Ca sớm, trễ và ngoài lịch

1. StaffHub xử lý đầy đủ lý do/OTP theo quyết định backend.
2. `IssuePosToken` chỉ phát exchange context có TTL; không tạo WorkShift và không lưu `StartingCash=0`.
3. Exchange có purpose `OPEN_WORKSHIFT`, `WorkShiftId=null` và `RequiresOpeningCash=true`.
4. `POST /api/v1/pos/shifts/open` revalidate identity, permission, StaffScope, Terminal, lịch và approval/OTP rồi mới tạo WorkShift, bind session và consume context trong transaction.
5. Người dùng rời POS trước khi bấm **Xác nhận mở ca** thì không có WorkShift business được tạo.

Nếu mở trực tiếp URL POS, exchange hết hạn hoặc context bình thường đã đổi thành sớm/trễ/ngoài lịch trước lúc submit, backend trả `STAFFHUB_OPEN_REQUIRED` + `OPEN_STAFFHUB`; React quay về `/StaffHub?openPos=1&terminalId=...`.

Không tạo trạng thái WorkShift pending hoặc pre-open mới. Exchange resume một ca đã hoạt động dùng `RESUME_WORKSHIFT`, có `WorkShiftId` và `RequiresOpeningCash=false`.

## 3. OpenContext

- `WITHIN_SCHEDULE`: đúng lịch không cần lý do/OTP; mở sớm vẫn hoàn tất bước mở tại StaffHub.
- `LATE_FOR_SCHEDULE`: trễ đến 15 phút được audit; trên 15 nhưng dưới 30 phút cần lý do; từ 30 đến 45 phút cần Manager và có thể duyệt theo lịch; trên 45 phút chỉ được từ chối hoặc chuyển ngoài lịch.
- `OUTSIDE_SCHEDULE`: cần `POS.WorkShift.OpenOutsideSchedule`, lý do 10–500 ký tự và OTP.
- Ngoài lịch không tạo `StaffShift`; `AutoCloseAtUtc = StartTimeUtc + 6 giờ`.
- Hai nhân viên có thể có WorkShift độc lập trên hai terminal trống trong cùng cửa hàng.

## 4. Nguồn sự thật WorkShiftId

- Ca bình thường: ID từ response `POST open` sau commit.
- Mọi ca mới: ID chỉ xuất hiện sau commit authoritative tại `POST /api/v1/pos/shifts/open`.
- `GET current`, polling và SignalR chỉ được chấp nhận nếu khớp ID có thẩm quyền vừa mở. Local state/response cũ không được ghi đè.
- Query active chỉ gồm `OPEN`, `CLOSING`, `EXPIRED_PENDING_CLOSE`, sắp `StartTimeUtc DESC, ShiftId DESC`.
- `CLOSED` và `RECONCILIATION_REQUIRED` không bao giờ là phiên current/khóa mở mới.

Response mở/xác nhận tiền tối thiểu có `resultCode`, `shiftId`, `terminalId`, `startTimeUtc`, `autoCloseAtUtc`, `serverNowUtc`, `requiresOpeningCash`.

### 4.1 Ma trận quyền dùng terminal và Current Operator

| Trạng thái | Kết quả |
|---|---|
| Chính requester có WorkShift `OPEN` | Resume đúng `WorkShiftId`; không tạo ca và không nhập lại tiền đầu ca. |
| Người khác chọn terminal đang `OPEN` | Không phát exchange/token mới; trả tên người chịu trách nhiệm và `SWITCH_CURRENT_OPERATOR`. |
| Người khác chọn terminal trống cùng StoreId | Được mở WorkShift riêng vì mỗi terminal là một két riêng. |
| WorkShift ở store khác | Không ảnh hưởng; lookup terminal, session và dữ liệu luôn scope theo StoreId. |
| Terminal `CLOSING`/`EXPIRED_PENDING_CLOSE` | Không bán, join hoặc mở mới; phải kiểm đếm/chốt két. |

`WorkShift.UserId` là người chịu trách nhiệm két cho đến khi đóng. **Đổi Current Operator** chỉ cập nhật `CurrentOperatorStaffId` để bàn giao thao tác trên cùng quầy mà không đóng/mở lại két. Order, Payment và audit mới ghi nhân viên thực tế thao tác, nhưng không chuyển trách nhiệm két, tiền đầu ca, trách nhiệm tài chính hoặc `WorkShiftId`. PIN là mã cá nhân; backend vẫn kiểm account active, `POS.Operator.Switch` và StaffScope. Trang Két, header bán hàng và các tab cùng terminal đồng bộ tên người thao tác qua SignalR.

## 5. Idempotency và concurrency

- Retry cùng RequestKey/payload trả đúng WorkShift của request trước.
- Cùng key khác payload trả `DUPLICATE_REQUEST`.
- Transaction serializable, rowversion và `UX_WorkShifts_ActiveStaff`/`UX_WorkShifts_ActiveTerminal` ngăn race.
- Staff/terminal có `OPEN`, `CLOSING`, `EXPIRED_PENDING_CLOSE` đều khóa mở mới.
- Xung đột trả lần lượt `STAFF_ALREADY_HAS_OPEN_SHIFT`, `TERMINAL_ALREADY_HAS_OPEN_SHIFT`, `WORKSHIFT_PENDING_CLOSE` hoặc `CONCURRENCY_CONFLICT`.

## 6. Hết hạn

Worker không tự đóng ca rỗng. Mọi WorkShift ngoài lịch đến hạn, dù tiền/order bằng không, chuyển `EXPIRED_PENDING_CLOSE`, giữ khóa terminal và chờ kiểm đếm. Không tự đặt `ActualEndingCash`, `EndTimeUtc`, `CloseType` hoặc `CLOSED`.

`WORKSHIFT_EXPIRED` chỉ dùng khi thao tác trực tiếp không còn được phép; preview phiên cũ active dùng lỗi active/pending tương ứng.

## 7. State machine OTP StaffHub

```text
IDLE → SENDING → SENT → VERIFYING
                    ├→ INVALID → VERIFYING
                    ├→ EXPIRED
                    ├→ LOCKED
                    └→ VERIFIED
```

- Sau gửi: ẩn nút gửi đầu tiên; hiện mã/xác nhận/gửi lại, chạy cooldown; khóa terminal, lý do và payload bind; SweetAlert2 xác nhận đã gửi.
- Sai mã: giữ form, xóa/đặt focus input, giảm số lần thử.
- Đúng mã: xóa mã, ẩn toàn bộ input/xác nhận/gửi lại, hiện `✓ OTP đã được xác nhận`, bật nút sang POS và SweetAlert2 thành công.
- Reload gọi `POST /StaffHub/GetOpenPosOtpState`. `Pending` tiếp tục countdown; `Approved` giữ form ẩn; expired/locked vô hiệu hóa control.
- Endpoint chỉ đọc challenge của requester/store hiện tại và chỉ trả public ID, status, action/OpenContext, terminal, reason, RequestKey, countdown/cooldown/remaining attempts; tuyệt đối không trả OTP/hash/protected payload.
- UI, service, transaction và unique index cùng chống double-click. Không lưu hoặc log OTP trên client.

### 7.1 Action, quyền và thứ tự người duyệt

| OTP action | Permission người duyệt | UI người duyệt |
|---|---|---|
| `OPEN_SHIFT_OUTSIDE_SCHEDULE` | `POS.WorkShift.ApproveOutsideSchedule` | **Xem OTP**; requester nhập mã tại StaffHub |
| `OPEN_SHIFT_LATE` legacy | `POS.WorkShift.ApproveOutsideSchedule` | chỉ giữ tương thích challenge cũ; request mới không dùng |
| `REGISTER_POS_TERMINAL` | `POS.WorkShift.OverrideTerminal` | **Xem OTP → Xác nhận Terminal** |
| `CASH_DIFFERENCE` | `POS.WorkShift.Close` | xác nhận đóng két theo context |
| `CLOSE_SHIFT_EXCEPTION` | `POS.WorkShift.CloseException` | xác nhận ngoại lệ |
| `RECONCILE_WORKSHIFT` | `POS.WorkShift.Reconcile` | xác nhận đối soát |

Action không nhận diện bị deny; không fallback sang permission chung. Candidate phải có staff/account active, email hợp lệ, không phải requester và có permission trong đúng StaffScope.

Với OTP vận hành, thứ tự candidate là Ca trưởng → Store Manager → Area Manager → Business Owner. `SeedAll.sql` cấp `ApproveOutsideSchedule` nhưng không cấp `OverrideTerminal` mặc định cho Ca trưởng; vì vậy ngoài lịch ưu tiên Ca trưởng, còn đăng ký Terminal mặc định tới Store Manager. Challenge đã tồn tại giữ approver cũ.

### 7.2 State machine riêng cho đăng ký Terminal

```text
PENDING
├→ USED          (Store Manager bấm Xác nhận Terminal)
├→ EXPIRED
├→ CANCELLED
└→ LOCKED
```

Notification và challenge có typed relation. **Xem OTP** chỉ unprotect sau khi kiểm tra recipient, relation, Store, StaffScope, action permission, trạng thái và hạn. Chỉ `REGISTER_POS_TERMINAL` có `CanContinueTerminalConfirmation=true`. Realtime/list/log không chứa OTP plaintext.

| Error code | HTTP | Ý nghĩa |
|---|---:|---|
| `OTP_INVALID` | 400 | Sai định dạng hoặc sai mã |
| `OTP_EXPIRED` | 410 | Hết hạn |
| `OTP_ALREADY_USED` | 409 | Đã approved/used, không verify lại |
| `OTP_CONTEXT_MISMATCH` | 409 | Sai requester/store/context |
| `OTP_VERIFICATION_LOCKED` | 423 | Quá số lần thử |
| `OTP_RATE_LIMITED` | 429 | Vượt rate limit |

### 7.3 PIN thao tác POS

- PIN không phải mật khẩu đăng nhập và không được gửi email/hiển thị lại.
- Nhân viên tự vào StaffHub → **PIN thao tác POS** → **Thiết lập hoặc đổi PIN**, xác thực bằng mật khẩu hiện tại rồi tạo đúng 6 chữ số.
- Cấm một chữ số lặp sáu lần, `123456` và `654321`.
- Backend chỉ lưu BCrypt hash, có đếm sai/khóa tạm; client không lưu PIN. `SeedAll.sql` không seed PIN dùng chung.

## 8. Endpoint

StaffHub dùng cookie authorization và anti-forgery:

- `POST /StaffHub/PreviewOpenPos`
- `POST /StaffHub/GetOpenPosOtpState`
- `POST /StaffHub/RequestOpenPosOtp`
- `POST /StaffHub/VerifyOperationalOtp`
- `POST /StaffHub/ResendOperationalOtp`
- `POST /StaffHub/IssuePosToken`
- `POST /StaffHub/IssueResumePosToken`
- các endpoint OTP/đăng ký terminal hiện hành.

Notification OTP:

- `GET /Admin/AdminNotifications/RevealOperationalOtp?id=...`
- `GET /api/v1/pos/notifications/{id}/operational-otp`
- `GET .../RevealTerminalOtp` và `/terminal-otp` là alias tương thích một chu kỳ.
- `POST /Admin/AdminNotifications/ConfirmTerminal` chỉ dành cho `REGISTER_POS_TERMINAL`.

POS:

- `POST /api/v1/pos/session/exchange`
- `GET /api/v1/pos/shifts/current`
- `POST /api/v1/pos/shifts/open` — chỉ nhận tiền đầu phiên; identity/context/WorkShiftId lấy từ token và server context.

Các mã điều hướng chính:

| Error/result code | HTTP | `recommendedAction` |
|---|---:|---|
| `STAFFHUB_OPEN_REQUIRED` | 409 | `OPEN_STAFFHUB` |
| `OPENING_CASH_REQUIRED` | 409 | `ENTER_OPENING_CASH` |
| `TERMINAL_ALREADY_HAS_OPEN_SHIFT` của người khác | 409 | `SWITCH_CURRENT_OPERATOR` |
| `WORKSHIFT_PENDING_CLOSE` | 409 | `COMPLETE_CLOSING` hoặc `COUNT_AND_CLOSE` |
| `OPENED_NEW_WORKSHIFT` | 201 | `CONTINUE_POS` |
| `OPENING_CASH_CONFIRMED` | 200 | `CONTINUE_POS` |

Response blocking luôn có `responsibleStaffId`, `responsibleStaffName`, `isOwnedByRequester` và `recommendedAction`; không được hiện **Tiếp tục POS** khi ca thuộc người khác.

Exchange Code có TTL 60 giây, raw code chỉ trả một lần, DB lưu SHA-256, truyền bằng URL fragment và React xóa fragment trước network I/O.

## 9. Modal viewport contract

- Bootstrap modal giới hạn `max-height: calc(100dvh - safe area)`, chiều rộng không vượt viewport.
- `.modal-body` có `min-height:0`, `overflow-y:auto`, overscroll containment; modal tùy biến StaffHub/Supplier phải mang class `modal-body`.
- Màn hình thấp giảm padding; header/footer vẫn truy cập được ở zoom 100%.
- React overlay phải có `max-height:100dvh`, `min-height:0`, vùng nội dung scroll.
- Nghiệm thu tại `423×825`, `390×844`, `768×1024`, `1366×768`.

## 10. Phạm vi P2

Đã có: unique active indexes, Current Operator, order attribution, offline giữ WorkShiftId, permission/audit/SignalR, dedup và terminal scope.

Chuyển giai đoạn sau: transfer terminal, `DeviceInstallationId`, auto-lock, revoke session toàn diện và approval cho mọi thao tác nhạy cảm. Đợt này không thêm migration schema.

## 11. Tiêu chí nghiệm thu P0

- JSON UTC kết thúc bằng `Z`; hạn ngoài lịch đúng 6 giờ khi so theo instant.
- Không còn tình trạng vừa báo mở thành công vừa báo hết hạn.
- StaffHub không tạo pre-open WorkShift; POS open commit đúng một WorkShift sau khi xác nhận tiền đầu ca.
- Ca rỗng và có dữ liệu đều chuyển `EXPIRED_PENDING_CLOSE`.
- Replay/race không tạo hai WorkShift; phiên `CLOSED`/`RECONCILIATION_REQUIRED` không được chọn current.

## 12. Quy tắc chuẩn cuối (2026-08)

Quy tắc tại mục này thay thế mô tả cũ “trễ trên 30 phút cần OTP”:

| Độ trễ | Kết quả |
|---|---|
| `0 < late <= 15` | Mở ngay, audit, hiển thị số phút trễ |
| `15 < late < 30` | Lý do 10–500 ký tự, mở và thông báo Manager |
| `30 <= late <= 45` | Tạo `WorkShiftOpenApprovalRequest`; Manager được duyệt, từ chối hoặc chuyển ngoài lịch |
| `late > 45` | Tạo `WorkShiftOpenApprovalRequest`; khóa duyệt theo lịch cũ, chỉ cho từ chối hoặc chuyển ngoài lịch |
| Quá `planned end + PostEndGraceMinutes` | Không bind lịch cũ; chỉ ngoài lịch hoặc lịch bổ sung |

Request trễ từ 30 phút có state machine `PENDING → APPROVED | REJECTED | CONVERTED_TO_OUTSIDE_SCHEDULE | CANCELLED | EXPIRED`. `APPROVED` chỉ hợp lệ khi `MinutesLate <= 45`; trên 45 phút UI khóa **Duyệt mở ca** và backend cũng từ chối direct POST. Quyền quyết định là `POS.WorkShift.ApproveLateOpen` với store scope; Shift Supervisor không mặc nhiên có quyền. **Chuyển ngoài lịch** không sửa lịch nguồn, không tự tạo WorkShift và không yêu cầu OTP thứ hai; WorkShift chỉ được tạo sau khi requester xác nhận tiền đầu ca tại POS.

Hủy trước opening cash phải đánh dấu exchange context bằng `CancelledAtUtc`, chuyển dedup ticket sang `EXPIRED`, kết thúc POS access session chưa bind rồi hủy OTP/approval chưa dùng. Không dùng `CANCELLED` cho `RequestDeduplications` vì check constraint chỉ cho `PROCESSING`, `SUCCESS`, `FAILED`, `EXPIRED`. Nếu session đã bind WorkShift, hủy trả 409.

`PosAccessSession` độc lập với `WorkShift`: một Terminal có tối đa một session `ACTIVE`; JWT session/JTI phải khớp dữ liệu server. Mở/nhập tiền đầu ca bind đúng `WorkShiftId`; Order, StartClosing, Close, CloseException, Reconcile và SwitchOperator đều revalidate session-bound WorkShift. Session mới thay session cũ và phát `PosAccessSessionChanged` sau commit.

Notification đăng ký Terminal không được suy diễn từ `Title/Body`. Notification giữ liên kết typed tới `OtpChallenge`, tồn tại cả sau `Used/Expired/Cancelled`, hỗ trợ mark-one theo `RecipientStaffId`, reveal OTP riêng cho approver và cập nhật realtime bằng `TerminalRegistrationChanged`.

Các mã client phải phân biệt: `OPENING_CASH_REQUIRED`, `LATE_OPEN_APPROVAL_PENDING`, `LATE_OPEN_APPROVAL_REJECTED`, `LATE_OPEN_APPROVAL_EXPIRED`, `LATE_OPEN_REQUIRES_OUTSIDE_SCHEDULE`, `POS_SESSION_EXPIRED`, `POS_SESSION_REVOKED`, `POS_SESSION_ENDED`, `POS_TERMINAL_LOCKED` và `POS_SESSION_WORKSHIFT_MISMATCH`.
