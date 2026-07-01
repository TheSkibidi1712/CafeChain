namespace CafeChain.Application.DTOs.Admin.InventoryDocuments.Create
{
    public class InventoryDocumentMutationResultDTO
    {
        public int DocumentId { get; set; }

        public List<InventoryStockWarningDTO> Warnings { get; set; } = [];
    }
}
