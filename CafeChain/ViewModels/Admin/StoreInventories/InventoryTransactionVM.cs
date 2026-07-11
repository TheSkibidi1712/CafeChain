namespace CafeChain.ViewModels.Admin.StoreInventories
{
    public class InventoryTransactionVM
    {
        public int InventoryTransactionId { get; set; }
        public int StoreInventoryId { get; set; }

        public int StoreId { get; set; }
        public string StoreName { get; set; } = string.Empty;

        public string IngredientName { get; set; } = string.Empty;
        public string IdentityBadge { get; set; } = string.Empty;
        public string QuantitySemanticsStatus { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string StockStatusName { get; set; } = string.Empty;

        public decimal Quantity { get; set; }
        public decimal BeforeQty { get; set; }
        public decimal AfterQty { get; set; }

        public decimal? UnitPrice { get; set; }
        public decimal? TotalAmount { get; set; }

        public int? InventoryDocumentId { get; set; }
        public int? InventoryTransferId { get; set; }
        public int? ReferenceOrderId { get; set; }
        public string ReferenceType { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
        public string UnitCode { get; set; } = string.Empty;
    }
}
