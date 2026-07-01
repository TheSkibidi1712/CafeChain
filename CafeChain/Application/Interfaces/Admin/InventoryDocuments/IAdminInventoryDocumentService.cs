using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Application.DTOs.Admin.InventoryDocuments.Index;
using CafeChain.ViewModels.Admin.InventoryDocuments.Detail;
using CafeChain.ViewModels.Admin.InventoryDocuments.Index;
using CafeChain.ViewModels.Admin.InventoryDocuments.Preview;
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
        // EXPORT FILE
        // =====================================================
        Task<byte[]?> ExportFileAsync(ExportInventoryDocumentDTO dto);


    }
}
