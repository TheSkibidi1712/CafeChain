using System.ComponentModel.DataAnnotations;

namespace CafeChain.ViewModels.Admin.Toppings
{
    public class AdminToppingVM
    {
        public int ToppingId { get; set; }

        [Required(ErrorMessage = "Tên topping là bắt buộc")]
        [MaxLength(100)]
        public string Name { get; set; }

        [Required(ErrorMessage = "Giá là bắt buộc")]
        [Range(1, double.MaxValue, ErrorMessage = "Giá phải lớn hơn 0")]
        public decimal Price { get; set; }

        public string? ImageUrl { get; set; }

        // ❗ CHỈ required khi CREATE → xử lý ở Controller
        public IFormFile? ImageFile { get; set; }

        public bool Active { get; set; } = true;
    }
}
