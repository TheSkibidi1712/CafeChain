using CafeChain.Models.Customers;
namespace CafeChain.Models.Vouchers
{
    public class VoucherUsage
    {
        public int VoucherUsageId { get; set; }

        public int VoucherId { get; set; }
        public int CustomerId { get; set; }

        public DateTime UsedAt { get; set; } // 🔥 thêm

        public virtual Voucher Voucher { get; set; }
        public virtual Customer Customer { get; set; }
    }
}
