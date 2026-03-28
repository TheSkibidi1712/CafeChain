using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories
{
    public class Waste
    {
        public int WasteId { get; set; }

        public int StoreId { get; set; }
        public int StaffId { get; set; }

        public DateTime CreatedAt { get; set; }

        public string Note { get; set; } // ghi chú thêm nếu cần

        // Navigation
        public virtual Store Store { get; set; }
        public virtual Staff Staff { get; set; }

        public virtual ICollection<WasteDetail> Details { get; set; }
    }
}
