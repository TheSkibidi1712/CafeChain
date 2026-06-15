namespace CafeChain.Models.Drinks
{
    public class DrinkSize
    {
        public int DrinkSizeId { get; set; }
        public int DrinkId { get; set; }
        public int SizeId { get; set; }
        public decimal Price { get; set; }
        public bool Active { get; set; }

        public virtual Drink Drink { get; set; }
        public virtual Size Size { get; set; }
    }
}
