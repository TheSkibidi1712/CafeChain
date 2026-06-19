namespace CafeChain.Application.DTOs.Admin.InventoryDocuments
{
    public class InventoryDocumentSnapshotItemDTO
    {
        public string ItemName { get; set; }

        public string UnitName { get; set; }

        public decimal Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal TotalAmount { get; set; }
    }
}
