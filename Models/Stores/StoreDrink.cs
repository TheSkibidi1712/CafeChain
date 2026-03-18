using CafeChain.Models.Drinks;

namespace CafeChain.Models.Stores
{
    public class StoreDrink
    {
        public int StoDId { get; set; }
        public int StoId { get; set; }
        public int DriId { get; set; }
        public bool Active { get; set; }

        public virtual Store Store { get; set; }
        public virtual Drink Drink { get; set; }
    }
}
