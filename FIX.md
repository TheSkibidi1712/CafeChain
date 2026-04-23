# VAI TRÒ: Senior Fullstack ASP.NET Core & UI/UX F&B Architect
# DỰ ÁN: CafeChain (ASP.NET Core MVC)
# TÁC VỤ: Hoàn thiện dữ liệu động (Dynamic Data) cho toàn bộ trang chủ (Home/Index) bao gồm ViewModel, Controller và View.

# RÀNG BUỘC TỐI THƯỢNG (CRITICAL):
- BẮT BUỘC sử dụng `.Include(d => d.DrinkImages)` và `.Include(d => d.DrinkSizes)` khi truy vấn bảng Drinks. Tuyệt đối không để xảy ra lỗi N+1 Query hoặc NullReferenceException khi gọi Ảnh và Giá.
- Giao diện sử dụng Bootstrap 5 có sẵn, không tự ý viết thêm CSS rác.
- Code phải bọc Try-Catch an toàn.

Vui lòng thực thi "All-in-one" 3 bước sau:

## BƯỚC 1: TẠO/CẬP NHẬT VIEWMODEL (`HomeViewModel.cs`)
- Trong thư mục `ViewModels`, tạo class `HomeViewModel`.
- Khai báo 4 danh sách đại diện cho 4 phân hệ trên trang chủ:
  ```csharp
  public class HomeViewModel
  {
      public List<DrinkItemViewModel> BestSellers { get; set; } = new();
      public List<DrinkItemViewModel> CoffeeList { get; set; } = new();
      public List<DrinkItemViewModel> MilkTeaList { get; set; } = new();
      public List<DrinkItemViewModel> SoftDrinkList { get; set; } = new();
  }

  public class DrinkItemViewModel
  {
      public int DrinkId { get; set; }
      public string Name { get; set; }
      public decimal Price { get; set; }
      public string ImageUrl { get; set; }
      public double AverageRating { get; set; }
      public int RatingCount { get; set; }
  }
# VAI TRÒ: Senior DevOps & System Administrator
# TÁC VỤ: Tự động hóa đồng bộ file vật lý theo Database (Fix lỗi 404 Image).

Dựa trên file `CafeChain2.sql` (bảng `DrinkImages`) và thư mục `wwwroot/Images/DrinkImages` hiện tại, hãy thực hiện các bước sau:

1. **Phân tích Database:** Đọc các đường dẫn `ImageUrl` trong bảng `DrinkImages` (ví dụ: `/Images/DrinkImages/bacxiu1.jpg`, `latte1.jpg`, `nuoccamep1.jpg`...).
2. **Kiểm tra Ổ cứng:** Đối chiếu danh sách tên file đó với các file đang thực sự có trong thư mục `wwwroot/Images/DrinkImages/`.
3. **Thực thi đồng bộ (Automation):**
   - Với những file nào Database yêu cầu mà ổ cứng đang thiếu: Hãy lấy một file có sẵn bất kỳ (ví dụ `coca1.jpg` hoặc `sting1.jpg`) rồi thực hiện lệnh COPY và RENAME nó thành tên file đang thiếu đó.
   - Mục tiêu: Đảm bảo sau khi chạy xong, mọi đường dẫn trong Database đều có một file vật lý tương ứng trong thư mục `DrinkImages`.
4. **Xử lý Error-handler:** Nếu thư mục `DrinkImages` chưa tồn tại, hãy tạo mới nó.

Làm xong hãy liệt kê danh sách các file bạn đã tạo ra để tôi kiểm tra.