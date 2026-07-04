# POS Payment, Offline Sync, Print, and WorkShift PRD

Status: Approved
Owner: Wyatt / CafeChain
Source: Decision Register from grill-me session
Target repo: TheSkibidi1712/CafeChain

## 1. Problem / Goal

CafeChain POS needs a stable end-to-end workflow for selling at the counter across online and offline conditions. The POS must support payment completion, Offline Order storage and Sync, Silent Print through Print Proxy, WorkShift enforcement, order history review, and post-sale reprint actions without creating duplicate orders, duplicate Inventory Deduction, or duplicate print jobs.

The goal is to complete the POS workflow using the Decision Register already agreed in the grill-me session. This PRD must not introduce extra features outside those decisions.

## 2. Scope

- Online payment by cash.
- Online payment by VietQR/PayOS.
- Split payment with cash plus VietQR/PayOS.
- Offline Order for cash only, stored in IndexedDB/local queue.
- Temporary receipt and temporary drink label for offline operation.
- Silent Print through Print Proxy for official receipts and drink labels.
- Order history summary rows.
- Order detail drawer with payment, Sync, receipt, and drink label states.
- Manual reprint for official receipt and drink label.
- WorkShift enforcement before POS payment.
- Normal WorkShift close and supervisor/manager close exception.
- Sync by ClientOrderId as the Idempotency Key.
- Inventory Deduction through IInventoryDeductionService.

## 3. Non-goals

- Do not implement bar preparation states such as Chờ pha, Đang pha, Đã pha xong, or Đã giao khách.
- Do not support offline VietQR, offline PayOS, offline bank transfer, or manual transfer confirmation.
- Do not allow manager override to mark an offline bank transfer as paid in POS.
- Do not let POS frontend calculate or mutate inventory directly.
- Do not add loyalty, voucher, refund, order cancellation after commit, or customer account features in this PRD.
- Do not implement StaffShift features. StaffShift is a separate attendance concept.

## 4. Actors

- Thu ngân: sells items, enters payments, views order history, opens order detail, and reprints receipt or drink label.
- Supervisor/manager: can close WorkShift by exception when network is unavailable for a long time and Offline Orders cannot Sync.
- Backend POS API: commits order/payment, handles Sync, applies idempotency, triggers Inventory Deduction and print jobs.
- PayOS/VietQR: confirms online transfer payment.
- Print Proxy: receives Silent Print jobs from backend and sends ESC/POS payloads to printer/emulator.
- Quản lý: reviews WorkShift reconciliation after delayed Sync or Negative Inventory.

## 5. Core Flows

### 5.1 Cash Payment Online

1. Thu ngân opens an active WorkShift.
2. Thu ngân builds cart and enters cash payment.
3. Backend commits order and payment.
4. Backend runs Inventory Deduction through IInventoryDeductionService.
5. Backend creates official receipt and drink label Silent Print jobs through Print Proxy.
6. POS clears cart and shows success state.

### 5.2 VietQR/PayOS Online

1. Thu ngân opens an active WorkShift.
2. Thu ngân builds cart and chooses VietQR/PayOS.
3. Backend creates PayOS/VietQR payment reference.
4. POS waits for PayOS/VietQR confirmation.
5. After confirmation, backend commits order and payment.
6. Backend runs Inventory Deduction.
7. Backend creates official receipt and drink label Silent Print jobs.

### 5.3 Split Payment

1. Thu ngân opens an active WorkShift.
2. Thu ngân enters pending cash amount.
3. Thu ngân creates VietQR/PayOS payment for the remaining amount.
4. Cart is locked while the payment flow is active.
5. Order is not committed until the total amount is fully confirmed.
6. If the flow is canceled, POS reminds Thu ngân to return or adjust any pending cash amount.
7. If payment becomes fully confirmed, backend commits order/payment, runs Inventory Deduction, and sends official print jobs.

### 5.4 Offline Cash

1. POS detects offline condition.
2. POS disables VietQR/PayOS/bank transfer payment methods.
3. Thu ngân can sell with cash only.
4. POS creates Offline Order with ClientOrderId, WorkShiftId, StaffId, StoreId, soldAt, cart snapshot, and payment snapshot.
5. POS stores Offline Order in IndexedDB/local queue.
6. If customer needs paper immediately, POS can print temporary receipt.
7. If store config allows, POS can print temporary drink label.
8. When online returns, Sync sends Offline Order to backend.
9. Backend commits order/payment by ClientOrderId and runs Inventory Deduction.

### 5.5 Reprint

1. Thu ngân opens order detail drawer from order history.
2. Drawer shows official receipt and drink label state.
3. If order is synced and paid, Thu ngân can choose In lại hóa đơn or In lại tem.
4. Reprint creates an intentional reprint job. It must not create a new order or rerun Inventory Deduction.

### 5.6 WorkShift Close Exception

1. Thu ngân cannot close WorkShift normally if there are active payment flows, Offline Orders waiting to Sync, or Sync errors.
2. If network is unavailable for a long time, supervisor/manager can close WorkShift by exception.
3. POS requires an exception reason.
4. POS shows number of unsynced Offline Orders, estimated total, and local cash amount if available.
5. Offline Orders remain in IndexedDB/local queue and keep the old WorkShiftId.
6. After Sync succeeds, orders still belong to the old WorkShift.
7. WorkShift is marked for reconciliation.

## 6. State Model

### Order State

- Đang thanh toán
- Đã thanh toán
- Chờ đồng bộ
- Đồng bộ lỗi
- Đã hủy

### Receipt State

- Chưa in
- Chờ in
- Đã gửi in
- In lỗi
- Đã in lại
- Phiếu tạm đã in
- Hóa đơn chính thức đã sẵn sàng

### Drink Label State

- Chưa in
- Chờ in
- Đã gửi in
- In lỗi
- Đã in lại
- Tem tạm đã in
- Tem chính thức đã sẵn sàng

### WorkShift State

- Đang mở
- Đã đóng
- Đã đóng ngoại lệ
- Có đơn đồng bộ sau đóng WorkShift
- Cần đối soát lại

### PendingPaymentState

PendingPaymentState should contain enough data to safely continue, cancel, or change payment method before order commit:

- Cart snapshot.
- TotalAmount.
- PendingCashAmount.
- PendingVietQrAmount.
- PayOS payment reference if any.
- Selected payment methods.
- QR expiredAt if any.
- Status: Đang thanh toán, Đã đủ tiền, Đã hủy, Hết hạn QR, Chuyển phương thức.

## 7. Business Rules

- Order/payment commit is the source of truth.
- Print is a side-effect after commit.
- Official receipt and official drink label can only print after backend commits order/payment.
- Split payment can only commit when total payment is fully confirmed.
- Pending cash amount in split payment is not an official payment until order commit.
- While payment flow is active, cart is locked.
- To edit cart, Thu ngân must cancel payment flow first.
- Offline Order can only use cash payment.
- Offline Order cannot print official receipt before Sync.
- Temporary receipt and temporary drink label use ClientOrderId, not backend OrderId.
- Temporary receipt and temporary drink label do not replace official receipt or official drink label.
- If Offline Order already printed a temporary receipt, Sync success should not automatically print official receipt.
- If Offline Order already printed a temporary drink label, Sync success should not automatically print official drink label by default.

## 8. UI Requirements

- POS must not automatically open browser print dialog after payment success.
- POS should show success state after backend commit.
- POS should show all order, receipt, drink label, and WorkShift states in Vietnamese.
- Offline mode must disable VietQR/PayOS/bank transfer payment methods.
- Offline mode must allow cash payment only.
- Order history rows must be summaries, not full detail rows.
- Order history row should include order code or ClientOrderId, sold time, total amount, payment summary, order state, receipt state, drink label state, and first one or two item summaries.
- Long text must not overflow rows.
- Order detail opens as a right-side drawer.
- Drawer shows order info, WorkShiftId, Staff, Store, items, size, topping, payment breakdown, Sync state, receipt state, and drink label state.
- Drawer always shows In lại hóa đơn and In lại tem.
- Reprint buttons are disabled if order is not synced or not paid.
- Reprint buttons are enabled when order has backend OrderId and state Đã thanh toán.
- If official receipt is ready after Sync, drawer shows Hóa đơn chính thức đã sẵn sàng.
- If official drink label is ready after Sync, drawer shows Tem chính thức đã sẵn sàng.

## 9. Backend Requirements

- Every committed POS order must be linked to WorkShiftId, StaffId, and StoreId.
- Backend must support cash, VietQR/PayOS, and split payment.
- Backend must commit split payment only after full amount is confirmed.
- Backend must expose enough order detail for drawer display.
- Backend must support reprint request for official receipt.
- Backend must support reprint request for official drink label.
- Backend must support Sync Offline Order by ClientOrderId.
- Backend must not allow Offline Order Sync to create duplicate order.
- Backend must not run Inventory Deduction more than once for the same committed order.
- Backend must not create duplicate automatic print jobs for duplicate Sync or duplicate PayOS webhook.

## 10. Idempotency Requirements

- ClientOrderId is the Idempotency Key for Offline Order Sync.
- Order must have a unique constraint for ClientOrderId.
- Retrying Sync with the same ClientOrderId returns the existing committed order instead of creating another one.
- Retrying Sync must not rerun Inventory Deduction.
- Duplicate PayOS webhook must not mark payment or commit order multiple times.
- Duplicate PayOS webhook must not create duplicate automatic print jobs.
- Duplicate automatic print request must not create duplicate print jobs.
- In lại hóa đơn and In lại tem are intentional reprint actions and should create separate reprint jobs.

## 11. Print Requirements

- Silent Print through Print Proxy is the primary print path.
- Browser window.print must not be used automatically after payment success.
- Print job types:
  - Receipt.
  - DrinkLabel.
  - TemporaryReceipt.
  - TemporaryDrinkLabel.
- Print job states:
  - Chờ in.
  - Đã gửi in.
  - In lỗi.
  - Đã in lại.
- Official receipt is printed only after backend commit.
- Official drink label is printed only after backend commit.
- Temporary receipt must show:
  - PHIEU TAM - CHUA DONG BO.
  - KHONG PHAI HOA DON CHINH THUC.
  - Ma tam: <ClientOrderId>.
- Temporary drink label must show:
  - TAM - CHUA DONG BO.
  - ClientOrderId.
- If a temporary receipt was printed, Sync success should not auto-print official receipt. Drawer should show Hóa đơn chính thức đã sẵn sàng.
- If a temporary drink label was printed, Sync success should not auto-print official drink label by default. Drawer should show Tem chính thức đã sẵn sàng.
- Official drink label after Offline Order Sync can auto-print only when AutoPrintOfficialLabelAfterOfflineSync is true.

## 12. Offline / Sync Requirements

- Offline Order is stored in IndexedDB/local queue.
- Offline Order must include ClientOrderId, WorkShiftId, StaffId, StoreId, soldAt, cart snapshot, and payment snapshot.
- Offline Order remains in local queue until Sync succeeds or is explicitly resolved.
- Closing WorkShift by exception must not delete Offline Order from local queue.
- Sync sends ClientOrderId and old WorkShiftId to backend.
- Backend commits synced order into the original WorkShift.
- Synced order must not move to a newer WorkShift.
- If Sync fails, POS shows Đồng bộ lỗi and keeps the Offline Order available for retry.
- If Sync succeeds after WorkShift exception close, old WorkShift is marked for reconciliation.

## 13. WorkShift Requirements

- A WorkShift must be open before any POS payment can proceed.
- Cash, VietQR/PayOS, split payment, and Offline Order must all be tied to WorkShiftId, StaffId, and StoreId.
- Normal WorkShift close is blocked if there is any active payment flow.
- Normal WorkShift close is blocked if any Offline Order is waiting to Sync.
- Normal WorkShift close is blocked if any Offline Order has Sync error that is not resolved.
- Supervisor/manager may close WorkShift by exception when network is unavailable for a long time.
- Exception close requires a reason.
- Exception close shows number of unsynced Offline Orders, estimated total, and local cash amount.
- ActualEndingCash must not be silently changed after close.
- If ExpectedEndingCash or revenue changes after late Sync, the change must be recorded as reconciliation/audit data.
- Old WorkShift must show Cần đối soát lại after late Sync changes its business totals.

## 14. Inventory Deduction Requirements

- Inventory Deduction runs only after order/payment commit succeeds.
- Inventory Deduction does not run for cart draft.
- Inventory Deduction does not run for active payment flow.
- Inventory Deduction does not run for split payment before full amount is confirmed.
- Inventory Deduction does not run for Offline Order before Sync.
- Inventory Deduction does not run for canceled payment flow.
- Offline Order triggers Inventory Deduction only when Sync commit succeeds.
- Inventory Deduction must use IInventoryDeductionService.
- Inventory Deduction must calculate BOM for the main drink by size.
- Inventory Deduction must calculate BOM for toppings.
- Inventory Deduction must calculate ChildRecipe if present.
- Blind Selling and Negative Inventory are accepted according to ADR 0001.
- Negative Inventory or Sync after WorkShift close should be visible for Inventory Reconciliation.

## 15. Data Model Impact

- Order requires persistent ClientOrderId with unique constraint for Offline Order Sync idempotency.
- Order requires enough linkage to WorkShiftId, StaffId, StoreId, and payment records.
- Payment data must support split payment and PayOS/VietQR references.
- A pending payment state must preserve the data needed to continue, cancel, or change payment method before commit.
- Print job records should distinguish automatic print from reprint and distinguish receipt, drink label, temporary receipt, and temporary drink label.
- Print job records should expose Vietnamese UI state mapping.
- WorkShift needs enough state to represent exception close, late Sync after close, and reconciliation requirement.
- Store config naming:
  - Backend/domain C#: AllowOfflineTemporaryDrinkLabel.
  - Backend/domain C#: AutoPrintOfficialLabelAfterOfflineSync.
  - API JSON/frontend: allowOfflineTemporaryDrinkLabel.
  - API JSON/frontend: autoPrintOfficialLabelAfterOfflineSync.

## 16. Permissions

- Thu ngân can sell, start payment, create Offline Order cash, view order history, view order detail drawer, and request reprint for synced paid orders.
- Thu ngân cannot close WorkShift normally while there are active payment flows, unsynced Offline Orders, or unresolved Sync errors.
- Supervisor/manager can close WorkShift by exception when network is unavailable for a long time.
- Supervisor/manager exception close requires a reason.
- POS must not allow manager override to mark offline bank transfer as paid.

## 17. Acceptance Criteria

- Cash online order commits once, runs Inventory Deduction once, and creates official receipt/drink label print jobs once.
- VietQR/PayOS order commits only after confirmation.
- Duplicate PayOS webhook does not duplicate commit, Inventory Deduction, or automatic print jobs.
- Split payment does not commit while total is not fully confirmed.
- Canceling split payment reminds Thu ngân to return or adjust pending cash and creates no order.
- Offline cash order is stored in local queue with ClientOrderId and original WorkShiftId.
- Offline cash order can print temporary receipt when customer needs paper.
- Offline cash order can print temporary drink label only if AllowOfflineTemporaryDrinkLabel is true.
- Sync success commits Offline Order to the original WorkShift.
- Duplicate Sync by the same ClientOrderId does not create duplicate order, Inventory Deduction, or automatic print.
- Print failure does not fail committed order.
- Reprint creates a new intentional print job and does not create a new order.
- WorkShift normal close is blocked by active payment flow, unsynced Offline Order, or Sync error.
- Supervisor/manager exception close keeps Offline Order for later Sync and marks WorkShift for reconciliation after late Sync.

## 18. Demo Checklist

- Demo cash online from open WorkShift to official receipt/drink label Silent Print.
- Demo VietQR/PayOS from awaiting payment to confirmed commit and print.
- Demo duplicate PayOS webhook behavior.
- Demo split payment with pending cash and VietQR/PayOS remainder.
- Demo canceling split payment before full amount is confirmed.
- Demo offline cash order saved into IndexedDB/local queue.
- Demo temporary receipt with ClientOrderId.
- Demo temporary drink label controlled by AllowOfflineTemporaryDrinkLabel.
- Demo Sync after online returns.
- Demo duplicate Sync retry.
- Demo print failure state and manual reprint.
- Demo order history summary row.
- Demo order detail drawer with payment breakdown, Sync state, receipt state, and drink label state.
- Demo WorkShift normal close blocked by unsynced Offline Order.
- Demo supervisor/manager WorkShift exception close.
- Demo WorkShift marked Cần đối soát lại after late Sync.

## 19. Issue Slicing Guidance

Issues should be split as vertical tracer bullets, not by horizontal layers. Each issue should be demoable end-to-end across API, state, UI, and tests where applicable.

Recommended slicing direction:

- WorkShift guard for all POS payments.
- Online cash commit with official Silent Print.
- VietQR/PayOS confirmed commit with idempotent webhook handling.
- Split payment pending state and cart lock.
- Offline cash order local queue with ClientOrderId.
- Temporary receipt and temporary drink label.
- Offline Sync idempotency and original WorkShift preservation.
- Order history summary and order detail drawer.
- Reprint receipt and drink label from drawer.
- WorkShift close exception and reconciliation state.
- Inventory Deduction guardrails for online, split, offline Sync, and duplicate retry cases.

## 20. Open Questions

- None for business scope at the time of this draft.
