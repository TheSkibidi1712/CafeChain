namespace CafeChain.Application.DTOs.Admin.InventoryDocuments.Create
{
    public class InventoryBaseQuantitySummaryDTO
    {
        public int UnitId { get; set; }

        public string UnitCode { get; set; } = string.Empty;

        public string UnitName { get; set; } = string.Empty;

        public decimal Quantity { get; set; }
    }
}
