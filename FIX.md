# VAI TRÒ: Senior Fullstack ASP.NET Core & F&B System Architect
# TÁC VỤ: Xử lý triệt để 3 luồng nghiệp vụ: Điểm thành viên, Hệ thống Review và Tồn kho trang chủ.

# RÀNG BUỘC (CRITICAL):
- Bọc Try-Catch cẩn thận.
- Ở trang chủ, việc gọi Check Tồn kho phải tối ưu để tránh chết Server (N+1 Query).
- Dữ liệu trả về View phải thông qua ViewModel.

Hãy thực thi tuần tự 3 Phase sau:

## PHASE 1: LOGIC HẠNG THÀNH VIÊN (LOYALTY SYSTEM)
**1. Tại `CustomerService` (hoặc `ProfileController`):**
- Lấy `TotalPoints` của khách hàng đang đăng nhập (Từ bảng `CustomerPoints` hoặc tính tổng Balance từ `PointTransactions`).
- Query bảng `MemberLevels` (Sắp xếp theo `MinPoints` ASC) để tìm Hạng hiện tại: `MinPoints <= TotalPoints`.
- Tìm Hạng tiếp theo (Next Tier) và tính `PointsNeeded = NextTier.MinPoints - TotalPoints`.
**2. Tại View `Profile/Index.cshtml`:**
- Xóa bỏ các dữ liệu hardcode "GOLD MEMBER", "1,250 điểm".
- Hiển thị Tên hạng (Name) từ DB, in ra Tổng điểm thật.
- Thanh Progress bar: Tính % hoàn thành tới hạng tiếp theo = `(TotalPoints - CurrentTier.MinPoints) / (NextTier.MinPoints - CurrentTier.MinPoints) * 100`.
- Hiển thị dòng text: "Bạn còn thiếu {PointsNeeded} điểm để lên hạng {NextTier.Name}". (Nếu max hạng thì hiển thị "Bạn đang ở hạng cao nhất").

## PHASE 2: HỆ THỐNG ĐÁNH GIÁ (REVIEWS & REACTIONS)
**1. Sửa lỗi Ghi đè Review (`DrinkService.cs`):**
- Tìm hàm `SubmitReviewAsync`. 
- BỎ NGAY đoạn logic `FirstOrDefaultAsync` tìm `existingReview` rồi `Update()`.
- Chuyển thành LUÔN LUÔN tạo mới (`new Rating { ... }` rồi `_context.Ratings.Add()`) để khách mua nhiều lần được đánh giá nhiều lần.
**2. Sửa lỗi Thả Cảm Xúc (View `Drink/Detail.cshtml`):**
- Tìm đoạn Javascript xử lý thả cảm xúc (Reaction).
- Đảm bảo mỗi icon (Thích, Yêu, Haha, Wow, Buồn, Phẫn nộ) có thuộc tính `data-type="1/2/3/4/5/6"`.
- Khi click, JS phải lấy đúng `type` này nhét vào payload AJAX gửi lên API `ToggleReactionAsync`. Cập nhật lại UI đúng icon người dùng vừa chọn.
**3. Xây dựng Bộ Lọc Review (View `Drink/Detail.cshtml`):**
- Viết Javascript thuần hoặc jQuery cho các nút: Tất cả, 5 Sao, 4 Sao, 3 Sao, Có hình ảnh.
- Khi bấm vào nút lọc, lặp qua tất cả các khối `.review-item` (gắn thêm `data-stars` và `data-has-image` vào HTML trước). Ẩn/Hiện (dùng `classList.toggle('d-none')`) các thẻ không thỏa mãn điều kiện.

## PHASE 3: HIỂN THỊ "HẾT HÀNG" NGOÀI TRANG CHỦ
**1. Cập nhật `HomeViewModel.cs` & `HomeController.cs`:**
- Trong `DrinkItemViewModel`, thêm property `public bool IsAvailable { get; set; }`.
- Trong `HomeController`, sau khi fetch xong các list đồ uống (BestSellers, CoffeeList...), lặp qua chúng và gọi service kiểm tra tồn kho:
  `item.IsAvailable = await _drinkService.CheckDrinkAvailabilityAsync(item.DrinkId, currentStoreId);`
**2. Cập nhật UI Trang chủ (`Views/Home/Index.cshtml`):**
- Tại vòng lặp in ra Card sản phẩm, kiểm tra `if (!item.IsAvailable)`.
- Nếu hết hàng: 
  - Phủ một lớp overlay `div` mờ (rgba trắng hoặc xám) lên tấm hình.
  - Căn giữa một Text box hoặc Badge màu đỏ/đen ghi chữ **"HẾT HÀNG"**.
  - CSS ảnh: `filter: grayscale(100%); opacity: 0.7;`.
  - Disable sự kiện onClick/thẻ <a> để không cho chui vào chi tiết sản phẩm hoặc không cho bấm nút "Thêm vào giỏ".