using CafeChain.Models.Stores;
using CafeChain.Models.Staffs;

namespace CafeChain.Models.Inventories
{
    public class StockImport
    {
        public int Id { get; set; }
        public int StoreId { get; set; }
        public int StaffId { get; set; }
        public DateTime ImportDate { get; set; }
        public string Note { get; set; }

        public virtual Store Store { get; set; }
        public virtual Staff Staff { get; set; }

        public virtual ICollection<StockImportDetail> Details { get; set; }
    }
}
