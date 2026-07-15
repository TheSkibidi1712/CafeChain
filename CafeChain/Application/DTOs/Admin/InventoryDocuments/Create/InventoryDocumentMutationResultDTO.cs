namespace CafeChain.Application.DTOs.Admin.InventoryDocuments.Create
{
    public class InventoryDocumentMutationResultDTO
    {
        public int DocumentId { get; set; }
        public CafeChain.Models.Enums.Inventory.InventoryDocumentStatus Status { get; set; }
        public long? ApprovalId { get; set; }

        public List<InventoryStockWarningDTO> Warnings { get; set; } = [];
    }
}
