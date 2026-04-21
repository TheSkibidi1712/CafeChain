using CafeChain.Models.Drinks;
using CafeChain.Models.Customers; // Thêm dòng này để gọi bảng Rating

namespace CafeChain.ViewModels
{
    public class DrinkDetailViewModel
    {
        public Drink Drink { get; set; }
        public List<Drink> RelatedDrinks { get; set; }
        public List<DrinkDefaultTopping> DefaultToppings { get; set; }
        public List<DrinkTopping> OptionalToppings { get; set; }

        // 🔥 THÊM DÒNG NÀY ĐỂ CHỨA REVIEW 🔥
        public List<Rating> Ratings { get; set; } = new List<Rating>();

        // Tính toán sao trung bình cho tiện
        public double AverageRating => Ratings.Any() ? Ratings.Average(r => r.Stars) : 0;
        public int TotalReviews => Ratings != null ? Ratings.Count : 0;
        public bool IsAvailable { get; set; } = true;
    }
}