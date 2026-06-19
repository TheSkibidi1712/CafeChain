namespace CafeChain.Models.Inventories.Documents
{
    public class InventoryDocumentSnapshot
    {
        public int InventoryDocumentSnapshotId { get; set; }

        public int InventoryDocumentId { get; set; }

        public string Code { get; set; }
        public DateTime DocumentDate { get; set; }

        public string StoreName { get; set; }
        public string StaffName { get; set; }

        public string? PartnerName { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal FinalAmount { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual ICollection<InventoryDocumentSnapshotDetail> Details { get; set; }
    }
}
