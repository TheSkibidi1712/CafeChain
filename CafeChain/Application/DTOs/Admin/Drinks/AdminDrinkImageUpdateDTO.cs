namespace CafeChain.Application.DTOs.Admin.Drinks
{
    public class AdminDrinkImageUpdateDTO
    {
        public int DrinkImageId { get; set; }
        public int DrinkId { get; set; }
        public IFormFile NewImageFile { get; set; }
    }
}
