namespace CafeChain.Models.Drinks
{
    public class DrinkTopping
    {
        public int DriTId { get; set; }
        public int DriId { get; set; }
        public int TopId { get; set; }

        public virtual Drink Drink { get; set; }
        public virtual Topping Topping { get; set; }
    }
}
