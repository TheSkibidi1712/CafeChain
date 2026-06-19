namespace CafeChain.Application.DTOs.Admin.Drinks
{
    public class AdminDrinkImageDTO
    {
        public int DrinkImageId { get; set; }
        public int DrinkId { get; set; }
        public string ImageUrl { get; set; }
        public bool IsDefault { get; set; }
    }
}
