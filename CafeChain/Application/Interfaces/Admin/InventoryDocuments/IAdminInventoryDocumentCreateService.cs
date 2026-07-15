using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Models.Enums.Inventory;
using CafeChain.ViewModels.Admin.InventoryDocuments.Create;

namespace CafeChain.Application.Interfaces.Admin.InventoryDocuments
{
    public interface IAdminInventoryDocumentCreateService
    {
        // =====================================================
        // CREATE PAGE
        // =====================================================

        Task<AdminInventoryDocumentCreateVM> GetCreateDataAsync(InventoryDocumentType type);

        Task<List<SupplierIngredientDTO>> GetSupplierIngredientsAsync(int supplierId);

        Task<List<SupplierIngredientDTO>> GetActiveIngredientsAsync(int storeId, InventoryDocumentPurpose purpose);

        Task<List<SupplierIngredientDTO>> GetStoreInventoryIngredientsAsync(
            int storeId,
            InventoryDocumentType type,
            InventoryDocumentPurpose purpose);

        Task<InventoryCreateSummaryDTO> CalculateSummaryAsync(CreateInventoryDocumentDTO dto);
        Task<InventoryDocumentPreflightResultDTO> PreflightAsync(CreateInventoryDocumentDTO dto);

        // =====================================================
        // CREATE
        // =====================================================

        Task<int> SaveDraftAsync(CreateInventoryDocumentDTO dto);

        Task<InventoryDocumentMutationResultDTO> CreateAndConfirmAsync(CreateInventoryDocumentDTO dto);

        Task<InventoryDocumentMutationResultDTO?> ConfirmDraftAsync(int documentId, string? requestKey, string? rowVersion = null);

        Task<bool> CancelInventoryDocumentAsync(int documentId, string? requestKey);
        Task<InventoryDocumentMutationResultDTO> ApproveNegativeAsync(int documentId, string? reviewNote);
        Task<InventoryDocumentMutationResultDTO> RejectNegativeAsync(int documentId, string reviewNote);
    }
}
