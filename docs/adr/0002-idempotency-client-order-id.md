# Idempotency cho Offline Order Sync bằng ClientOrderId trên Model Order

Khi iPad sync Offline Order lên cloud, mạng chập chờn có thể gây retry hoặc thu ngân bấm Sync nhiều lần. Backend phải đảm bảo mỗi đơn hàng chỉ được commit đúng 1 lần (Idempotent).

Quyết định: Thêm trường `ClientOrderId` (Guid, nullable, Unique Constraint) trực tiếp trên model `Order`. iPad sinh UUID v4 ngay lúc nhấn "Thanh toán" và đóng gói cứng vào IndexedDB. Backend kiểm tra `ClientOrderId` trước khi commit — nếu đã tồn tại thì trả về OrderId cũ mà không tạo đơn mới.

## Considered Options

1. **Dùng bảng `RequestDeduplication` hiện có** — Lưu UUID vào bảng log tạm, set TTL (ExpiredAt).
   Rejected: `ClientOrderId` là thuộc tính nghiệp vụ cốt lõi của đơn hàng offline, cần lưu vĩnh viễn để đối soát kế toán. `RequestDeduplication` là API log tạm thời với TTL — sai ngữ nghĩa.

2. **Sinh UUID lúc bấm Sync** thay vì lúc Thanh toán.
   Rejected: Nếu sync 2 lần trước khi `localStorage.removeItem` chạy → 2 batch có cùng nội dung nhưng UUID khác → trùng đơn.

3. **Thêm `ClientOrderId` trên model `Order` + Unique Constraint** — ✅ Chọn.
   UUID sinh tại thời điểm thanh toán → bất biến → retry-safe. Unique Constraint ở DB level chặn trùng ngay cả khi 2 request race condition.

## Consequences

- Migration mới: Thêm `ClientOrderId` (Guid?, nullable) vào `Order`, tạo Unique Index filtered (`WHERE ClientOrderId IS NOT NULL`).
- `CommitOrderAsync` và `SyncOfflineOrders` phải check `ClientOrderId` trước khi insert.
- Đơn hàng online (bán trực tiếp khi có mạng) có thể để `ClientOrderId = null` — chỉ offline order mới cần.
