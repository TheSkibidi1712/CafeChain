using CafeChain.Application.DTOs.Admin.InventoryDocuments;

namespace CafeChain.Application.Interfaces.Admin.InventoryDocuments
{
    public interface IAdminInventoryDocumentExportService
    {
        Task<byte[]> ExportPdfAsync(InventoryDocumentSnapshotDTO snapshot);

        Task<byte[]> ExportWordAsync(InventoryDocumentSnapshotDTO snapshot);
    }
}
