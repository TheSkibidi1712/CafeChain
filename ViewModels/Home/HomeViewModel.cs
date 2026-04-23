using CafeChain.Models.Drinks;

namespace CafeChain.ViewModels
{
    public class HomeViewModel
    {
        // Danh sách các loại (Cà phê, Trà...) để hiện ở thanh Filter
        public List<DrinkCategory> Categories { get; set; }

        // Danh sách các món nước để hiện ở lưới sản phẩm (theo từng Category)
        public List<Drink> Drinks { get; set; }

        public CafeChain.Models.Vouchers.WheelConfig? ActiveWheel { get; set; }

        // Món bán chạy nhất (Top 8)
        public List<DrinkItemViewModel> BestSellers { get; set; } = new();
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
}
