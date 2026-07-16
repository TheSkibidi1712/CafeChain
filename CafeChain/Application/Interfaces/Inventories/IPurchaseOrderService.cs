using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Results;
using CafeChain.Models.Inventories.Stock;

namespace CafeChain.Application.Interfaces.Inventories
{
    public interface IPurchaseOrderService
    {
        Task<ServiceResult<PurchaseOrderDetailDto>> CreateDraftAsync(CreatePurchaseOrderRequest input, int actorStaffId, IReadOnlyCollection<string> roles);
        Task<ServiceResult<PurchaseOrderDetailDto>> ApproveAsync(int id, string rowVersion, int actorStaffId, IReadOnlyCollection<string> roles);
        Task<ServiceResult<PurchaseOrderDetailDto>> MarkSentAsync(int id, string rowVersion, int actorStaffId, IReadOnlyCollection<string> roles);
        Task<ServiceResult<PurchaseOrderDetailDto>> CancelAsync(int id, string rowVersion, int actorStaffId, IReadOnlyCollection<string> roles, string reason);
        Task<ServiceResult<PurchaseOrderDetailDto>> CloseLineRemainingAsync(ClosePurchaseOrderLineRemainingRequest input, int actorStaffId, IReadOnlyCollection<string> roles);
        Task<ServiceResult<PurchaseOrderDetailDto>> GetDetailAsync(int id, int actorStaffId, IReadOnlyCollection<string> roles);
        Task<IReadOnlyList<PurchaseOrderListItemDto>> ListAsync(int? storeId, string? status, int actorStaffId, IReadOnlyCollection<string> roles);
        Task<ServiceResult> ValidateReceiptLineAsync(BranchReceipt receipt, BranchReceiptLine line);
        Task<ServiceResult> RegisterReceiptPostingAsync(BranchReceipt receipt, BranchReceiptLine line, int actorStaffId);
    }
}
