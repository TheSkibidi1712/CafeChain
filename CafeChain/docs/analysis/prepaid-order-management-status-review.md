# Rà soát quản lý đơn hàng theo mô hình thanh toán trước

## Kết luận

Issue `#199` được triển khai theo hướng tách read model và quyền thao tác theo kênh, không xóa vòng đời dùng chung:

- `POS_COUNTER` dùng Lịch sử bán hàng dựa trên bằng chứng thanh toán.
- `WEB_ORDER` và `DELIVERY` dùng Bảng xử lý đơn Web/Giao hàng.
- `LEGACY_UNKNOWN` không vào doanh thu hoặc board khi chưa phân loại chắc chắn.
- `MIGRATION_REQUIRED = false` vì `Order.Source` và `Order.OrderTypeId` hiện đủ làm authority.

`WEB_DELIVERY_LIFECYCLE_PRESERVED` và `PREPAID_POS_ORDER_HISTORY_SIMPLIFIED`.

## Channel authority

| Dữ liệu persist | Phân loại | Độ tin cậy |
|---|---|---|
| `Source=POS` | `POS_COUNTER` | Cao |
| `Source=Website`, `OrderTypeId=Delivery` | `DELIVERY` | Cao |
| `Source=Website`, loại đơn khác | `WEB_ORDER` | Cao |
| `Source` null hoặc giá trị khác | `LEGACY_UNKNOWN` | Chưa đủ, `MANUAL_REVIEW_REQUIRED` |

Policy nằm tại `Application/Policies/Orders/OrderChannelPolicy.cs`. Không suy đoán channel từ `OrderStatus`, payment method hoặc mã đơn.

Các writer chính:

- POS cash/QR/split: `POSOrderService.CommitOrderAsync` ghi `Source=POS`.
- POS offline: `POSOrderService.CommitOfflineSyncedOrderAsync` ghi `Source=POS` và giữ `ClientOrderId`.
- Web/customer: `OrderService.CreateOrderAsync` ghi `Source=Website`, loại `Delivery` cho đơn giao hàng.

## Vòng đời POS hiện tại

### Cash online và offline sync

`Completed + Paid` chỉ được tạo khi thanh toán đủ. Sau commit mới cập nhật WorkShift/CashDrawer, trừ kho và gửi lệnh in. Offline queue trước sync không tạo Order backend.

### VietQR và split

Backend hiện vẫn dùng `Order=AwaitingPayment`, `Payment=Unpaid` làm payment intent. Trước webhook xác nhận, order không vào lịch sử bán hàng, không vào board, không tính doanh thu, không trừ kho và không in chính thức.

Webhook thành công chuyển sang `Completed + Paid` theo idempotency guard. Đây là giới hạn kiến trúc hiện tại; task này không thêm `PaymentSession`.

### Cancel/refund

- `Cancelled + Failed/Unpaid` trước thanh toán không phải giao dịch bán hàng và bị loại khỏi POS history.
- Chỉ `Completed + Refunded` có payment line `Refunded` mới hiển thị `Đã hoàn tiền`.
- Hệ thống chưa có evidence riêng đủ mạnh để gọi mọi order paid-cancelled là `Đã vô hiệu hóa`; không tự gán nhãn này.

## Vòng đời Web/Delivery

Writer thật vẫn tồn tại:

`Pending -> Preparing -> Ready -> Delivering -> Completed`

- `AdminOrderService.AcceptOrderAsync`: `Pending -> Preparing`.
- `ReadyForPickupAsync`: `Preparing -> Ready`.
- `DispatchOrderAsync`: `Ready -> Delivering`.
- `CompleteOrderAsync`: `Ready/Delivering -> Completed`.
- `CancelOrderAsync`: trạng thái vận hành -> `Cancelled`.
- `SimulateWebhookAsync` và `OrderService.SimulateDeliveryAsync` vẫn phục vụ mô phỏng giao hàng.

Board vì vậy không thể bị xóa. Query và mọi action/detail hiện giới hạn `Source=Website` và `StoreId` hiện hành; POS và cross-store order bị từ chối.

## Dependency map

| Thành phần | OrderStatus | PaymentStatus | Channel authority | Rủi ro/biện pháp |
|---|---|---|---|---|
| POS cash | `Completed` | `Paid` | `Source=POS` | Chỉ vào history sau commit |
| POS VietQR pending | `AwaitingPayment` | `Unpaid` | `Source=POS` | Loại khỏi history/board/revenue |
| POS webhook | `Completed` sau confirm | `Paid` | `Source=POS` | Idempotency giữ side effect một lần |
| POS offline sync | `Completed` | `Paid` | `Source=POS`, `ClientOrderId` | Duplicate không tạo order/trừ kho/in lại |
| Inventory/FIFO | Paid commit | Paid | Không dùng board làm authority | Không thay trigger trong #199 |
| Receipt/label/PrintBridge | Paid commit | Paid | POS | UI chỉ nói tài liệu “đã sẵn sàng”, không khẳng định đã ra giấy |
| POS history | `Completed` | `Paid/Refunded` + payment evidence | POS | PaidAt, Store scope, split tender |
| Web/Delivery board | Active/Completed today | Tùy COD/online | Website + order type | Giữ transition hiện có |
| CSV | Như POS history | Như POS history | POS | Dùng cùng filtered query |
| Refund | `Completed` | `Refunded` + line evidence | POS | Không tính vào doanh thu Paid |
| Notification/SignalR | Web transition | Không làm authority | Website | Vẫn gửi update sau transition |

## Lịch sử bán hàng POS

Hai bề mặt được giữ:

- React POS `/history`: summary row, drawer chi tiết, backend sales và local offline queue.
- Admin `AdminOrder/History`: read model POS đã commit, export CSV và chi tiết payment lines.

Authority của history/admin CSV:

- `StoreId` đúng phạm vi hiện hành.
- `Source=POS`.
- `OrderStatus=Completed`.
- `PaymentStatus=Paid/Refunded`.
- Tồn tại payment line `Paid/Refunded` tương ứng.

Ngày lọc, sắp xếp và hiển thị dùng `PaidAt`; dữ liệu legacy có `PaidAt` null mới fallback `CreatedAt`.

Doanh thu chỉ cộng dòng `PaymentStatus=Paid`. `Refunded`, `AwaitingPayment`, cancel-before-payment, Web/Delivery và legacy thiếu evidence không được cộng.

Thông tin hiển thị/export gồm mã đơn, thời gian thanh toán, khách hàng, cửa hàng, thu ngân, loại đơn, tổng tiền, tender, trạng thái tài chính và trạng thái sẵn sàng của hóa đơn/tem.

## Payment/status display

| Evidence | Nhãn |
|---|---|
| `CASH` | `Tiền mặt` |
| `BANK`, `BANK_TRANSFER`, `VIETQR` | `Chuyển khoản VietQR` |
| Nhiều tender paid/refunded | `Thanh toán kết hợp` |
| `MOMO` | `Ví điện tử — dữ liệu cũ` |
| null/không nhận diện | `Chưa xác định` |

Không đổi Momo legacy thành VietQR và không hiển thị raw `N/A` trong POS sales history.

## Legacy discrepancy report

Khi chạy đối soát trên DB thật, export các cột sau, không update dữ liệu:

| Cột | Ý nghĩa |
|---|---|
| `OrderId` | Khóa Order |
| `Channel` | Kết quả `OrderChannelPolicy` |
| `OrderStatus` | Trạng thái order persist |
| `PaymentStatus` | Trạng thái tài chính persist |
| `PaymentMethod` | Tender code/name |
| `HasPayment` | Có payment line hay không |
| `HasInventoryDeduction` | Có `SALES_DEDUCTION` theo order hay không |
| `CreatedAt` | Thời gian tạo |
| `PaidAt` | Thời gian payment xác nhận |
| `SuggestedClassification` | Phân loại đề xuất |
| `Confidence` | `HIGH` hoặc `MANUAL_REVIEW_REQUIRED` |

Các nhóm bắt buộc kiểm tra thủ công:

- Momo hoặc phương thức đã ngừng dùng.
- Payment method null/không nhận diện.
- `Source=POS` nhưng status `Delivering`.
- `Completed` không có payment evidence.
- `Cancelled` có payment paid/refunded nhưng thiếu refund/void audit.
- `AwaitingPayment` quá hạn.
- `LEGACY_UNKNOWN`.

Không backfill bằng payment method/status và không mutate các dòng `MANUAL_REVIEW_REQUIRED`.

## Navigation và authorization

- Menu admin phân biệt `Bảng xử lý đơn Web/Giao hàng` và `Lịch sử bán hàng`.
- `AdminOrderController` kế thừa `AdminBaseController`, giữ policy `RequireAdminPanelAccess`.
- Store được resolve bằng `IAdminStoreScopeResolver`; service vẫn kiểm tra `StoreId` trên query/action.
- Route cũ được giữ để bookmark không hỏng; chỉ đổi heading và read model.

## Phần cố ý để lại

- Không thêm schema `OrderChannel`.
- Không tạo `PaymentSession`.
- Không xóa OrderStatus/Web/Delivery board.
- Không đổi inventory, PayOS webhook hoặc print authority.
- Không khẳng định PrintBridge đã in vật lý vì chưa có persisted acknowledgement.
- Không tự backfill hoặc sửa Momo/legacy rows.

## Quyết định Owner còn lại

1. Có tiếp tục Web checkout, Delivery/COD và shipper trong production không?
2. Có cần một lịch sử Web/Delivery riêng ngoài board không?
3. Có cần persisted PrintJob acknowledgement để hiển thị “đã in thành công” không?
4. Có duyệt `PaymentSession/PaymentAttempt` để VietQR pending không còn nằm trong `Orders` không?
5. Void/refund cần thêm money-movement audit nào trước khi hiển thị trạng thái chi tiết hơn?

Issue `#199` phải giữ `OPEN` cho tới khi Owner nghiệm thu runtime và legacy reconciliation.
