# 🛒 Báo cáo Tổng hợp Tính năng: Thanh toán & Lịch sử Đơn hàng (Checkout & Order History)

**Ngày cập nhật:** 16/04/2026
**Mô-đun:** Checkout, Order History, Order Details, Authentication

Tài liệu này tổng hợp chi tiết toàn bộ các hạng mục đã phát triển, kiến trúc hệ thống áp dụng và các tính năng liên quan đến luồng **Thanh toán (Checkout)**, **Lịch sử đơn hàng (Order History)** và **Chi tiết đơn hàng (Order Detail)**. 

---

## 1. 🛍️ Tính năng Thanh toán (Checkout)

Luồng kiểm duyệt và thanh toán giỏ hàng được đập đi xây lại theo chuẩn E-commerce, đảm bảo tính bảo mật và toàn vẹn dữ liệu lúc lên đơn.

### 1.1. Tái cấu trúc chuẩn Service Pattern
- **Tình trạng cũ:** Logic xử lý lưu hóa đơn phức tạp (gọi DB, tính giá, check validate) nằm rải rác và gắn chặt vào Controller.
- **Xử lý:** Tách bạch hoàn toàn Business Logic ra khỏi `CheckoutController`. Xây dựng `OrderService` đóng vai trò là cốt lõi của hệ thống Thanh toán, tiếp nhận Data từ Controller, thực hiện tính toán, validate và giao tiếp với DB. Điều này giúp Controller "sạch" (Clean Controller), mã nguồn dễ đọc và dễ bảo trì.

### 1.2. Bảo mật Zero-Trust Pricing
- **Vấn đề:** Nếu tin tưởng giá tiền từ phía giao diện client gửi lên, người dùng am hiểu công nghệ có thể can thiệp HTTP Request để đổi giá (ví dụ: sửa bill 50k thành 0đ) (Tampering).
- **Giải pháp:** Áp dụng mô hình **Zero-Trust Pricing**. Khi người dùng checkout, hệ thống bỏ qua toàn bộ giá tiền Client gửi lên. Thay vào đó, `OrderService` nhận đúng ID Sản Phẩm và ID Topping, truy xuất xuyên suốt lại vào Database theo thời gian thực (Real-time). Tổng bill sẽ được chốt lại lần cuối ở Backend để đảm bảo chính xác tới từng đồng.

### 1.3. Cơ chế Order Snapshot (Chụp nhanh dữ liệu)
- **Vấn đề:** Nếu chủ quán đổi giá một sản phẩm trên danh mục Menu chính hoặc khách hàng thay đổi/xóa địa chỉ cá nhân của họ, thì các hóa đơn cũ trong quá khứ có nguy cơ bị nhảy giá và mất thông tin người nhận gốc (Do tính chất Join Data theo khóa ngoại - Referential data).
- **Giải pháp:** Cơ chế **Snapshot**. Tại khoảnh khắc người nhấn "Đặt hàng thành công", mọi thông tin chủ chốt (Tiền lúc đó, Tên người nhận, SĐT, Địa chỉ text) đều được chuyển hóa và nhân bản thành chuỗi String nằm chết trong table `Orders`. Lịch sử các đơn hàng nhờ vậy luôn bất biến bất chấp năm tháng.

### 1.4. Quản lý Địa chỉ Giao hàng với Soft-Delete
- **Giải pháp:** Đối với các địa chỉ giao hàng KH không còn muốn giữ, chúng ta không xóa vĩnh viễn (Hard-delete) trong Database. Ta dùng kỹ thuật **Soft-Delete** (cờ `IsDeleted=true`) thuộc bảng `CustomerAddresses`. Điều này giải quyết bài toán: Người dùng không còn thấy địa chỉ đó tại màn chọn nữa, nhưng Đơn hàng cũ đang link (khóa ngoại) tới địa chỉ đó thì hệ thống không bị lỗi crash.

---

## 2. 🧾 Tính năng Lịch sử Đơn hàng (Order History)

Giao diện để người dùng tra cứu toàn bộ dữ liệu mua hàng và theo dõi trạng thái đơn hàng hiện tại.

### 2.1. Bảo mật dữ liệu tránh IDOR (Insecure Direct Object Reference)
- **Vấn đề nguy hiểm:** Một hacker là User A có thể sửa tham số ID trên thanh trình duyệt (Ví dụ: biến `/Order/1` thành `/Order/2`) để xem trộm chi phí và việc mua sắm của User B.
- **Giải pháp:** Ngăn chặn từ cấp độ Data Service. Bất kỳ hàm nào khi lấy dữ liệu Order cũng ép buộc gài điều kiện truy vấn `CustomerId == currentUserId` (Id chuẩn xác thực từ Identity/Cookie/Token). Kể cả truyền ID giả, hệ thống sẽ trả về Not Found hoặc Unauthorized.

### 2.2. Tối ưu Hiệu năng với Phân trang (Pagination)
- **Vấn đề:** Nếu một khách VIP có lịch sử cả ngàn đơn, việc dùng `.ToList()` kéo trực tiếp tất cả dữ liệu nạp lên View sẽ tốn RAM máy chủ nghiêm trọng (Memory Leak), giật lag.
- **Giải pháp:** Xây dựng Class hỗ trợ trực tiếp `PagedResult<T>`. Tại Lịch sử đơn hàng, ta kết hợp Entity Framework đếm bằng `.CountAsync()` sau đó dùng `.Skip().Take()` theo Pagesize. Hệ thống chỉ lấy đúng dữ liệu theo dòng hiển thị (VD: Lấy đợt 5 hoặc 10 đơn).

### 2.3. Trải nghiệm Giao diện (UI/UX)
- Giao diện `History.cshtml` hiển thị thanh Tab (Tabs filter) tự động đọc Query Parameter phân loại bằng Status ID (`?statusId=1, 3, 4, 5`).
- Tích hợp hệ thống Badge Labels. Tùy mã trạng thái của Order mà màu sắc sẽ hiển thị (Mới: Xanh lam, Thành công: Xanh lá, Hủy: Đỏ...)
- Trang Profile cá nhân cũng gắn Link dẫn trực tiếp vào Lịch sử. Bảo vệ qua filter `[Authorize]`.

---

## 3. 🔍 Tính năng Chi tiết Đơn hàng (Order Detail)

Nhấn vào từng Dòng trong Lịch sử, khách sẽ xem lại cụ thể món ăn mình đã order cho mã Hóa đơn đó.

### 3.1. Bất biến Dữ liệu Topping & Nâng cấp Snapshot
- Tương tự như giá tiền, danh sách tên các **Topping** khi khách mua cũng được System Snapshot ngược lại thành dạng text và gắn cố định vào `OrderToppings`.
- Khi xem Chi tiết (Detail View), hệ thống sẽ đổ dữ liệu dạng text đó ra hiển thị và DỨT KHOÁT KHÔNG kết nối (Join) lại với bảng Menu Topping chung. Điều này bảo vệ hóa đơn nếu ngày mai Quản lý Menu quyết định đổi tên món Topping đó, khách của bill tháng trước vẫn thấy y nguyên như lúc họ đã mua.

### 3.2. Cấu trúc UI 2 Cột Chuyên nghiệp (Detail.cshtml)
- Thiết kế trải layout tương tự hệ thống E-commerce thực thụ:
  - **Cột Trái (Sản phẩm & Thông tin Item):** Danh sách Image nhỏ của món uống (Thumbnail), Tên món, mớ Topping đi liền, và chi tiết yêu cầu Ghi chú từng món.
  - **Cột Phải (Hóa đơn Tổng & Tài chính):** Panel tính toán (Summary) liệt kê chi phí, Giao hàng vật lý, Giảm giá/Chiết khấu nếu có và Tổng tiền thu cuối cùng.

### 3.3. Định dạng Mã Hóa Đơn thân thiện (Order Code)
- Để khách hàng có trải nghiệm tốt, Id dài trên SQL (Ví dụ ID: 125) được convert thành cấu trúc Formatted: `#CC + 5 số` => mã Hóa đơn sẽ hiển thị trên Giao diện là `#CC00125`. Trực quan, chuyên nghiệp.

---

## 4. 🗂️ Tóm tắt các Layer bị điều chỉnh trong hệ thống

Dưới đây là cây đối tượng lập trình chi tiết nhất về nơi lưu các thay đổi:

- **Bảo mật & Authentication:** Cập nhật lại hệ thống `AccountService.cs`, `LoginResponseDto.cs` nhận thêm Payload.
- **Tầng Giao Diện (Presentation - UI/Controller):**
  - Thêm mới `OrderController.cs` và `CheckoutController.cs`.
  - Thiết kế `History.cshtml`, `Detail.cshtml`, layout 2 Cột `Index.cshtml` của phần Checkout.
- **Tầng Xử Lý (Service Layer):**
  - Áp dụng Interface contract `IOrderService.cs`.
  - Sinh ra ruột `OrderService.cs` và tinh chỉnh `CartService.cs`.
- **Tầng Nền tảng (Database / Data Tier):**
  - Migrate cấu trúc DB mới. Phục vụ Soft-delete trên `CustomerAddresses`. Lập cấu hình Fluent API trong `OrderConfiguration.cs` cho các trường Snapshot.
  - Snapshot cấu trúc tổng ở thư mục `Migrations/`.