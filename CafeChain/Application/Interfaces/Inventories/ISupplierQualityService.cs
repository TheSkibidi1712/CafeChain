using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories;

public interface ISupplierQualityService
{
    Task<ServiceResult<SupplierReceiptIssueContextDto>> GetReceiptContextAsync(
        int branchReceiptLineId,
        int actorStaffId,
        IReadOnlyCollection<string> roles);

    Task<ServiceResult<SupplierReceiptIssueListItemDto>> CreateIssueAsync(
        CreateSupplierReceiptIssueRequest input,
        int actorStaffId,
        IReadOnlyCollection<string> roles);

    Task<ServiceResult<SupplierReceiptIssueListItemDto>> TransitionAsync(
        int issueId,
        SupplierReceiptIssueTransitionRequest input,
        int actorStaffId,
        IReadOnlyCollection<string> roles);

    Task<ServiceResult<SupplierQualityDashboardDto>> GetDashboardAsync(
        int storeId,
        int? supplierId,
        DateTime fromUtc,
        DateTime toUtc,
        int actorStaffId,
        IReadOnlyCollection<string> roles);
}
