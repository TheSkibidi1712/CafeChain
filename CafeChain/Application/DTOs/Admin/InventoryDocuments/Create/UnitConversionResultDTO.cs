namespace CafeChain.Application.DTOs.Admin.InventoryDocuments.Create
{
    public class UnitConversionResultDTO
    {
        public decimal Quantity { get; set; }

        public decimal BaseQuantity { get; set; }

        public int BaseUnitId { get; set; }

        public string BaseUnitName { get; set; } = string.Empty;
    }
}
