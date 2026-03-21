using CafeChain.Models.Drinks;

namespace CafeChain.ViewModels
{
    public class HomeViewModel
    {
        // Danh sách các loại (Cà phê, Trà...) để hiện ở thanh Filter
        public List<DrinkCategory> Categories { get; set; }

        // Danh sách các món nước để hiện ở lưới sản phẩm
        public List<Drink> Drinks { get; set; }
    }
}
