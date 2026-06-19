using CafeChain.Models.Drinks;

namespace CafeChain.Models.Stores
{
    public class StoreDrink
    {
        public int StoreDrinkId { get; set; }
        public int StoreId { get; set; }
        public int DrinkId { get; set; }
        public bool Active { get; set; }

        public virtual Store Store { get; set; }
        public virtual Drink Drink { get; set; }
    }
}
