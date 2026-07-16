using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Results;
using CafeChain.Models.Inventories.Stock;

namespace CafeChain.Application.Interfaces.Inventories
{
    public interface IPurchaseOrderService
    {
        Task<ServiceResult<PurchaseOrderDetailDto>> CreateDraftAsync(CreatePurchaseOrderRequest input, int actorStaffId, IReadOnlyCollection<string> roles);
        Task<ServiceResult<PurchaseOrderDetailDto>> ApproveAsync(int id, int actorStaffId, IReadOnlyCollection<string> roles);
        Task<ServiceResult<PurchaseOrderDetailDto>> MarkSentAsync(int id, int actorStaffId, IReadOnlyCollection<string> roles);
        Task<ServiceResult<PurchaseOrderDetailDto>> CancelAsync(int id, int actorStaffId, IReadOnlyCollection<string> roles, string reason);
        Task<ServiceResult<PurchaseOrderDetailDto>> GetDetailAsync(int id);
        Task<IReadOnlyList<PurchaseOrderListItemDto>> ListAsync(int? storeId, string? status);
        Task<ServiceResult> ValidateReceiptLineAsync(BranchReceipt receipt, BranchReceiptLine line);
        Task<ServiceResult> RegisterReceiptPostingAsync(BranchReceipt receipt, BranchReceiptLine line, int actorStaffId);
    }
}
