using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Application.DTOs.Admin.InventoryTransfers;
using CafeChain.ViewModels.Admin.InventoryTransfers;

namespace CafeChain.Application.Interfaces.Admin.InventoryTransfers
{
    public interface IAdminInventoryTransferService
    {
        Task<AdminInventoryTransferCreateVM> GetCreateDataAsync();

        Task<List<SupplierIngredientDTO>> GetTransferIngredientsAsync(int fromStoreId);

        Task<InventoryTransferMutationResultDTO> CreateDraftAsync(InventoryTransferMutationDTO dto);

        Task<InventoryTransferMutationResultDTO> UpdateDraftAsync(int id, InventoryTransferMutationDTO dto);

        Task<InventoryTransferMutationResultDTO> ConfirmAsync(int id, string? requestKey);

        Task<bool> CancelAsync(int id, string? requestKey);

        Task<List<InventoryStockWarningDTO>> ValidateStockAsync(InventoryTransferMutationDTO dto);
    }
}
