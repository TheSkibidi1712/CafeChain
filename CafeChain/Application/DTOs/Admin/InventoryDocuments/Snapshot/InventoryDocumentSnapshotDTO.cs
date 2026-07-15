namespace CafeChain.Application.DTOs.Admin.InventoryDocuments.Snapshot
{
    public class InventoryDocumentSnapshotDTO
    {
        public int SnapshotId { get; set; }

        public int InventoryDocumentId { get; set; }

        public CafeChain.Models.Enums.Inventory.InventoryDocumentType Type { get; set; }

        public CafeChain.Models.Enums.Inventory.InventoryDocumentPurpose Purpose { get; set; }

        public CafeChain.Models.Enums.Inventory.InventoryDocumentStatus Status { get; set; }

        public bool CostComplete { get; set; }

        public string Code { get; set; }

        public DateTime DocumentDate { get; set; }

        public string StoreName { get; set; }

        public string StaffName { get; set; }

        public string? PartnerName { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal VatAmount { get; set; }

        public decimal FinalAmount { get; set; }

        public DateTime CreatedAt { get; set; }

        public List<InventoryDocumentSnapshotItemDTO> Details { get; set; } = [];
    }
}
