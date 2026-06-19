using System.Collections.Generic;

namespace CafeChain.ViewModels.Cart
{
    public class CartItemViewModel
    {
        public string CartItemId { get; set; } // UUID từ session

        public int DrinkId { get; set; }
        public int SizeId { get; set; }
        public List<int> ToppingIds { get; set; } = new List<int>();

        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public string SizeName { get; set; }
        public List<string> ToppingNames { get; set; } = new List<string>();

        public decimal Price { get; set; } // Giá snapshot từ lúc bỏ vào giỏ (chỉ để hiển thị nhanh)
        public int Quantity { get; set; }
        public string Note { get; set; }

        public decimal Total => Price * Quantity;
    }
}
