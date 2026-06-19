namespace CafeChain.Models.Drinks
{
    public class DrinkImage
    {
        public int DrinkImageId { get; set; }
        public int DrinkId { get; set; }

        // Cloudinary
        public string ImageUrl { get; set; } = null!;

        public string PublicId { get; set; } = null!;
        public bool IsDefault { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual Drink Drink { get; set; }
    }
}
