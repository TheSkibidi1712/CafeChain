namespace CafeChain.ViewModels.Admin.Drinks
{
    public class AdminDrinkImageEditViewModel
    {
        public int DrinkImageId { get; set; }
        public int DrinkId { get; set; }
        public string CurrentImageUrl { get; set; }
        public IFormFile NewImageFile { get; set; }
    }
}
