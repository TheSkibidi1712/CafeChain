using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.DTOs.Admin.InventoryDocuments.Export
{
    public class AdminInventoryDocumentExcelDetailRowDTO
    {
        public int No { get; set; }

        public string DocumentCode { get; set; } = string.Empty;

        public InventoryDocumentType Type { get; set; }

        public InventoryDocumentPurpose Purpose { get; set; }

        public string StoreName { get; set; } = string.Empty;

        public DateTime DocumentDate { get; set; }

        public InventoryDocumentStatus Status { get; set; }

        public string IngredientName { get; set; } = string.Empty;

        public string UnitName { get; set; } = string.Empty;

        public decimal Quantity { get; set; }

        public decimal BaseQuantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal CostPrice { get; set; }

        public decimal CostAmount { get; set; }

        public string? Note { get; set; }
    }
}
