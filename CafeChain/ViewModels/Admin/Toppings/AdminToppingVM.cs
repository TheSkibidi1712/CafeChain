using System.ComponentModel.DataAnnotations;

using CafeChain.ViewModels.Admin.Recipes;

namespace CafeChain.ViewModels.Admin.Toppings
{
    public class AdminToppingVM
    {
        public int ToppingId { get; set; }

        [Required(ErrorMessage = "Mã topping là bắt buộc")]
        [MaxLength(50, ErrorMessage = "Mã topping tối đa 50 ký tự")]
        public string ToppingCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên topping là bắt buộc")]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Giá là bắt buộc")]
        [Range(1, double.MaxValue, ErrorMessage = "Giá phải lớn hơn 0")]
        public decimal Price { get; set; }

        public string? ImageUrl { get; set; }

        // ❗ CHỈ required khi CREATE → xử lý ở Controller
        public IFormFile? ImageFile { get; set; }

        public bool Active { get; set; } = true;

        public ToppingConsumptionSourceVM ConsumptionSource { get; set; } = new();
    }
}
