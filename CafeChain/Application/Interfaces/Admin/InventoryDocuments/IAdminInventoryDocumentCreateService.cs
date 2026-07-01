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

        Task<InventoryCreateSummaryDTO> CalculateSummaryAsync(CreateInventoryDocumentDTO dto);

        // =====================================================
        // CREATE
        // =====================================================

        Task<int> SaveDraftAsync(CreateInventoryDocumentDTO dto);

        Task<InventoryDocumentMutationResultDTO> CreateAndConfirmAsync(CreateInventoryDocumentDTO dto);

        Task<InventoryDocumentMutationResultDTO?> ConfirmDraftAsync(int documentId);

        Task<bool> CancelInventoryDocumentAsync(int documentId);
    }
}
