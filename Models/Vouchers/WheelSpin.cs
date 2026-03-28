namespace CafeChain.Models.Vouchers
{
    public class WheelSpin
    {
        public int WheelSpinId { get; set; }

        public int CustomerId { get; set; }
        public int WheelConfigId { get; set; }

        public int? WheelPrizeId { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual WheelConfig WheelConfig { get; set; }
        public virtual WheelPrize WheelPrize { get; set; }
    }
}
