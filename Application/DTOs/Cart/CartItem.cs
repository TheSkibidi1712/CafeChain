namespace CafeChain.Application.DTOs
{
    public class CartItem
    {
        // ID độc nhất để phân biệt 2 ly giống nhau nhưng khác Topping (Pha xử lý cực bén của bác!)
        public string CartItemId { get; set; } = Guid.NewGuid().ToString();

        public int DrinkId { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public decimal Price { get; set; } // Giá đã bao gồm: Giá Size + Tổng giá Topping mua thêm
        public int Quantity { get; set; }

        public string SizeName { get; set; }

        // 🔥 CHỈ SỬA CHỖ NÀY: Đổi string thành List để View dễ vẽ UI
        public List<string> AddedToppings { get; set; } = new List<string>();   // VD: ["Trân châu trắng (+5.000đ)", "Khúc bạch (+7.000đ)"]
        public List<string> RemovedToppings { get; set; } = new List<string>(); // VD: ["Phô mai viên", "Thạch trái cây"]

        public decimal Total => Price * Quantity;
        public string? Note { get; set; } // 🔥 Thêm dòng này để lưu vào Session và in ra Giỏ hàng
    }
}