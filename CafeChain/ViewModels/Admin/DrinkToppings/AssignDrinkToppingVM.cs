using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Admin.DrinkToppings
{
    public class AssignDrinkToppingVM
    {
        public int ToppingId { get; set; }

        [Required]
        public int DrinkId { get; set; }
    }
}
