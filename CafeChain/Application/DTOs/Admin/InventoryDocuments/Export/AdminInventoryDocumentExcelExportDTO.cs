namespace CafeChain.Application.DTOs.Admin.InventoryDocuments.Export
{
    public class AdminInventoryDocumentExcelExportDTO
    {
        public IReadOnlyList<AdminInventoryDocumentExcelRowDTO> Documents { get; set; } =
            [];

        public IReadOnlyList<AdminInventoryDocumentExcelDetailRowDTO> Details { get; set; } =
            [];
    }
}
