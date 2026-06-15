# CafeChain POS

Hệ thống Point of Sale cho chuỗi F&B, chạy trên iPad Pro M5 qua Safari, kết nối backend ASP.NET Core trên cloud.

## Language

**WorkShift**:
Phiên két tiền POS — đại diện cho một lượt mở/đóng két của Thu ngân. Chứa StartingCash, ExpectedEndingCash, ActualEndingCash.
_Avoid_: Cash session, POS session, ca làm việc

**StaffShift**:
Ca chấm công nhân sự — ghi nhận giờ vào/ra thực tế bằng Face ID, dùng để tính lương.
_Avoid_: Ca làm việc (khi nói về chấm công)

**BOM (Bill of Materials)**:
Cấu trúc đệ quy Recipe → RecipeDetail, mỗi RecipeDetail trỏ đến Ingredient (nguyên liệu) hoặc ChildRecipe (bán thành phẩm).
_Avoid_: Công thức, formula

**Offline Order**:
Đơn hàng được tạo trên iPad khi mất mạng, lưu tạm vào IndexedDB, chờ đồng bộ lên cloud khi mạng phục hồi.
_Avoid_: Cached order, pending order

**Sync**:
Quá trình đẩy Offline Order từ IndexedDB lên Backend API khi phát hiện mạng phục hồi.
_Avoid_: Upload, push

**Inventory Deduction**:
Hành động trừ kho nguyên liệu theo BOM khi đơn hàng được commit. Chạy qua IInventoryDeductionService, không truy cập trực tiếp DB kho từ POS.
_Avoid_: Stock deduction, trừ hàng

**Silent Print**:
In hóa đơn/phiếu pha chế qua máy in LAN mà không hiện Print Dialog trên iPad. Yêu cầu Print Proxy trung gian vì Safari không hỗ trợ Raw Socket.
_Avoid_: Background print, auto print

**Print Proxy**:
Thành phần trung gian (SignalR Hybrid Bridge) nhận lệnh in từ cloud backend, chuyển tiếp sang máy in LAN tại quán qua TCP/ESC/POS.
_Avoid_: Print server, print agent

**Blind Selling**:
Chế độ bán hàng khi iPad offline — thu ngân bán mà không kiểm tra tồn kho server. Tồn kho vật lý tại quán là chân lý.
_Avoid_: Offline selling, unchecked selling

**Negative Inventory**:
Trạng thái `StoreInventory.AvailableQty < 0` xảy ra khi Offline Order sync lên và trừ kho vượt quá tồn kho hiện tại. Được chấp nhận — không chặn đơn hàng.
_Avoid_: Stock deficit, backorder

**Inventory Reconciliation**:
Quy trình kiểm kê cuối ca để đối soát tồn kho thực tế với tồn kho hệ thống, đặc biệt quan trọng sau khi sync Offline Order gây Negative Inventory.
_Avoid_: Stock take (đó là StockTakeSession — khái niệm rộng hơn)

**Idempotency Key**:
Mã định danh duy nhất gắn với mỗi request để đảm bảo retry không gây side-effect trùng lặp. Trong POS, đây là ClientOrderId.
_Avoid_: Dedup key, request token

**ClientOrderId**:
UUID v4 sinh tại iPad ngay lúc nhấn "Thanh toán", lưu vĩnh viễn trên model `Order` với Unique Constraint. Dùng để chống trùng đơn khi Sync Offline Order.
_Avoid_: LocalId, offlineId, requestKey
