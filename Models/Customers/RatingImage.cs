namespace CafeChain.Models.Customers
{
    public class RatingImage
    {
        public int RatingImageId { get; set; }

        public int RatingId { get; set; }

        public string ImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now; // 🔥 optional

        public virtual Rating Rating { get; set; }
    }
}
