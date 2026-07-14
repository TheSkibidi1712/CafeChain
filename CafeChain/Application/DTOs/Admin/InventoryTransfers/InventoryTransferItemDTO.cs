using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;

namespace CafeChain.Application.DTOs.Admin.InventoryTransfers
{
    public class InventoryTransferItemDTO
    {
        public string ItemType { get; set; } = string.Empty;
        public int? IngredientId { get; set; }
        public int? PreparedItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int BaseUnitId { get; set; }
        public string BaseUnitName { get; set; } = string.Empty;
        public string BaseUnitCode { get; set; } = string.Empty;
        public decimal AvailableBaseQuantity { get; set; }
        public decimal SuggestedBaseUnitCost { get; set; }
        public decimal SuggestedUnitPrice { get; set; }
        public List<InventoryIngredientUnitOptionDTO> UnitOptions { get; set; } = [];
    }
}
