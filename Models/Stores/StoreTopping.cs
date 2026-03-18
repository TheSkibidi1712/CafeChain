using CafeChain.Models.Drinks;

namespace CafeChain.Models.Stores
{
    public class StoreTopping
    {
        public int StoTId { get; set; }
        public int StoId { get; set; }
        public int TopId { get; set; }
        public bool Active { get; set; }

        public virtual Store Store { get; set; }
        public virtual Topping Topping { get; set; }
    }
}
