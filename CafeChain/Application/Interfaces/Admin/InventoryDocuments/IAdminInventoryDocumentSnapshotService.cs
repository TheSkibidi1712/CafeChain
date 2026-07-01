using CafeChain.Application.DTOs.Admin.InventoryDocuments.Snapshot;
using CafeChain.Models.Inventories.Documents;

namespace CafeChain.Application.Interfaces.Admin.InventoryDocuments
{
    public interface IAdminInventoryDocumentSnapshotService
    {
        Task CreateSnapshotAsync(InventoryDocument document);

        Task<InventoryDocumentSnapshotDTO?> GetSnapshotAsync(int id);
    }
}
