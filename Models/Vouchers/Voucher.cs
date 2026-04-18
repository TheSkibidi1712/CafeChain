namespace CafeChain.Models.Vouchers
{
    public class Voucher
    {
        public int VoucherId { get; set; }

        public string Code { get; set; }

        public int? DiscountPercent { get; set; }
        public decimal? DiscountAmount { get; set; }

        public decimal? MaxDiscount { get; set; }
        public decimal? MinOrderValue { get; set; }

        public int? MaxUsage { get; set; }
        public int? MaxUsagePerUser { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public bool Active { get; set; }
        
        public string? DaysOfWeek { get; set; }
        public TimeSpan? StartHour { get; set; }
        public TimeSpan? EndHour { get; set; }

        public virtual ICollection<OrderVoucher> OrderVouchers { get; set; }
        public virtual ICollection<VoucherUsage> VoucherUsages { get; set; }
    }
}
