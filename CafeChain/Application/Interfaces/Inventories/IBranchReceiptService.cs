using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories
{
    /// <summary>Issue #128 — BranchReceipt draft + confirm/post (only confirm mutates inventory).</summary>
    public interface IBranchReceiptService
    {
        Task<ServiceResult<BranchReceiptDetailDto>> CreateDraftAsync(
            CreateBranchReceiptRequest request,
            int actorStaffId,
            IReadOnlyCollection<string> roleNames);

        Task<ServiceResult<PurchaseOrderReceiptDraftDto>> CreateOrOpenPurchaseOrderDraftAsync(
            int purchaseOrderId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames);

        Task<ServiceResult<PurchaseOrderReceiptDraftDto>> GetPurchaseOrderDraftAsync(
            int branchReceiptId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames);

        Task<ServiceResult<PurchaseOrderReceiptDraftDto>> SavePurchaseOrderDraftAsync(
            SavePurchaseOrderReceiptDraftRequest request,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames);

        Task<ServiceResult<BranchReceiptDetailDto>> GetDetailAsync(
            int branchReceiptId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames);

        Task<ServiceResult<List<BranchReceiptListItemDto>>> ListForStoreAsync(
            int storeId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames,
            string? statusFilter = null);

        Task<ServiceResult<ConfirmBranchReceiptResultDto>> ConfirmAsync(
            int branchReceiptId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames,
            string? rowVersion);

        Task<ServiceResult<List<BranchReceiptSupplierOptionDto>>> GetSupplierOptionsAsync(
            int storeId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames);

        Task<ServiceResult<List<BranchReceiptOfferOptionDto>>> GetOfferOptionsAsync(
            int storeId,
            int supplierId,
            int? restockRequestId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames);
    }
}
