namespace CafeChain.Application.DTOs
{
    public class CartItem
    {
        // Thêm ID độc nhất để phân biệt 2 ly giống nhau nhưng khác Topping
        public string CartItemId { get; set; } = Guid.NewGuid().ToString();

        public int DrinkId { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public decimal Price { get; set; } // Giá này sẽ = Giá Size + Giá Topping
        public int Quantity { get; set; }

        // THÊM 2 DÒNG NÀY ĐỂ HIỂN THỊ
        public string SizeName { get; set; }
        public string ToppingsDescription { get; set; } // Ví dụ: "+ Trân châu, - Không đá"

        public decimal Total => Price * Quantity;
    }
}