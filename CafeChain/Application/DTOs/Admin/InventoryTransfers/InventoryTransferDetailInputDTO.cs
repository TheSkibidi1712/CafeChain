namespace CafeChain.Application.DTOs.Admin.InventoryTransfers
{
    public class InventoryTransferDetailInputDTO
    {
        public int? IngredientId { get; set; }
        public int? PreparedItemId { get; set; }
        public int? RestockRequestId { get; set; }
        public int? RestockRequestFulfillmentId { get; set; }
        public int UnitId { get; set; }
        public decimal Quantity { get; set; }
        public decimal BaseQuantity { get; set; }
        public string? Note { get; set; }
    }
}
