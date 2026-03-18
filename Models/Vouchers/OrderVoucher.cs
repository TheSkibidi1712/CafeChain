using CafeChain.Models.Orders;

namespace CafeChain.Models.Vouchers
{
    public class OrderVoucher
    {
        public int OrVId { get; set; }

        public int OrdId { get; set; }
        public int VouId { get; set; }

        public decimal DiscountValue { get; set; } // 🔥 snapshot

        public virtual Order Order { get; set; }
        public virtual Voucher Voucher { get; set; }
    }
}
