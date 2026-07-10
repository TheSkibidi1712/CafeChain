namespace CafeChain.Application.DTOs.Admin.Toppings
{
    public class ToppingDto
    {
        public int ToppingId { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Mã topping là bắt buộc")]
        [System.ComponentModel.DataAnnotations.StringLength(50, ErrorMessage = "Mã topping tối đa 50 ký tự")]
        public string ToppingCode { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public decimal Price { get; set; }
        // Cloudinary
        public string? ImageUrl { get; set; }

        public string? ImagePublicId { get; set; }

        // Upload file
        public IFormFile? ImageFile { get; set; }
        public bool Active { get; set; }
    }
}
