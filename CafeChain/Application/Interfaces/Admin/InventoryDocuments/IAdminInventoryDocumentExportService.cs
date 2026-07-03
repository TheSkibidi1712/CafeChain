using CafeChain.Application.DTOs.Admin.InventoryDocuments.Export;
using CafeChain.Application.DTOs.Admin.InventoryDocuments.Snapshot;

namespace CafeChain.Application.Interfaces.Admin.InventoryDocuments
{
    public interface IAdminInventoryDocumentExportService
    {
        Task<byte[]> ExportPdfAsync(InventoryDocumentSnapshotDTO snapshot);

        Task<byte[]> ExportWordAsync(InventoryDocumentSnapshotDTO snapshot);

        Task<byte[]> ExportExcelAsync(IReadOnlyList<AdminInventoryDocumentExcelRowDTO> rows);
    }
}
