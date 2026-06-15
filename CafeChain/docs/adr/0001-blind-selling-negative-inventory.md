# Blind Selling + Negative Inventory cho Offline POS

Khi iPad mất kết nối mạng, hệ thống POS cho phép thu ngân tiếp tục bán hàng mà không kiểm tra tồn kho server (Blind Selling). Inventory Deduction chỉ chạy tại thời điểm Sync — khi Offline Order được đẩy lên cloud. Nếu tồn kho không đủ, `StoreInventory.AvailableQty` được phép xuống giá trị âm (Negative Inventory) thay vì chặn đơn hàng.

## Considered Options

1. **Replicate BOM sang IndexedDB** — iPad tự tính tồn kho local, cảnh báo trước khi bán.
   Rejected: BOM đệ quy 5 tầng (`MAX_BOM_DEPTH = 5`), kèm UnitConversion, quá phức tạp để cache offline. Tồn kho local sẽ drift ngay khi có iPad thứ 2 cùng bán.

2. **Hard-block khi kho = 0 tại lúc Sync** — Từ chối commit Offline Order nếu nguyên liệu hết.
   Rejected: Đơn hàng đã bán thực tế cho khách (thu tiền mặt, phục vụ ly nước). Từ chối ghi nhận doanh thu tạo ra sai lệch kế toán nghiêm trọng hơn kho âm.

3. **Blind Selling + Soft-block (chấp nhận kho âm)** — ✅ Chọn.
   Đây là cách vận hành thực tế của chuỗi F&B (Highlands, Starbucks). Tồn kho vật lý tại quán luôn là chân lý. Kho âm trên hệ thống được giải quyết bởi Inventory Reconciliation (kiểm kê cuối ca).

## Consequences

- `InventoryDeductionService.DeductStockForOrderAsync` giữ nguyên logic `AvailableQty -= convertedQty` không chặn giá trị âm.
- Dashboard Admin cần hiển thị cảnh báo khi `AvailableQty < 0` để Quản lý kho biết và đối soát.
- Quy trình "Inventory Reconciliation" (kiểm kê cuối ca) là bắt buộc để cân bằng kho sau khi sync Offline Order.
