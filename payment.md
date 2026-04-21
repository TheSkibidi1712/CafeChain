# VAI TRÒ: Senior ASP.NET Core & Vue/React Developer (Tech Lead)
# NHIỆM VỤ: Refactor hệ thống Quản lý đơn hàng (Kanban), Checkout, Tồn kho (Logical) và Lịch sử đơn hàng cho dự án F&B (CafeChain).

# 🚫 QUY TẮC TUYỆT ĐỐI (STRICT RULES - MUST OBEY) 🚫
1. **NO DATABASE CHANGES:** TUYỆT ĐỐI KHÔNG ĐƯỢC chạm vào thư mục Migrations, DataContext, hay sửa đổi cấu trúc Model của Database. Không thêm trường (column), không đổi kiểu dữ liệu (data type).
2. **SCOPE LIMITATION:** CHỈ ĐƯỢC PHÉP thao tác trên các file liên quan trực tiếp đến 4 luồng:
   - Views/Components của Checkout, Lịch sử đơn hàng (Order History), Chi tiết đơn hàng (Order Detail).
   - Views/Components của Admin Kanban Board (Quản lý đơn hàng).
   - Các Services/Controllers xử lý logic trực tiếp cho 4 luồng trên (VD: `OrderService`, `PaymentController`, `PayOSService`).
3. Tôn trọng kiến trúc hiện tại, không tự ý viết lại (rewrite) các core class không liên quan.

Hãy thực hiện từng Phase dưới đây:

## PHASE 1: TỐI ƯU UI KANBAN BOARD (CHO BARTENDER)
**Scope:** Admin Order Kanban UI.
**Mục tiêu:** Giảm thiểu số lần click cho nhân viên pha chế.
1. **Thiết kế lại Order Card (Thẻ đơn hàng):** - Trên mỗi thẻ đơn hàng (đặc biệt ở cột `ĐANG PHA CHẾ`), hiển thị trực tiếp danh sách món ăn: `[Tên món] - [Size] - [Toppings (nếu có)]`. Không bắt người dùng phải click vào Detail mới thấy.
2. **Nút Thao tác nhanh (Quick Actions):**
   - Thêm nút `Hoàn thành` (Complete) trực tiếp trên Card. Bấm vào là trigger API chuyển đơn sang cột `CHỜ LẤY / GIAO`.
   - Thêm nút `🖨️ In Đơn` (Print Receipt). Khi bấm vào, trigger function gọi màn hình in (`window.print()` hoặc template in hóa đơn).
   - *Lưu ý:* Vẫn giữ nguyên tính năng click vào nền của Card để xem Popup Chi tiết đơn.

## PHASE 2: TỐI ƯU LUỒNG ĐỐI TÁC NGOÀI (ONE-CLICK HANDOVER)
**Scope:** `Index.cshtml` (Admin Kanban Board).
**Mục tiêu:** Do hệ thống vận hành 100% qua đối tác giao hàng ngoài (AhaMove, Grab) và tích hợp qua Webhook, loại bỏ hoàn toàn các Modal/Popup yêu cầu nhập liệu thủ công (nhập biển số, mã vận đơn, chọn shipper). Tối ưu thao tác "1-Click".

**Yêu cầu xử lý (Action Required):**
1. **Loại bỏ Modal cũ:** Xóa bỏ hoàn toàn luồng hiển thị Popup "Chọn Shipper giao hàng" (cả nội bộ lẫn đối tác).
2. **Thêm nút Quick Action cho cột [CHỜ LẤY / GIAO]:** - Trên mỗi thẻ đơn hàng đang ở trạng thái `ReadyForPickup`, bổ sung một nút bấm nổi bật: `🛵 Giao cho Tài xế` (Handover to Partner).
3. **Xử lý Logic 1-Click:** - Khi Admin bấm `🛵 Giao cho Tài xế`, gọi trực tiếp API/AJAX update trạng thái đơn hàng thành `InDelivery` (Đang giao).
   - Thẻ đơn hàng tự động ẩn khỏi cột [CHỜ LẤY / GIAO] (hoặc chuyển sang một cột [ĐANG GIAO] nếu có). Không cần nhập thêm bất kỳ Text Input nào.
4. **Giả lập Webhook (Dành cho Testing/Demo):**
   - Thêm một nút nhỏ ở góc màn hình Kanban (hoặc trên Header): `[Test] Simulate Partner Webhook`. 
   - Nút này sẽ gọi một API quét tất cả các đơn đang ở trạng thái `InDelivery` và chuyển thẳng thành `Completed` (Hoàn thành) để giả lập việc Server của Grab/Ahamove gọi Webhook trả kết quả về hệ thống.
   
## PHASE 3: REAL INVENTORY DEDUCTION & CONCURRENCY CONTROL
**Scope:** `MockInventoryService.cs` (Replace with Real Logic), `OrderService.cs`.
**Mục tiêu:** Áp dụng trừ kho thực tế dựa trên Recipe và xử lý Race Condition. KHÔNG THAY ĐỔI DB SCHEMA.

**1. Xây dựng hàm CalculateRequiredIngredients (Tính toán nguyên liệu):**
- Khi có Order, lặp qua danh sách `OrderDetails`.
- Với mỗi `DrinkId`, tìm `Recipe` tương ứng (`Recipes.DrinkId == DrinkId`).
- Duyệt qua `RecipeDetails` để lấy danh sách nguyên liệu cần thiết. 
- **Lưu ý đệ quy (Recursive/Flatten):** Chú ý constraint `CK_RecipeDetail_OnlyOneSource`. Nếu `RecipeDetail` có `IngredientId`, thì cộng dồn `Quantity * OrderDetail.Quantity`. Nếu có `ChildRecipeId`, phải lấy tiếp `RecipeDetails` của ChildRecipe đó để tính ra `IngredientId` gốc.

**2. Áp dụng Optimistic Concurrency (Chống âm kho):**
- Đảm bảo entity `StoreInventory` trong EF Core model đã được cấu hình `.IsRowVersion()` hoặc attribute `[Timestamp]` cho thuộc tính `RowVersion`.
- Trong hàm `ReserveInventoryForOrderAsync(int storeId, List<OrderDetail> items)`:
  - Lấy danh sách `StoreInventory` của Store đó dựa trên danh sách `IngredientId` vừa tính ở bước 1.
  - So sánh: Nếu bất kỳ `StoreInventory.AvailableQty < RequiredQty`, lập tức ném ra custom exception (VD: `InventoryShortageException`) với thông báo lỗi thân thiện để hiển thị cho UI.
  - Nếu đủ kho: Thực hiện logic giữ kho (Trừ `AvailableQty` và Cộng `ReservedQty` tương ứng với số lượng RequiredQty).
  - Bọc khối `await _context.SaveChangesAsync();` trong `try-catch`.
  - Bắt lỗi `DbUpdateConcurrencyException`: Nếu xảy ra (tức là có người khác vừa tranh mua xong), ném ra lỗi *"Nguyên liệu vừa hết do có khách hàng khác đặt mua. Vui lòng thử lại."*

**3. Hàm ReleaseInventory (Hoàn kho khi Hủy đơn):**
- Viết logic ngược lại cho `ReleaseInventoryForOrderAsync`: Lấy lại danh sách nguyên liệu đã tiêu hao của Order, Cộng trả lại vào `AvailableQty` và Trừ đi ở `ReservedQty`.

**Strict Rule:** Sử dụng transaction `IDbContextTransaction` cho toàn bộ quá trình Tạo Order và Trừ kho để đảm bảo tính toàn vẹn (ACID).

## PHASE 4: CHECKOUT, FIX LỖI PAYOS 231 & TIMEOUT
**Scope:** Checkout UI, `PaymentController`, `PayOSService`.
**Mục tiêu:** Sửa lỗi tạo trùng đơn thanh toán và xử lý đơn treo.
1. **Nút Hủy (Cancel):** Thêm nút `Hủy thanh toán` ngay trong form hiển thị QR Code. Nút này gọi API để hủy Order và cộng lại Tồn kho.
2. **Fix lỗi PayOS [231] (Đơn thanh toán đã tồn tại):** - Giải pháp: Khi gọi hàm tạo link PayOS cho một Order đã tồn tại, phải sinh ra `orderCode` mới (VD dùng số OrderId nối với Tick hiện tại: `long payosOrderCode = long.Parse($"{order.Id}{DateTime.Now.ToString("fff")}");`). Không được gửi lại ID cũ. Đảm bảo UI show đúng chuẩn VietQR.
3. **Cơ chế Timeout (Auto-Cancel):**
   - Đặt thời gian đếm ngược 2 phút trên trang hiển thị QR Code.
   - Nếu timeout: Tự động gọi API hủy đơn hàng (Cancel), release lại tồn kho, và redirect User về trang `Lịch sử đơn hàng`.
   - Đối với trường hợp User thoát ngang (Close tab): Đảm bảo `OrderCleanupWorker` (hoặc background task tương đương hiện có) quét và tự động hủy các đơn Pending quá 2 phút và hoàn tồn kho.

## PHASE 5: LỊCH SỬ ĐƠN HÀNG & UI PHÂN TRANG
**Scope:** Order History UI, Order Detail UI.
**Mục tiêu:** Rõ ràng, dễ sử dụng theo chuẩn E-commerce.
1. Trạng thái đơn hàng phải được hiển thị bằng Badges màu sắc nổi bật (VD: Chờ thanh toán - Vàng, Đang pha chế - Cam, Hoàn thành - Xanh lá, Đã hủy - Xám/Đỏ).
2. **Logic Phân trang (Pagination UX):**
   - Nút `Trước` (Previous) phải bị **vô hiệu hóa (Disabled/Mờ đi)** nếu đang ở Trang 1.
   - Nút `Tiếp` (Next) phải bị **vô hiệu hóa (Disabled/Mờ đi)** nếu đang ở Trang cuối cùng.
   - Không được dùng kiểu in đậm nút ngược logic.