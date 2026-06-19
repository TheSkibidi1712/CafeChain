namespace CafeChain.Application.DTOs.Admin.Toppings
{
    public class ToppingDto
    {
        public int ToppingId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        // Cloudinary
        public string? ImageUrl { get; set; }

        public string? ImagePublicId { get; set; }

        // Upload file
        public IFormFile? ImageFile { get; set; }
        public bool Active { get; set; }
    }
}
