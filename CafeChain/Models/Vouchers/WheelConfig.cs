namespace CafeChain.Models.Vouchers
{
    public class WheelConfig
    {
        public int WheelConfigId { get; set; }

        public string Name { get; set; } // Vòng quay tháng 3

        public int SpinCost { get; set; } // điểm loyalty / lượt (AC1)

        public int SlotCount { get; set; } // 6 hoặc 8 (AC2)

        public bool Active { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual ICollection<WheelPrize> Prizes { get; set; }
    }
}
