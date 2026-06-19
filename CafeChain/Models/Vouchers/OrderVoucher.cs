using CafeChain.Models.Orders;

namespace CafeChain.Models.Vouchers
{
    public class OrderVoucher
    {
        public int OrderVoucherId { get; set; }

        public int OrderId { get; set; }
        public int VoucherId { get; set; }

        public decimal DiscountValue { get; set; } // 🔥 snapshot

        public virtual Order Order { get; set; }
        public virtual Voucher Voucher { get; set; }
    }
}
