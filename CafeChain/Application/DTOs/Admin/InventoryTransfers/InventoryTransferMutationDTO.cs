using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.DTOs.Admin.InventoryTransfers
{
    public class InventoryTransferMutationDTO
    {
        public int? TransferId { get; set; }
        public string? RowVersion { get; set; }
        public string? RequestKey { get; set; }
        public int FromStoreId { get; set; }
        public int ToStoreId { get; set; }
        public InventoryTransferPurpose Purpose { get; set; } = InventoryTransferPurpose.REPLENISHMENT;
        public DateTime DocumentDate { get; set; } = DateTime.Today;
        public string? Note { get; set; }
        public bool AllowRestockOverallocation { get; set; }
        public string? RestockOverallocationReason { get; set; }
        public List<InventoryTransferDetailInputDTO> Details { get; set; } = [];
    }
}
