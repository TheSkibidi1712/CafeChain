using CafeChain.Models.Customers;
namespace CafeChain.Models.Vouchers
{
    public class VoucherUsage
    {
        public int VouUId { get; set; }

        public int VouId { get; set; }
        public int CusId { get; set; }

        public DateTime UsedAt { get; set; } // 🔥 thêm

        public virtual Voucher Voucher { get; set; }
        public virtual Customer Customer { get; set; }
    }
}
