using CafeChain.Models.Stores;

namespace CafeChain.Models.Orders
{
    public class DiningTable
    {
        public int TableId { get; set; }
        public int StoreId { get; set; }
        public int TableNumber { get; set; }
        public string Status { get; set; }
        public bool Active { get; set; }

        public virtual Store Store { get; set; }
        public virtual ICollection<Order> Orders { get; set; }
    }
}
