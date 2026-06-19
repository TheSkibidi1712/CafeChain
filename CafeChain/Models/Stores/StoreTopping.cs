using CafeChain.Models.Drinks;

namespace CafeChain.Models.Stores
{
    public class StoreTopping
    {
        public int StoreToppingId { get; set; }
        public int StoreId { get; set; }
        public int ToppingId { get; set; }
        public bool Active { get; set; }

        public virtual Store Store { get; set; }
        public virtual Topping Topping { get; set; }
    }
}
