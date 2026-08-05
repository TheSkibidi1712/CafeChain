# Báo cáo triển khai StaffHub và WorkShift POS

Cập nhật: 03/08/2026.

## Kết quả

Đã chuyển toàn bộ nghiệp vụ chọn/đăng ký terminal, preview lịch, mở sớm/trễ/ngoài lịch, lý do và OTP từ React POS sang StaffHub. POS chỉ exchange context đã được backend xác nhận, nhập tiền đầu phiên và gọi mở phiên bình thường.

Không sửa `FIX.md`, không tạo chấm công/lương/tăng ca/`StaffShift` giả và không chỉnh `Scripts/SeedAll.sql`.

## Backend

- Thêm DTO StaffHub terminal/preview/OTP/issue ticket và exchange context.
- StaffHub actions có authorization/anti-forgery cho preview, OTP, terminal registration, issue open/resume.
- Context bind account/staff/store/terminal/purpose/OpenContext/schedule/RequestKey/reason/approval.
- Exchange code hash-only, TTL 60 giây, one-time; JWT chứa context ID/purpose.
- Tách `POS_EXCHANGE_CODE_EXPIRED`, `POS_EXCHANGE_CODE_ALREADY_USED`, `POS_EXCHANGE_CODE_INVALID`.
- POS open yêu cầu exchange context; request client chỉ còn tiền đầu phiên có ý nghĩa nghiệp vụ.
- Public `open-assessment` và `POSTerminalController` đã loại bỏ.
- Repository có query active theo staff/terminal và danh sách terminal active.
- Staff active được map đúng: `OPEN` → `STAFF_ALREADY_HAS_OPEN_SHIFT`; `CLOSING`/`EXPIRED_PENDING_CLOSE` → `WORKSHIFT_PENDING_CLOSE`.
- Terminal active → `TERMINAL_ALREADY_HAS_OPEN_SHIFT`.
- `WORKSHIFT_EXPIRED` không còn được dùng cho preview/mở mới/exchange.
- Open revalidate context/lịch/permission/terminal/OTP; active check và create nằm trong transaction serializable.
- Unique violation được requery để trả lỗi nghiệp vụ hoặc `CONCURRENCY_CONFLICT`.
- OTP mở ca không bind tiền đầu phiên; vẫn revalidate approver và consume trong transaction.

## StaffHub UI

- Bắt buộc chọn terminal trước preview.
- Luôn hiển thị xác nhận, kể cả `WITHIN_SCHEDULE`.
- Lý do và request/verify/resend OTP nằm tại StaffHub.
- Đăng ký terminal và OTP terminal nằm tại StaffHub.
- Phiên đang khóa hiển thị mã, terminal, bắt đầu, trạng thái, hạn và nút tiếp tục/đóng/kiểm đếm.
- Tiền đầu phiên không xuất hiện tại StaffHub.

## React POS

- Đã gỡ `open-assessment`, tự phân loại lịch, form lý do, OTP mở trễ/ngoài lịch và đăng ký terminal.
- Open request chỉ gửi `startingCash`.
- Fragment exchange được xóa trước network theo luồng session hiện hành.
- Direct open không có context bị backend từ chối.

## Idempotency và schema

Schema hiện hành đã có rowversion và filtered unique index:

- `UX_WorkShifts_ActiveStaff`
- `UX_WorkShifts_ActiveTerminal`

Cả hai bảo vệ `OPEN`, `CLOSING`, `EXPIRED_PENDING_CLOSE`. Migration hiện hành là `20260803054639_InitialCreate`; không thêm migration vì model đã có đủ guard.

RequestKey/payload hash bảo đảm replay đúng trả kết quả cũ, cùng key khác payload trả `DUPLICATE_REQUEST`, race còn lại trả lỗi active hoặc `CONCURRENCY_CONFLICT`.

## Kiểm thử

Đã cập nhật/thêm kiểm thử cho:

- Preview StaffHub không issue ticket.
- Staff `OPEN`, `CLOSING`, `EXPIRED_PENDING_CLOSE` trả đúng mã.
- Terminal ở cả ba active state trả terminal conflict.
- React POS chỉ submit tiền đầu phiên và không còn nghiệp vụ đặc biệt.
- POS bắt buộc exchange context; không còn public assessment/terminal registration.
- Exchange contract 60 giây/hash-only/one-time/ba mã lỗi.
- Hai filtered unique index chứa đầy đủ active states.
- Worker expiry hiện hành: phiên rỗng tự đóng; phiên có tiền chuyển `EXPIRED_PENDING_CLOSE`.

Kết quả xác minh tại lần cập nhật tài liệu:

- Backend build: đạt.
- Frontend `npm ci` và production build: đạt; chỉ còn cảnh báo bundle/annotation từ dependency.
- Targeted StaffHub/assessment/expiry/refactor: 23/23 đạt.
- Suite loại theo tên `SqlServer`: 1.738 đạt; còn hai lỗi hạ tầng/baseline không thuộc refactor (một test trỏ migration cũ `20260802183312_InitialCreate` không còn tồn tại và một test vẫn mở SQL Server dù tên không chứa `SqlServer`).
- Frontend production build và ESLint: đạt; build chỉ còn cảnh báo bundle/annotation từ dependency.
- EF `has-pending-model-changes`: không có thay đổi model chưa migration.
- Full SQL Server integration chưa thể kết luận trong runner hiện tại: `sqlcmd` kết nối được instance nhưng tiến trình test .NET thất bại khi tạo SSPI context. Nhóm OTP Phase 2 không dùng SQL đạt 21/21 sau khi kiểm tra tương thích fingerprint.

## Chưa mở rộng

- Không tạo migration/index mới vì schema hiện hành đã đủ.
- Không thay đổi nghiệp vụ đóng ngoại lệ/reconciliation ngoài việc giữ đúng ranh giới active.
- Không thêm chấm công, tiền lương, overtime, thu/chi két.
- Kiểm thử tải nhiều application instance và SQL Server race thực tế vẫn phụ thuộc hạ tầng CI/connection string.
