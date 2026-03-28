using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories
{
    public class StockTake
    {
        public int StockTakeId { get; set; }

        public int StoreId { get; set; }
        public int StaffId { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsBalanced { get; set; } // đã cân bằng kho chưa

        // Navigation
        public virtual Store Store { get; set; }
        public virtual Staff Staff { get; set; }

        public virtual ICollection<StockTakeDetail> Details { get; set; }
    }
}
