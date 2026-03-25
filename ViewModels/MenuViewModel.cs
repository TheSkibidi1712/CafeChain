using CafeChain.Models.Drinks;

namespace CafeChain.ViewModels
{
    public class MenuViewModel
    {
        // Dữ liệu đổ ra View
        public List<DrinkCategory> Categories { get; set; }
        public List<Drink> Drinks { get; set; }

        // Trạng thái bộ lọc (để binding lại giao diện)
        public int? SelectedCategoryId { get; set; }
        public decimal MinPrice { get; set; } = 0;
        public decimal MaxPrice { get; set; } = 150000;
        public string SortBy { get; set; }

        // Phân trang
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; }
    }
}
