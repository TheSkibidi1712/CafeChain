namespace CafeChain.Models.Inventories
{
    public class WasteReason
    {
        public int WasteReasonId { get; set; }

        public string Code { get; set; }
        // EXPIRED, BROKEN, DAMAGED

        public string Name { get; set; }
        // Hết hạn, Đổ vỡ, Hư hỏng

        public bool Active { get; set; }

        public virtual ICollection<WasteDetail> WasteDetails { get; set; }
    }
}
