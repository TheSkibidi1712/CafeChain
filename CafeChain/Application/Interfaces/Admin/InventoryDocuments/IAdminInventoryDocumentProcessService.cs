using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Models.Inventories.Documents;

namespace CafeChain.Application.Interfaces.Admin.InventoryDocuments
{
    public interface IAdminInventoryDocumentProcessService
    {
        Task<InventoryProcessResultDTO> ExecuteProcessAsync(InventoryDocument document);
    }
}
