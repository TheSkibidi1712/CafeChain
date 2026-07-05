using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.DTOs.Admin.InventoryTransfers
{
    public class InventoryTransferMutationResultDTO
    {
        public int InventoryTransferId { get; set; }
        public string Code { get; set; } = string.Empty;
        public InventoryTransferStatus Status { get; set; }
        public List<InventoryStockWarningDTO> Warnings { get; set; } = [];
    }
}
