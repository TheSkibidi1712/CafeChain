Bạn hãy đóng vai là một Senior Developer có 20 năm kinh nghiệm, chuyên về ASP.NET Core MVC theo mô hình Layered Architecture. Hãy phân tích và refactor lại source code theo đúng nghiệp vụ, đúng kiến trúc MVC, hạn chế làm phình Controller và đảm bảo code dễ bảo trì.

## Bối cảnh nghiệp vụ

Hiện tại hệ thống CafeChain đang có các chức năng quản lý Category, Drink, Size và AssignDrink trong khu vực Admin. Tôi muốn bạn refactor và bổ sung nghiệp vụ theo các yêu cầu bên dưới.

## Yêu cầu bắt buộc

### 1. Sửa Index của AdminCategory

Hiện tại chức năng tạo Category đã có phần icon, nhưng ở màn hình Index của AdminCategory vẫn chưa hiển thị icon lên View.

Hãy kiểm tra và refactor lại đầy đủ các phần liên quan như:

* Model nếu cần.
* ViewModel hoặc DTO nếu đang thiếu field icon.
* Service nếu dữ liệu icon chưa được mapping.
* Repository nếu query chưa lấy icon.
* View Index nếu chưa render icon.
* CSS nếu cần hiển thị icon đẹp và đồng bộ giao diện.

Mục tiêu: Khi vào trang Index của AdminCategory, mỗi Category phải hiển thị được icon đã tạo trước đó.

---

### 2. Bổ sung nghiệp vụ AssignDrink trong AdminSize

Tôi muốn bạn xử lý lại nghiệp vụ khi gắn Size cho Drink như sau:

Khi AssignDrink ở AdminSize:

* Những Size có đơn vị dạng `ml` hoặc `l` chỉ được phép gắn cho Drink có `ProductType` là `Retail`.
* Các Drink có `ProductType` là `Handcrafted` chỉ nên được gắn các size kiểu ly như `S`, `M`, `L`, `XL`.
* Tránh trường hợp các sản phẩm bán sẵn như nước ngọt, Sting, Coca, v.v. bị gắn size kiểu `S`, `M`, `L`, `XL`.
* Tránh trường hợp các món pha chế bị gắn size kiểu `150ml`, `200ml`, `250ml`, `300ml`.

Bạn có thể cải thiện nghiệp vụ này sao cho hợp lý hơn trong thực tế. Nếu cần, bạn có thể đề xuất cập nhật database để việc kiểm tra và mở rộng sau này dễ hơn.

---

## Dữ liệu Model hiện tại

```csharp
using CafeChain.Models.Customers;
using CafeChain.Models.Stores;
using CafeChain.Models.Orders;

namespace CafeChain.Models.Drinks
{
    public class Drink
    {
        public int DrinkId { get; set; }
        public string DrinkCode { get; set; }
        public int? CategoryId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int ProductTypeId { get; set; }
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }

        public decimal? CalculatedCogs { get; set; }

        public virtual DrinkCategory Category { get; set; }
        public virtual ProductType ProductType { get; set; }
        public virtual ICollection<DrinkImage> DrinkImages { get; set; }
        public virtual ICollection<DrinkSize> DrinkSizes { get; set; }
        public virtual ICollection<DrinkTopping> DrinkToppings { get; set; }
        public virtual ICollection<DrinkDefaultTopping> DrinkDefaultToppings { get; set; }
        public virtual ICollection<StoreDrink> StoreDrinks { get; set; }
        public virtual ICollection<Recipe> Recipes { get; set; }
        public virtual ICollection<Rating> Ratings { get; set; }
        public virtual ICollection<OrderDetail> OrderDetails { get; set; }
    }
}
```

```csharp
namespace CafeChain.Models.Enums.Drink
{
    public enum ProductTypeEnum
    {
        Handcrafted = 1, // pha chế
        Retail = 2       // đóng chai / bán sẵn
    }
}
```

```csharp
using CafeChain.Models.Orders;

namespace CafeChain.Models.Drinks
{
    public class Size
    {
        public int SizeId { get; set; }
        public string SizeCode { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Active { get; set; }

        public virtual ICollection<DrinkSize> DrinkSizes { get; set; }
        public virtual ICollection<OrderDetail> OrderDetails { get; set; }
    }
}
```

```csharp
namespace CafeChain.Models.Drinks
{
    public class DrinkSize
    {
        public int DrinkSizeId { get; set; }
        public int DrinkId { get; set; }
        public int SizeId { get; set; }
        public decimal Price { get; set; }
        public bool Active { get; set; }

        public virtual Drink Drink { get; set; }
        public virtual Size Size { get; set; }
    }
}
```

## Seed data Size hiện tại

```csharp
entity.HasData(
    new Size { SizeId = 1, Name = "S", SizeCode = "S", Description = "Kích thước nhỏ", Active = true },
    new Size { SizeId = 2, Name = "M", SizeCode = "M", Description = "Kích thước trung bình", Active = true },
    new Size { SizeId = 3, Name = "L", SizeCode = "L", Description = "Kích thước lớn", Active = true },
    new Size { SizeId = 4, Name = "XL", SizeCode = "XL", Description = "Kích thước rất lớn", Active = true },
    new Size { SizeId = 5, Name = "150ml", SizeCode = "150ML", Description = "Kích thước 150ml", Active = true },
    new Size { SizeId = 6, Name = "200ml", SizeCode = "200ML", Description = "Kích thước 200ml", Active = true },
    new Size { SizeId = 7, Name = "250ml", SizeCode = "250ML", Description = "Kích thước 250ml", Active = true },
    new Size { SizeId = 8, Name = "300ml", SizeCode = "300ML", Description = "Kích thước 300ml", Active = true }
);
```

## Yêu cầu về kiến trúc khi refactor

1. Controller chỉ dùng để điều hướng request, gọi Service và trả View hoặc Redirect. Không xử lý nghiệp vụ trực tiếp trong Controller.
2. Service chịu trách nhiệm xử lý nghiệp vụ AssignDrink, validate ProductType và Size.
3. Repository chỉ chịu trách nhiệm truy vấn dữ liệu, không chứa nghiệp vụ.
4. Nếu cần cập nhật Database, hãy đề xuất rõ cần thêm field gì, ví dụ như `SizeType`, `AllowedProductType`, hoặc enum tương ứng.
5. Nếu chưa đủ file để refactor chính xác, hãy yêu cầu tôi gửi thêm file còn thiếu, không tự ý bịa code hoặc bịa cấu trúc dự án.
6. Khi đưa code refactor, hãy chia theo từng file rõ ràng.
7. Nếu có thay đổi database, hãy hướng dẫn cách cập nhật migration và seed data.

## Kết quả tôi mong muốn

Hãy trả lời theo thứ tự:

1. Phân tích vấn đề hiện tại.
2. Đề xuất hướng refactor đúng kiến trúc.
3. Đề xuất cập nhật database nếu cần.
4. Liệt kê các file cần sửa.
5. Viết code refactor chi tiết cho từng file.
6. Giải thích luồng hoạt động sau khi refactor.
7. Nêu các case validate cần test lại.
