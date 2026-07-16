using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories
{
    /// <summary>Issue #128 — RestockRequest state transitions (intent-only; no inventory mutation).</summary>
    public interface IRestockRequestWorkflowService
    {
        Task<ServiceResult<RestockRequestWorkflowDetailDto>> GetWorkflowDetailAsync(
            int requestId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames);

        Task<ServiceResult<RestockRequestWorkflowDetailDto>> StartProcessingAsync(
            int requestId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames,
            string? reason,
            string? rowVersion);

        Task<ServiceResult<RestockRequestWorkflowDetailDto>> SubmitAsync(
            int requestId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames,
            string? rowVersion);

        Task<ServiceResult<RestockRequestWorkflowDetailDto>> CloseRemainingAsync(
            int requestId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames,
            string reason,
            string? rowVersion);

        Task<ServiceResult<RestockRequestWorkflowDetailDto>> RejectAsync(
            int requestId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames,
            string reason,
            string? rowVersion);

        Task<ServiceResult<RestockRequestWorkflowDetailDto>> CancelAsync(
            int requestId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames,
            string? reason,
            string? rowVersion);

        Task<ServiceResult<RestockFulfillmentDto>> LinkFulfillmentAsync(
            int requestId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames,
            LinkRestockFulfillmentRequest input,
            string? rowVersion);
    }
}
