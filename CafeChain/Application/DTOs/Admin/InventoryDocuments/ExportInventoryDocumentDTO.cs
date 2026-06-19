using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.DTOs.Admin.InventoryDocuments
{
    public class ExportInventoryDocumentDTO
    {
        public int DocumentId { get; set; }

        public InventoryDocumentExportType ExportType { get; set; }
    }
}
