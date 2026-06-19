using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Admin.DrinkSizes
{
    public class AssignDrinkSizeVM
    {
        public int SizeId { get; set; }

        [Required]
        public int DrinkId { get; set; }

        [Range(1000, 1000000, ErrorMessage = "Giá không hợp lệ")]
        public decimal Price { get; set; }
    }
}
