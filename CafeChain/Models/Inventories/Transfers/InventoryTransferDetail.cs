using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Stock;

namespace CafeChain.Models.Inventories.Transfers
{
    public class InventoryTransferDetail
    {
        public int InventoryTransferDetailId { get; set; }

        public int InventoryTransferId { get; set; }

        public int? IngredientId { get; set; }
        public int? PreparedItemId { get; set; }
        public int? RestockRequestId { get; set; }
        public int? RestockRequestFulfillmentId { get; set; }
        public int UnitId { get; set; }

        public decimal Quantity { get; set; }
        public decimal BaseQuantity { get; set; }

        public decimal? SourceBeforeQty { get; set; }
        public decimal? SourceAfterQty { get; set; }
        public decimal? DestinationBeforeQty { get; set; }
        public decimal? DestinationAfterQty { get; set; }

        public decimal? UnitPrice { get; set; }
        public string? Note { get; set; }

        public virtual InventoryTransfer InventoryTransfer { get; set; }
        public virtual Ingredient? Ingredient { get; set; }
        public virtual PreparedItem? PreparedItem { get; set; }
        public virtual RestockRequest? RestockRequest { get; set; }
        public virtual RestockRequestFulfillment? RestockRequestFulfillment { get; set; }
        public virtual Unit Unit { get; set; } = null!;
    }
}
