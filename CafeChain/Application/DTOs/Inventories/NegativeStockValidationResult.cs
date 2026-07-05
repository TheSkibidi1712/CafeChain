using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.DTOs.Inventories
{
    public class NegativeStockValidationResult
    {
        public bool IsAllowed { get; set; }
        public bool IsNegative { get; set; }
        public bool RequiresApproval { get; set; }
        public decimal ThresholdQuantity { get; set; }
        public decimal BeforeQty { get; set; }
        public decimal IssueQuantity { get; set; }
        public decimal AfterQty { get; set; }
        public InventoryStockStatus StockStatus { get; set; } = InventoryStockStatus.NORMAL;
        public string Message { get; set; } = string.Empty;
    }
}
