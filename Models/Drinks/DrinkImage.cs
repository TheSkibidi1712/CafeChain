namespace CafeChain.Models.Drinks
{
    public class DrinkImage
    {
        public int DriIId { get; set; }
        public int DriId { get; set; }
        public string ImageUrl { get; set; }

        public virtual Drink Drink { get; set; }
    }
}
