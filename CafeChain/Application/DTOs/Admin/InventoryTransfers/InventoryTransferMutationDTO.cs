using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.DTOs.Admin.InventoryTransfers
{
    public class InventoryTransferMutationDTO
    {
        public string? RequestKey { get; set; }
        public int FromStoreId { get; set; }
        public int ToStoreId { get; set; }
        public InventoryTransferPurpose Purpose { get; set; } = InventoryTransferPurpose.REPLENISHMENT;
        public DateTime DocumentDate { get; set; } = DateTime.Today;
        public string? Note { get; set; }
        public List<InventoryTransferDetailInputDTO> Details { get; set; } = [];
    }
}
