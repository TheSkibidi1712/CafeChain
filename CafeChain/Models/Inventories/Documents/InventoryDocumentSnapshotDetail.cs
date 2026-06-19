namespace CafeChain.Models.Inventories.Documents
{
    public class InventoryDocumentSnapshotDetail
    {
        public int Id { get; set; }

        public int InventoryDocumentSnapshotId { get; set; }

        public string ItemName { get; set; }
        public string UnitName { get; set; }

        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }

        public virtual InventoryDocumentSnapshot InventoryDocumentSnapshot { get; set; }
    }
}
