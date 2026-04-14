namespace CafeChain.Application.DTOs.Admin.StoreInventories
{
    public class InventoryDTO
    {
        public int StoreInventoryId { get; set; }
        public string IngredientName { get; set; }
        public decimal AvailableQty { get; set; }
        public decimal ReservedQty { get; set; }
        public DateTime LastUpdated { get; set; }
        public string UnitCode { get; set; }
    }
}
