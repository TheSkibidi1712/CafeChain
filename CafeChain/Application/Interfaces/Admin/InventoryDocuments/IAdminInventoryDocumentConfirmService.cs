using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Models.Inventories.Documents;

namespace CafeChain.Application.Interfaces.Admin.InventoryDocuments
{
    public interface IAdminInventoryDocumentConfirmService
    {
        Task<InventoryDocumentMutationResultDTO?> ConfirmAsync(ConfirmInventoryDocumentDTO dto);

        Task<InventoryProcessResultDTO> ConfirmDocumentAsync(InventoryDocument document, int confirmedByStaffId);
    }
}
