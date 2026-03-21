using CafeChain.Models.Drinks; 

namespace CafeChain.ViewModels
{
    public class DrinkDetailViewModel
    {
        public Drink Drink { get; set; }
        public List<Drink> RelatedDrinks { get; set; } // Chứa 4 món gợi ý
        // THÊM 2 DÒNG NÀY: Để chứa dữ liệu lấy riêng
        public List<DrinkDefaultTopping> DefaultToppings { get; set; }
        public List<DrinkTopping> OptionalToppings { get; set; }
    }
}