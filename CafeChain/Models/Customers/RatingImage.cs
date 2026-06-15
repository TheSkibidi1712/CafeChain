namespace CafeChain.Models.Customers
{
    public class RatingImage
    {
        public int RatingImageId { get; set; }

        public int RatingId { get; set; }

        // Cloudinary
        public string ImageUrl { get; set; } = null!;

        public string PublicId { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.Now; // 🔥 optional

        public virtual Rating Rating { get; set; }
    }
}
