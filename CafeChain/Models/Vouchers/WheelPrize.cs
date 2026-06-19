namespace CafeChain.Models.Vouchers
{
    public class WheelPrize
    {
        public int WheelPrizeId { get; set; }

        public int WheelConfigId { get; set; }

        public int SlotIndex { get; set; } // vị trí ô (0 → 5 hoặc 7)

        public int? VoucherId { get; set; } // null = xịt

        public decimal Probability { get; set; }
        // % trúng (AC3)

        public bool IsLose { get; set; }
        // true = "Chúc bạn may mắn lần sau"

        // Navigation
        public virtual WheelConfig WheelConfig { get; set; }
        public virtual Voucher Voucher { get; set; }
    }
}
