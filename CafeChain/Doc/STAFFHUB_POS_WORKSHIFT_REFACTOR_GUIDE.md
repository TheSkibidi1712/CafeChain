# Hướng dẫn refactor StaffHub và WorkShift POS

Tài liệu vận hành chi tiết nằm tại [STAFFHUB_USER_BUSINESS_FLOWS.md](./STAFFHUB_USER_BUSINESS_FLOWS.md); quy tắc chuẩn nằm tại [STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md](./STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md).

## 1. Kiến trúc sau refactor

- StaffHub: account/staff/store scope, terminal, lịch, active WorkShift, lý do, OTP và phát exchange code.
- POS session exchange: đổi code một lần thành JWT có `PosExchangeContextId` và `PosPurpose`.
- React POS: xóa fragment trước network, chỉ nhập `startingCash` và gọi open.
- WorkShift service/repository: tải context từ database, revalidate, idempotency và transaction.
- Worker: xử lý deadline với lock/transaction, không tự đóng phiên cần kiểm đếm.

Không tạo chấm công, tính lương, tăng ca hoặc `StaffShift` giả.

## 2. Contract StaffHub

Preview nhận `TerminalId` và `RequestKey`; account/staff/store/time lấy từ identity và server. Response gồm OpenContext, phút sớm/trễ, lịch nguồn, yêu cầu lý do/OTP và `BlockingWorkShift`.

Các action mutation dùng cookie authorization, permission và anti-forgery:

- `PreviewOpenPos`
- `RequestOpenPosOtp`, `VerifyOperationalOtp`, `ResendOperationalOtp`
- `RequestTerminalRegistrationOtp`, `RegisterTerminal`
- `IssuePosToken`, `IssueResumePosToken`

StaffHub luôn xác nhận `WITHIN_SCHEDULE`; trễ/ngoài lịch xử lý đầy đủ trước khi issue. Tiền đầu phiên không xuất hiện tại StaffHub.

## 3. Contract exchange

`PosSessionExchangeContextDto` bind purpose, account, staff, store, terminal, RequestKey, OpenContext, lịch nguồn, lý do, approval và WorkShift khi resume.

- TTL: 60 giây.
- Lưu hash, không lưu raw code.
- Một lần dùng.
- URL fragment, không query JWT.
- Ba lỗi: `POS_EXCHANGE_CODE_EXPIRED`, `POS_EXCHANGE_CODE_ALREADY_USED`, `POS_EXCHANGE_CODE_INVALID`.
- Context đã exchange được giữ đến expiry của POS token để nhập tiền đầu phiên.

## 4. Contract POS open

`POST /api/v1/pos/shifts/open` chỉ nhận tiền đầu phiên từ React. Controller lấy exchange context ID từ claim; service tải context server và không tin terminal/OpenContext/reason/OTP do client tự gửi.

Token không có context/purpose open trả `POS_OPEN_CONTEXT_REQUIRED`; context sai/hết hạn trả `POS_OPEN_CONTEXT_INVALID`.

Public POS `open-assessment` và `POSTerminalController` đã loại bỏ.

## 5. Active WorkShift và mã lỗi

`WorkShiftStatuses.ActiveResponsibility` = `OPEN`, `CLOSING`, `EXPIRED_PENDING_CLOSE`.

- Staff `OPEN` → `STAFF_ALREADY_HAS_OPEN_SHIFT`.
- Staff `CLOSING` hoặc `EXPIRED_PENDING_CLOSE` → `WORKSHIFT_PENDING_CLOSE`.
- Terminal có bất kỳ active state → `TERMINAL_ALREADY_HAS_OPEN_SHIFT`.
- `CLOSED`, `RECONCILIATION_REQUIRED` không khóa.
- `WORKSHIFT_EXPIRED` chỉ dành cho thao tác trực tiếp trên phiên thực sự hết hạn.

Repository dùng `GetActiveShiftAsync`, `GetActiveShiftByTerminalAsync`, `GetActiveTerminalsAsync`; assessment dùng chung các query này.

## 6. Idempotency và race

RequestKey lấy từ exchange context. Replay đúng payload trả WorkShift cũ; key trùng khác payload trả `DUPLICATE_REQUEST`. Open chạy transaction serializable và repository recheck active staff/terminal trong transaction.

Schema hiện hành đã có rowversion và hai filtered unique index:

- `UX_WorkShifts_ActiveStaff`
- `UX_WorkShifts_ActiveTerminal`

Filter của cả hai gồm `OPEN`, `CLOSING`, `EXPIRED_PENDING_CLOSE`; vì vậy refactor này không cần migration mới. Unique/rowversion race được requery và map sang lỗi nghiệp vụ hoặc `CONCURRENCY_CONFLICT`.

## 7. OTP

Fingerprint mở ca bind action, requester/approver, store, terminal, lịch nguồn/context, reason và RequestKey; starting cash là 0 trong fingerprint vì tiền chỉ nhập ở POS. Consume OTP và gắn WorkShiftId trong transaction mở thành công.

Đăng ký terminal cũng nằm tại StaffHub, có request/verify/resend OTP và permission/scope riêng.

## 8. Cách kiểm tra

```powershell
dotnet build CafeChain/CafeChain.csproj --no-restore --nologo
dotnet test .\CafeChain.Tests\CafeChain.Tests.csproj --no-build --filter "FullyQualifiedName~StaffHub|FullyQualifiedName~WorkShiftOpenAssessment|FullyQualifiedName~WorkShiftExpiry|FullyQualifiedName~StaffHubPosRefactor"
cd CafeChain.Frontend
npm.cmd ci
npm.cmd run build
```

SQL Server integration cần cấu hình `CAFECHAIN_TEST_SQLSERVER_CONNECTION_STRING`. Kiểm tra tối thiểu gồm hai staff/hai terminal, ba active state, double-click/replay, worker-open race, closed/reconciliation, exchange TTL/one-time và direct POS rejection.

## 9. Triển khai

Không cần migration mới cho thay đổi này. Áp dụng migration hiện hành `20260803054639_InitialCreate`, cấu hình JWT/Data Protection/SMTP/SignalR như môi trường, seed permission bằng nguồn seed hiện hành và không sửa dữ liệu WorkShift cũ để “hợp thức hóa” lịch.
