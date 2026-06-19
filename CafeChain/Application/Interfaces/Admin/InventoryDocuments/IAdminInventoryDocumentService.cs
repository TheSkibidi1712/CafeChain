using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Stores;
using CafeChain.ViewModels.Admin.InventoryDocuments;
using CafeChain.ViewModels.Shared;


namespace CafeChain.Application.Interfaces.Admin.InventoryDocuments
{
    public interface IAdminInventoryDocumentService
    {

        // =====================================================
        // INDEX
        // =====================================================

        Task<PaginatedListViewModel<AdminInventoryDocumentListVM>> GetPagedDocumentsAsync(AdminInventoryDocumentFilterDTO filter);

        Task<AdminInventoryDocumentIndexVM> GetIndexDataAsync(AdminInventoryDocumentFilterDTO filter);

        // =====================================================
        // DETAIL
        // =====================================================

        Task<AdminInventoryDocumentDetailVM?> GetDetailAsync(int documentId);

        // =====================================================
        // PREVIEW
        // =====================================================

        Task<AdminInventoryDocumentPreviewVM?> GetPreviewAsync(int documentId);

        // =====================================================
        // SNAPSHOT
        // =====================================================

        Task<InventoryDocumentSnapshotDTO?> GetSnapshotAsync(int documentId);


        // =====================================================
        // CONFIRM
        // =====================================================

        Task<bool> ConfirmAsync(ConfirmInventoryDocumentDTO dto);

        // =====================================================
        // EXPORT FILE
        // =====================================================
        Task<byte[]?> ExportFileAsync(ExportInventoryDocumentDTO dto);


    }
}
