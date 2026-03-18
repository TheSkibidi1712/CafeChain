namespace CafeChain.Models.Drinks
{
    public class DrinkImage
    {
        public int DrinkImageId { get; set; }
        public int DrinkId { get; set; }
        public string ImageUrl { get; set; }

        public virtual Drink Drink { get; set; }
    }
}
