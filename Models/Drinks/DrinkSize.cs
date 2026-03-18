namespace CafeChain.Models.Drinks
{
    public class DrinkSize
    {
        public int DriSId { get; set; }
        public int DriId { get; set; }
        public int SizId { get; set; }
        public decimal Price { get; set; }
        public bool Active { get; set; }

        public virtual Drink Drink { get; set; }
        public virtual Size Size { get; set; }
    }
}
