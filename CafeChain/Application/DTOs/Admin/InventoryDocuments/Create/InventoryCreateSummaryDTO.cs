namespace CafeChain.Application.DTOs.Admin.InventoryDocuments.Create
{
    public class InventoryCreateSummaryDTO
    {
        public decimal TotalAmount { get; set; }

        public decimal VatAmount { get; set; }

        public decimal FinalAmount { get; set; }

        public decimal VatRate { get; set; }

        public int TotalItems { get; set; }

        public decimal TotalQuantity { get; set; }

        public List<InventoryBaseQuantitySummaryDTO> BaseQuantities { get; set; } = [];

        public string BaseQuantityText { get; set; } = "0";
    }
}
