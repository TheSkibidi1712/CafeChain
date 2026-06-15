using CafeChain.Models.Stores;
using CafeChain.Models.Staffs;
namespace CafeChain.Models.Payments
{
    public class CashSession
    {
        public int CashSessionId { get; set; }
        public int StaffId { get; set; }
        public int StoreId { get; set; }
        public decimal? StartCash { get; set; }
        public decimal? EndCash { get; set; }
        public DateTime OpenTime { get; set; }
        public DateTime? CloseTime { get; set; }
        public bool IsClosed { get; set; }

        public virtual Staff Staff { get; set; }
        public virtual Store Store { get; set; }
        public virtual ICollection<Payment> Payments { get; set; }

    }
}
