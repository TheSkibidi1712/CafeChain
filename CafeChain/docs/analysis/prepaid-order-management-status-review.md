# Rà soát trạng thái đơn hàng theo mô hình thanh toán trước

## Cập nhật triển khai channel-aware (#199)

Dependency audit ban đầu vẫn đúng: không thể xóa lifecycle Web/Delivery vì còn writer thật. Giải pháp đã chọn là tách read model và action theo kênh, không xóa status dùng chung.

Authority hiện tại không cần đổi schema:

- `Source=POS` => `POS_COUNTER`.
- `Source=Website` và `OrderTypeId=Delivery` => `DELIVERY`.
- `Source=Website` và loại đơn khác => `WEB_ORDER`.
- `Source` null/khác => `LEGACY_UNKNOWN`; không suy đoán bằng trạng thái hay payment.

Các discrepancy legacy phải giữ nguyên để kiểm tra:

- Momo hiển thị `Ví điện tử — dữ liệu cũ`, không đổi thành VietQR.
- Payment thiếu/không nhận diện hiển thị `Chưa xác định`, không tính doanh thu nếu thiếu evidence Paid/Refunded.
- POS `AwaitingPayment/Unpaid` không vào lịch sử bán hàng đã commit.
- POS `Completed/Paid` và `Completed/Refunded` vào history; doanh thu chỉ cộng dòng Paid.
- Board chỉ nhận `WEB_ORDER/DELIVERY` thuộc đúng store; POS và `LEGACY_UNKNOWN` không được thao tác qua board.

Schema impact: `MIGRATION_REQUIRED = false`.

## Phạm vi và kết luận ban đầu

Tài liệu này chỉ phân tích. Không thay đổi controller, service, trạng thái, seed hay giao diện Quản lý đơn hàng.

**Kết luận ban đầu:** `ORDER_BOARD_RETIREMENT_BLOCKED_BY_ACTIVE_WEB_ORDER_AND_DELIVERY_WRITERS`.

React POS hiện không cần bảng điều phối: đơn tiền mặt được tạo thẳng ở `Completed`; đơn VietQR chỉ chuyển sang `Completed` sau webhook. Tuy nhiên bảng điều phối vẫn đang phục vụ luồng web/customer khác với writer thật cho pha chế, giao hàng, COD và mô phỏng giao hàng. Không thể xóa hoặc đổi status toàn hệ thống chỉ dựa trên nghiệp vụ React POS.

## Vòng đời Order hiện tại

### React POS

- Cash online và offline sync tạo `Order=Completed`, `Payment=Paid`.
- VietQR/split hiện tạo bản ghi `Order=AwaitingPayment`, `Payment=Unpaid`; webhook thành công chuyển atomically sang `Order=Completed`, `Payment=Paid`.
- Trước webhook không cộng tiền mặt tạm vào `WorkShift.ExpectedEndingCash`, không trừ kho và không in chính thức.
- Hủy intent VietQR chuyển `AwaitingPayment/Unpaid` sang `Cancelled/Failed`.

Điểm chưa khớp hoàn toàn với target Owner: intent VietQR vẫn dùng bảng `Orders` vì codebase chưa có `PaymentSession/PaymentAttempt` độc lập và PayOS đang map `orderCode` qua `Order.PaymentReference`.

### Web/customer và Admin board

- `OrderService` tạo order web ở `Pending` hoặc `AwaitingPayment`.
- `PaymentController` chuyển `AwaitingPayment -> Pending` sau thanh toán web.
- `AdminOrderService.AcceptOrderAsync`: `Pending -> Preparing`.
- `AdminOrderService.ReadyForPickupAsync`: `Preparing -> Ready`.
- `AdminOrderService.DispatchOrderAsync`: `Ready -> Delivering`, có shipper nội bộ/đối tác ngoài.
- `AdminOrderService.CompleteOrderAsync`: `Ready/Delivering -> Completed`; còn chứa nhánh COD đánh dấu payment `Paid`, gọi inventory và loyalty.
- `AdminOrderService.CancelOrderAsync`: trạng thái đang vận hành -> `Cancelled`; payment `Paid -> Refunded`, còn lại -> `Failed`.
- `OrderService.SimulateDeliveryAsync` và `AdminOrderService.SimulateWebhookAsync` là writer mô phỏng `Ready -> Delivering -> Completed`.
- `OrderCleanupWorker` và `PaymentCleanupWorker` hủy order pending/awaiting quá hạn.

## Vòng đời Payment hiện tại

- Trạng thái seed: `Unpaid`, `Paid`, `Refunded`, `Failed`.
- Phương thức seed gồm Cash, Bank, Momo, ZaloPay, VNPay.
- React POS chỉ hỗ trợ Cash (`1`) và VietQR/Bank (`2`) trong split.
- `Momo` vẫn xuất hiện trong web checkout và master data, nhưng không phải phương thức hợp lệ của React POS hiện tại.
- `N/A` trong history là fallback khi order không có payment hoặc navigation/payment mapping thiếu; không phải một phương thức thanh toán.

## Bảng điều phối và dependency

- Board lấy `Pending`, `Preparing`, `Ready`, `Delivering`, cộng 20 order `Completed` trong ngày.
- UI có action nhận đơn, xong món, gán shipper, hoàn thành và mô phỏng webhook.
- SignalR group `AdminDashboard` nhận status updates.
- Inventory legacy của web order được gọi khi `CompleteOrderAsync`; React POS có guardrail inventory riêng khi paid commit/webhook.
- Delivery type (`OrderTypeId=3`) kích hoạt mô phỏng giao hàng sau khi món Ready.
- Vì các writer/dependency này tồn tại, hide board toàn hệ thống có thể làm web order kẹt ở Pending/Ready và có thể ngăn nhánh inventory/COD legacy hoàn tất.

Không thấy KDS độc lập hoặc printer kitchen xác nhận trạng thái pha chế; board hiện chính là UI vận hành duy nhất cho lifecycle web/delivery này.

## Lịch sử đơn hàng

- Admin history query trả mọi `Order`, kể cả `AwaitingPayment`, `Cancelled` và dữ liệu legacy.
- Row lấy payment đầu tiên nên split payment không được mô tả đầy đủ; thiếu payment trả `N/A`.
- Bộ lọc vẫn hiển thị `Chờ lấy hàng`, `Đang giao` vì status master và writer đang hoạt động.
- POS React history có DTO/drawer riêng, phù hợp hơn cho cashier nhưng vẫn đọc backend Orders và local offline queue.

## Target đề xuất cho React POS prepaid

Trước thanh toán nên thuộc payment intent/session:

- `OPEN`
- `AWAITING_QR`
- `CANCELLED`
- `EXPIRED`
- `COMPLETED`

Sau thanh toán, POS order history ưu tiên:

- `Đã thanh toán`
- `Đã hủy/Voided`
- `Đã hoàn tiền`

Các trạng thái pha chế/giao hàng chỉ giữ cho channel thực sự dùng fulfillment. Không gán chúng mặc định cho React POS.

## Kế hoạch deprecate an toàn

1. Xác nhận Owner có tiếp tục web checkout, Delivery/COD và shipper hay không.
2. Gắn `Source/channel` rõ ràng và thống kê số order theo source/status trong DB thật.
3. Tách payment intent khỏi `Orders` trước khi loại `AwaitingPayment` khỏi history chính thức.
4. Nếu bỏ web delivery, ngừng writer và background simulation trước; chuyển các order active còn lại bằng migration có audit.
5. Chuyển inventory/COD side effect khỏi `CompleteOrderAsync` sang paid-commit authority phù hợp trước khi ẩn board.
6. Sau thời gian quan sát không còn writer/active row, mới hide navigation; xóa code/status là bước cuối và cần issue riêng.

## Rủi ro dữ liệu

- Dữ liệu `Delivering`, Momo và payment thiếu có thể là dữ liệu web/seed/legacy thật; không được bulk đổi mà chưa phân loại theo `Source`, `OrderTypeId`, payment và thời gian.
- Split payment bị history admin rút gọn thành payment đầu tiên.
- `Cancelled + Refunded` hiện có thể chỉ là đổi status, chưa chứng minh money movement hoàn tiền thật.
- POS intent ở `AwaitingPayment` đang nằm trong Orders nên history admin có thể coi nhầm là đơn chính thức.

## Đề xuất UI

- POS history: mã đơn, thời gian, tổng tiền, breakdown phương thức, trạng thái payment/sync/in, thu ngân, cửa hàng, loại đơn.
- Admin history: hiển thị tất cả payment lines thay vì payment đầu tiên; đổi `N/A` thành lý do cụ thể như `Chưa có dòng thanh toán`.
- Board: chỉ hiển thị source/channel có fulfillment; không đưa React POS `Completed` vào board thao tác.

## Issues

- Implement payment hardening: GitHub issue #198.
- Review/deprecation backlog: GitHub issue #199.

## Quyết định Owner còn thiếu

1. Web/customer checkout còn là sản phẩm chính thức hay chỉ demo legacy?
2. Có vận hành Delivery/COD và shipper thật không?
3. Có cần KDS/pha chế riêng sau khi POS đã thanh toán không?
4. Với order paid bị hủy, nghiệp vụ là void hay refund có money movement?
5. Có duyệt thêm `PaymentSession/PaymentAttempt` để VietQR pending không còn nằm trong bảng Orders không?
