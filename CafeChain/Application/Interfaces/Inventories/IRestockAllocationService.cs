using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories
{
    public interface IRestockAllocationService
    {
        Task<RestockAllocationSummaryDto?> GetSummaryAsync(
            int restockRequestId,
            int? excludeInventoryTransferId = null,
            int? excludePurchaseOrderLineId = null,
            bool lockRequest = false);

        Task<ServiceResult<RestockAllocationSummaryDto>> ValidateAllocationAsync(
            RestockAllocationValidationRequest request);
    }
}
