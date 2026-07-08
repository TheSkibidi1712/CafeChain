# POS Payment, Offline Sync, Print, WorkShift - Handoff

## 1. Tổng quan module POS

Module POS hiện tại đã hoàn thiện luồng bán hàng tại quầy cho CafeChain theo PRD `pos-payment-offline-sync-print-workshift.md`. Hệ thống đã có WorkShift guard, thanh toán tiền mặt online, VietQR/PayOS qua webhook, split payment, Offline Order lưu IndexedDB, Sync bằng ClientOrderId, Silent Print qua PrintBridge, lịch sử đơn hàng, reprint, đóng WorkShift thường và đóng ngoại lệ bởi supervisor/manager.

Nguyên tắc chính:

- Order/payment commit là source of truth.
- Print là side-effect sau commit, không được làm fail order đã commit.
- Inventory Deduction chỉ chạy sau khi order đã paid/committed.
- Offline Order dùng ClientOrderId làm Idempotency Key và vẫn gắn với WorkShift gốc.
- WorkShift đã đóng ngoại lệ hoặc có late offline sync phải được đánh dấu cần đối soát lại.

## 2. Issues #75-#86 đã hoàn thành

- #75: POS WorkShift Guard Cho Mọi Thanh Toán.
- #76: Cash Online Commit Với Silent Print Chính Thức.
- #77: VietQR/PayOS Commit Sau Webhook Confirmed.
- #78: Split Payment Pending Flow Với Cart Lock.
- #79: Offline Cash Order Local Queue Với ClientOrderId.
- #80: Offline Temporary Receipt Và Temporary Drink Label.
- #81: Offline Sync Idempotent Vào WorkShift Cũ.
- #82: Order History Summary Và Detail Drawer.
- #83: Reprint Hóa Đơn Và Tem Từ Drawer.
- #84: WorkShift Normal Close Blocking.
- #85: Supervisor WorkShift Close Exception Và Reconciliation.
- #86: Inventory Deduction Guardrails Cho Online, Split, Offline Sync.

## 3. Các flow đã hoàn thành

### WorkShift guard

- POS payment bị chặn nếu thu ngân chưa có WorkShift đang mở.
- WorkShift gắn theo StoreId, StaffId và POS terminal khi có.
- Normal close bị chặn khi có backend-known AwaitingPayment/Unpaid POS order.

### Cash online payment

- POS tạo order paid/completed trong WorkShift đang mở.
- Tiền mặt được cộng vào ExpectedEndingCash.
- Inventory Deduction chạy sau commit bằng `DeductStockForCommittedOrderAsync`.
- PrintDispatcher gửi receipt và drink label qua PrintBridge.

### VietQR/PayOS webhook

- Order VietQR/Split có PayOS payment được tạo ở trạng thái AwaitingPayment/Unpaid.
- Webhook confirmed chuyển order/payment sang paid/completed trong DB transaction.
- Duplicate webhook không dispatch print lại.
- Inventory guard cho phép repair nếu order đã paid nhưng deduction trước đó thiếu transaction.

### Split payment

- POS giữ pending cash amount và pending VietQR amount.
- Cart bị khóa khi đang thanh toán.
- Order chỉ commit paid khi PayOS webhook confirmed phần VietQR.
- Cancel flow không tạo paid order mới.

### Offline cash queue

- Offline cash order lưu vào IndexedDB `cartSyncQueue`.
- Queue lưu ClientOrderId, WorkShiftId, StaffId, StoreId, soldAt, cart snapshot và payment snapshot.
- Queue không bị xóa khi đóng WorkShift ngoại lệ.

### Phiếu tạm / tem tạm

- POS có thể in phiếu tạm bằng ClientOrderId khi offline.
- POS có thể in tem tạm nếu flag `allowOfflineTemporaryDrinkLabel` cho phép.
- Phiếu/tem tạm không thay thế hóa đơn/tem chính thức.

### Offline sync vào WorkShift cũ

- Sync gọi `/api/v1/pos/orders/sync-offline`.
- Backend dùng ClientOrderId để idempotency.
- Backend lookup WorkShift gốc bằng WorkShiftId + StaffId + StoreId.
- Order sync thành công vẫn thuộc WorkShift cũ, kể cả WorkShift đã đóng.

### Order history drawer

- Row lịch sử là summary, không dump full item/topping.
- Drawer bên phải hiển thị thông tin order, WorkShiftId, Staff, Store, payment breakdown, Sync state, receipt state, drink label state và items.
- Unsynced Offline Order từ IndexedDB hiển thị bằng mã tạm ClientOrderId.

### Reprint hóa đơn/tem

- Drawer có nút `In lại hóa đơn` và `In lại tem`.
- Chỉ enable khi order có backend OrderId và đã thanh toán.
- Reprint gọi endpoint riêng, không tạo order/payment, không trừ kho, không sửa WorkShift.
- Receipt reprint không kick cash drawer.

### WorkShift close blocking

- Normal close bị chặn khi có active payment guard trong POS.
- Normal close bị chặn khi local IndexedDB có Offline Order Pending/Syncing/Failed trong WorkShift hiện tại.
- Backend defense-in-depth chặn AwaitingPayment/Unpaid POS order.

### Supervisor exception close/reconciliation

- Supervisor/manager có thể đóng WorkShift ngoại lệ khi còn Offline Order local chưa sync.
- Bắt buộc supervisor PIN và exception reason.
- Persist IsExceptionClosed, ExceptionCloseReason, ExceptionClosedByStaffId, ExceptionClosedAt và offline queue summary trên WorkShift.
- RequiresReconciliation được set true khi đóng ngoại lệ.

### Inventory deduction guardrails

- Inventory Deduction chỉ chạy cho order Completed + Paid.
- Guard bằng `ReferenceOrderId + SALES_DEDUCTION`.
- Duplicate webhook/offline sync/commit retry không trừ kho lại.
- BOM cover main drink, size recipe, topping và ChildRecipe/BTP theo behavior hiện tại.
- Blind Selling/Negative Inventory được chấp nhận và ghi InventoryTransaction.

## 4. DB/domain-level hardening đã có

### WorkShift exception/reconciliation fields

Fields trên `WorkShifts`:

- `IsExceptionClosed`
- `ExceptionCloseReason`
- `ExceptionClosedByStaffId`
- `ExceptionClosedAt`
- `OfflineOrderCountAtClose`
- `OfflineEstimatedTotalAtClose`
- `OfflineCashTotalAtClose`
- `RequiresReconciliation`
- `HasLateOfflineSync`
- `LateOfflineSyncCount`
- `LastLateOfflineSyncedAt`

Ý nghĩa:

- Lưu trạng thái đóng ca ngoại lệ đúng domain WorkShift.
- Lưu summary Offline Order tại thời điểm đóng ca ngoại lệ.
- Đánh dấu ca cần đối soát lại khi có late offline sync.
- Bảo vệ `ActualEndingCash`: late sync không sửa ngầm số tiền đã chốt.

Migration #85 đã được tạo/test local nhưng không commit theo workflow team.

### ClientOrderId idempotency

- `Orders.ClientOrderId` là field persistent.
- EF config có unique filtered index `IX_Orders_ClientOrderId_Unique` với filter `ClientOrderId IS NOT NULL`.
- Offline Sync check existing order trước khi tạo mới.
- Race condition do unique constraint được catch và lookup lại bằng ClientOrderId.

### PayOS webhook idempotency

- Webhook transition từ AwaitingPayment/Unpaid sang Paid/Completed dùng `ExecuteUpdateAsync` có điều kiện state trong DB transaction.
- Nếu order đã paid thì webhook duplicate trả về `ALREADY_PAID`.
- Duplicate webhook không dispatch print lại.
- Inventory duplicate/repair đi qua `DeductStockForCommittedOrderAsync`.

### Inventory deduction guard

- `InventoryTransactions.ReferenceOrderId` gắn với order đã commit.
- `DeductStockForCommittedOrderAsync` chỉ cho order Completed + Paid.
- Nếu đã có `SALES_DEDUCTION` transaction cho ReferenceOrderId thì return idempotent/no-op.
- Nếu order paid nhưng lần trừ kho trước fail trước khi tạo transaction, retry có thể repair và trừ đúng một lần.

### Hardening bổ sung trong pass này

- Đã loại bỏ việc ghi WorkShift cash discrepancy vào `InvoiceAuditLog`.
- `InvoiceAuditLog` là domain hóa đơn/supervisor bypass, không dùng cho WorkShift reconciliation.
- Cash discrepancy hiện được persist đúng domain trên `WorkShift` bằng `ExpectedEndingCash`, `ActualEndingCash`, `CashDiscrepancy`, `DiscrepancyReason`.
- Không thêm field mới.
- Không tạo migration mới.

## 5. Follow-up không làm ngay

- Cash payment confirmation modal với `receivedAmount` và `changeAmount`.
- Persisted PrintJob audit/status để biết `Chờ in`, `Đã gửi in`, `In lỗi`, `Đã in lại`.
- Persist temporary receipt/label printed flags nếu cần UI accuracy sau reload.
- Optional DB unique constraint cho `ReferenceOrderId + SALES_DEDUCTION` nếu muốn extreme race protection ở DB-level.
- Docker dev environment.
- PaymentReference unique/index có thể cần xem lại nếu sau này không còn encode OrderId trong PayOS orderCode.

## 6. Known risks

- `CafeChain/appsettings.json` đang dirty local.
- `CafeChain/FIX.md` đang dirty local.
- Migration workflow: team sẽ delete/regenerate migrations ở merge time, thường là một `InitialCreate`.
- Migration #85 đang local uncommitted theo chủ trương hiện tại.
- Backend không thể thấy IndexedDB queues trên thiết bị khác, nên blocker Offline Order local là device-local.
- PrintBridge hiện chỉ xác nhận đã gửi command qua SignalR, chưa xác nhận máy in đã in vật lý.
- Chưa có persisted PrintJob audit/status nên UI chỉ nên nói "đã gửi lệnh", không nói "đã in thành công".
- Inventory idempotency hiện là code-level/transaction-level; chưa có DB unique constraint cho `ReferenceOrderId + SALES_DEDUCTION`.

## 7. Verification

Sau hardening pass này:

- `dotnet test .\CafeChain\CafeChain.slnx`: pass, 52/52.
- `npm run lint` trong `CafeChain.Frontend`: pass.
- `npm run build` trong `CafeChain.Frontend`: pass.

Warnings còn lại:

- Frontend build có warning từ dependency `@microsoft/signalr` về `/*#__PURE__*/` annotation do Rolldown ignore.
- Frontend build có warning chunk JS lớn hơn 500 kB.
- Các warning này không làm fail build.

## 8. Demo checklist

- Mở StaffHub, handoff JWT vào POS.
- Mở WorkShift trước khi thanh toán.
- Bán cash online, confirm order paid, inventory deduction và lệnh in được gửi.
- Bán VietQR/PayOS, confirm order AwaitingPayment, webhook confirmed thành Paid/Completed.
- Gửi duplicate PayOS webhook, confirm không print/trừ kho trùng.
- Tạo split payment cash + VietQR, confirm pending flow khóa cart.
- Cancel split payment, confirm không commit paid order.
- Chuyển offline, bán cash, confirm Offline Order vào IndexedDB với ClientOrderId.
- In phiếu tạm offline.
- In tem tạm offline nếu flag cho phép.
- Sync offline khi online lại, confirm order vào WorkShift cũ.
- Sync duplicate ClientOrderId, confirm không tạo order/trừ kho/print trùng.
- Mở lịch sử đơn, confirm row summary không tràn layout.
- Mở drawer chi tiết order backend.
- Mở drawer cho unsynced Offline Order local.
- In lại hóa đơn từ drawer cho order đã thanh toán.
- In lại tem từ drawer cho order đã thanh toán.
- Thử đóng WorkShift thường khi có active payment, confirm bị chặn.
- Thử đóng WorkShift thường khi có Offline Order Pending/Syncing/Failed, confirm bị chặn.
- Đóng WorkShift ngoại lệ bằng supervisor PIN + reason.
- Sync Offline Order sau khi WorkShift đã đóng ngoại lệ, confirm WorkShift có `RequiresReconciliation` và `HasLateOfflineSync`.
- Confirm `ActualEndingCash` không bị sửa sau late sync.
- Confirm Negative Inventory được chấp nhận và InventoryTransaction được ghi.
