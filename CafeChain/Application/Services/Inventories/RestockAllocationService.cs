using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Stock;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories
{
    public sealed class RestockAllocationService : IRestockAllocationService
    {
        private readonly AppDbContext _context;
        private readonly IRestockPurchaseAllocationProvider _purchaseAllocations;

        public RestockAllocationService(
            AppDbContext context,
            IRestockPurchaseAllocationProvider purchaseAllocations)
        {
            _context = context;
            _purchaseAllocations = purchaseAllocations;
        }

        public async Task<RestockAllocationSummaryDto?> GetSummaryAsync(
            int restockRequestId,
            int? excludeInventoryTransferId = null,
            int? excludePurchaseOrderLineId = null,
            bool lockRequest = false)
        {
            var request = await LoadRequestAsync(restockRequestId, lockRequest);
            if (request == null)
                return null;

            var transferQuantities = await _context.InventoryTransferDetails
                .AsNoTracking()
                .Where(x => x.RestockRequestId == restockRequestId
                    && x.InventoryTransfer.Status != InventoryTransferStatus.CANCELLED
                    && (!excludeInventoryTransferId.HasValue
                        || x.InventoryTransferId != excludeInventoryTransferId.Value))
                .Select(x => x.BaseQuantity)
                .ToListAsync();
            var fulfilledQuantities = await _context.RestockFulfillmentPostings
                .AsNoTracking()
                .Where(x => x.RestockRequestId == restockRequestId)
                .Select(x => x.Quantity)
                .ToListAsync();
            var purchaseAllocated = await _purchaseAllocations.GetAllocatedBaseQuantityAsync(
                restockRequestId, excludePurchaseOrderLineId);
            var transferAllocated = transferQuantities.Sum();
            var fulfilled = fulfilledQuantities.Sum();

            return new RestockAllocationSummaryDto
            {
                RestockRequestId = restockRequestId,
                RequestedQuantity = request.RequestedQuantity,
                FulfilledQuantity = fulfilled,
                TransferAllocatedQuantity = transferAllocated,
                PurchaseAllocatedQuantity = purchaseAllocated,
                ClosedRemainingQuantity = request.ClosedRemainingQuantity,
                RemainingUnallocatedQuantity = Math.Max(
                    0m,
                    request.RequestedQuantity
                    - transferAllocated
                    - purchaseAllocated
                    - request.ClosedRemainingQuantity),
                RemainingToReceiveQuantity = Math.Max(
                    0m,
                    request.RequestedQuantity - fulfilled - request.ClosedRemainingQuantity)
            };
        }

        public async Task<ServiceResult<RestockAllocationSummaryDto>> ValidateAllocationAsync(
            RestockAllocationValidationRequest input)
        {
            if (input.AllocationQuantity <= 0)
                return ServiceResult<RestockAllocationSummaryDto>.Failure("Số lượng phân bổ phải lớn hơn 0.");

            var request = await LoadRequestAsync(input.RestockRequestId, lockRequest: true);
            if (request == null)
                return ServiceResult<RestockAllocationSummaryDto>.Failure("Không tìm thấy yêu cầu nhập hàng.");
            if (request.StoreId != input.DestinationStoreId)
                return ServiceResult<RestockAllocationSummaryDto>.Failure("Yêu cầu nhập không thuộc cửa hàng nhận.");
            if (request.IngredientId != input.IngredientId || request.PreparedItemId != input.PreparedItemId)
                return ServiceResult<RestockAllocationSummaryDto>.Failure("Mặt hàng phân bổ không khớp yêu cầu nhập.");
            if (request.Status is not (RestockRequestStatuses.Processing or RestockRequestStatuses.PartiallyReceived))
                return ServiceResult<RestockAllocationSummaryDto>.Failure(
                    $"Chỉ phân bổ yêu cầu đang PROCESSING hoặc PARTIALLY_RECEIVED. Hiện tại: {request.Status}.");

            var summary = await GetSummaryAsync(
                input.RestockRequestId,
                input.ExcludeInventoryTransferId,
                lockRequest: false);
            if (summary == null)
                return ServiceResult<RestockAllocationSummaryDto>.Failure("Không tải được số liệu phân bổ yêu cầu nhập.");

            if (input.AllocationQuantity > summary.RemainingUnallocatedQuantity)
            {
                var canOverride = input.AllowOverallocationOverride
                    && (input.ActorRoles.Contains(RoleConstants.BusinessOwner)
                        || input.ActorRoles.Contains(RoleConstants.SystemAdmin))
                    && !string.IsNullOrWhiteSpace(input.OverrideReason);
                if (!canOverride)
                {
                    return ServiceResult<RestockAllocationSummaryDto>.Failure(
                        $"Số lượng phân bổ {input.AllocationQuantity:N3} vượt phần chưa phân bổ {summary.RemainingUnallocatedQuantity:N3}.");
                }

                _context.RestockRequestTransitions.Add(new RestockRequestTransition
                {
                    RestockRequestId = request.RestockRequestId,
                    PreviousStatus = request.Status,
                    NewStatus = request.Status,
                    ActorStaffId = input.ActorStaffId,
                    OccurredAtUtc = DateTime.UtcNow,
                    Reason = $"OVER_ALLOCATION_OVERRIDE: {input.OverrideReason!.Trim()}",
                    QuantityBefore = summary.TransferAllocatedQuantity + summary.PurchaseAllocatedQuantity,
                    QuantityAfter = summary.TransferAllocatedQuantity + summary.PurchaseAllocatedQuantity + input.AllocationQuantity,
                    RequestKey = input.RequestKey
                });
            }

            return ServiceResult<RestockAllocationSummaryDto>.Success(summary);
        }

        private async Task<RestockRequest?> LoadRequestAsync(int requestId, bool lockRequest)
        {
            var tracked = _context.ChangeTracker.Entries<RestockRequest>()
                .Select(x => x.Entity)
                .FirstOrDefault(x => x.RestockRequestId == requestId);
            if (tracked != null)
                return tracked;

            if (lockRequest && _context.Database.IsSqlServer())
            {
                return await _context.RestockRequests
                    .FromSqlInterpolated(
                        $@"SELECT * FROM RestockRequests WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                           WHERE RestockRequestId = {requestId}")
                    .SingleOrDefaultAsync();
            }

            return await _context.RestockRequests
                .FirstOrDefaultAsync(x => x.RestockRequestId == requestId);
        }
    }
}
