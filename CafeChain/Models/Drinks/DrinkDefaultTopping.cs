namespace CafeChain.Models.Drinks
{
    public class DrinkDefaultTopping
    {
        public int DrinkDefaultToppingId { get; set; }

        public int DrinkId { get; set; }
        public int ToppingId { get; set; }

        public virtual Drink Drink { get; set; }
        public virtual Topping Topping { get; set; }
    }
}
