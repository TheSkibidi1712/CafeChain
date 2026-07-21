using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Application.DTOs.Admin.InventoryTransfers;
using CafeChain.ViewModels.Admin.InventoryTransfers;

namespace CafeChain.Application.Interfaces.Admin.InventoryTransfers
{
    public interface IAdminInventoryTransferService
    {
        Task<AdminInventoryTransferIndexVM> GetIndexAsync(
            AdminInventoryTransferIndexVM filter,
            IReadOnlyCollection<int>? allowedStoreIds = null);

        Task<AdminInventoryTransferCreateVM> GetCreateDataAsync(
            IReadOnlyCollection<int>? allowedStoreIds = null);

        Task<AdminInventoryTransferDetailVM?> GetDetailAsync(int id);

        Task<List<InventoryTransferItemDTO>> GetTransferItemsAsync(int fromStoreId);

        Task<InventoryTransferMutationResultDTO> CreateDraftAsync(InventoryTransferMutationDTO dto);

        Task<InventoryTransferMutationResultDTO> UpdateDraftAsync(int id, InventoryTransferMutationDTO dto);

        Task<InventoryTransferMutationResultDTO> ConfirmAsync(int id, string? requestKey);

        Task<InventoryTransferMutationResultDTO> DispatchAsync(int id, string? requestKey);

        Task<InventoryTransferMutationResultDTO> ReceiveAsync(int id, InventoryTransferReceiveDTO dto);

        Task<InventoryTransferMutationResultDTO> RequestReturnAsync(int id, InventoryTransferResolutionDTO dto);

        Task<InventoryTransferMutationResultDTO> ConfirmReturnAsync(int id, InventoryTransferResolutionDTO dto);

        Task<InventoryTransferMutationResultDTO> ResolveShortageAsync(int id, InventoryTransferResolutionDTO dto);

        Task<InventoryTransferMutationResultDTO> CreateFollowUpAsync(int id, InventoryTransferFollowUpDTO dto);

        Task<List<InventoryTransferDiscrepancyDryRunRowDTO>> GetDiscrepancyDryRunAsync();

        Task<bool> CancelAsync(int id, string? requestKey);

        Task<List<InventoryStockWarningDTO>> ValidateStockAsync(InventoryTransferMutationDTO dto);
    }
}
