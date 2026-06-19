using CafeChain.Models.Customers;
using System;

namespace CafeChain.Models.Vouchers
{
    public class CustomerVoucher
    {
        public int CustomerVoucherId { get; set; }
        public int CustomerId { get; set; }
        public int VoucherId { get; set; }
        public bool IsUsed { get; set; } = false;
        public DateTime CollectedDate { get; set; } = DateTime.Now;
        public DateTime? UsedDate { get; set; }

        public virtual Customer Customer { get; set; }
        public virtual Voucher Voucher { get; set; }
    }
}
