namespace CafeChain.Application.DTOs.Admin.InventoryTransfers
{
    public class InventoryTransferDetailInputDTO
    {
        public int IngredientId { get; set; }
        public int UnitId { get; set; }
        public decimal Quantity { get; set; }
        public decimal BaseQuantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public string? Note { get; set; }
    }
}
