using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Inventories.Stock;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories
{
    public sealed class RestockFulfillmentPostingService : IRestockFulfillmentPostingService
    {
        private readonly AppDbContext _context;

        public RestockFulfillmentPostingService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ServiceResult<RestockFulfillmentPostingResult>> RegisterAsync(
            RegisterRestockFulfillmentPostingCommand command)
        {
            if (command.Quantity <= 0 || command.BaseUnitId <= 0)
                return Failure("Số lượng fulfillment và đơn vị cơ sở phải hợp lệ.");
            if (command.IngredientId.HasValue == command.PreparedItemId.HasValue)
                return Failure("Nguồn thực hiện phải có đúng một định danh nguyên liệu hoặc bán thành phẩm.");
            if (command.SourceDocumentType is not (
                RestockFulfillmentDocumentTypes.BranchReceipt or
                RestockFulfillmentDocumentTypes.InventoryTransfer or
                RestockFulfillmentDocumentTypes.ProductionRun))
                return Failure("Loại chứng từ fulfillment không hợp lệ.");

            var replay = await _context.RestockFulfillmentPostings
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.SourceDocumentType == command.SourceDocumentType &&
                    p.SourceDocumentId == command.SourceDocumentId &&
                    p.SourceDocumentLineId == command.SourceDocumentLineId &&
                    p.RestockRequestId == command.RestockRequestId);
            if (replay != null)
            {
                var replayTotal = await SumPostedQuantityAsync(command.RestockRequestId);
                var replayRequest = await _context.RestockRequests.AsNoTracking()
                    .FirstOrDefaultAsync(r => r.RestockRequestId == command.RestockRequestId);
                return ServiceResult<RestockFulfillmentPostingResult>.Success(new()
                {
                    WasReplay = true,
                    FulfilledQuantity = replayTotal,
                    TargetQuantity = replayRequest?.RequestedQuantity ?? replayTotal,
                    RequestStatus = replayRequest?.Status ?? string.Empty
                }, "Fulfillment posting đã tồn tại; không ghi trùng.");
            }

            var request = await LoadRequestForUpdateAsync(command.RestockRequestId);
            if (request == null)
                return Failure("Không tìm thấy yêu cầu nhập hàng.");
            if (request.StoreId != command.DestinationStoreId)
                return Failure("Cửa hàng nhận không khớp yêu cầu nhập hàng.");
            if (!IdentityMatches(request, command))
                return Failure("Identity chứng từ không khớp yêu cầu nhập hàng.");
            if (request.Status is not (
                RestockRequestStatuses.Submitted or
                RestockRequestStatuses.Processing or
                RestockRequestStatuses.PartiallyReceived))
                return Failure($"Không fulfillment yêu cầu ở trạng thái {request.Status}.");

            var target = request.RequestedQuantity;
            var posted = await SumPostedQuantityAsync(request.RestockRequestId);
            posted += _context.ChangeTracker.Entries<RestockFulfillmentPosting>()
                .Where(e => e.State == EntityState.Added && e.Entity.RestockRequestId == request.RestockRequestId)
                .Sum(e => e.Entity.Quantity);
            var after = posted + command.Quantity;
            if (after > target)
                return Failure($"Số lượng fulfillment {after:N3} vượt mục tiêu {target:N3}.");

            _context.RestockFulfillmentPostings.Add(new RestockFulfillmentPosting
            {
                RestockRequestId = request.RestockRequestId,
                SourceDocumentType = command.SourceDocumentType,
                SourceDocumentId = command.SourceDocumentId,
                SourceDocumentLineId = command.SourceDocumentLineId,
                IngredientId = command.IngredientId,
                PreparedItemId = command.PreparedItemId,
                Quantity = command.Quantity,
                BaseUnitId = command.BaseUnitId,
                CreatedAtUtc = DateTime.UtcNow
            });

            var previous = request.Status;
            if (previous == RestockRequestStatuses.Submitted)
            {
                AddTransition(
                    request,
                    previous,
                    RestockRequestStatuses.Processing,
                    command,
                    posted,
                    posted,
                    "Bắt đầu xử lý bằng chứng từ kho.");
                previous = RestockRequestStatuses.Processing;
            }

            var next = after < target
                ? RestockRequestStatuses.PartiallyReceived
                : RestockRequestStatuses.Completed;
            request.Status = next;
            request.UpdatedAt = DateTime.UtcNow;
            request.HandledByStaffId ??= command.ActorStaffId;
            request.HandledAt ??= DateTime.UtcNow;
            if (!string.Equals(previous, next, StringComparison.Ordinal))
                AddTransition(request, previous, next, command, posted, after, command.Reason);

            return ServiceResult<RestockFulfillmentPostingResult>.Success(new()
            {
                FulfilledQuantity = after,
                TargetQuantity = target,
                RequestStatus = next
            });
        }

        private async Task<RestockRequest?> LoadRequestForUpdateAsync(int requestId)
        {
            var tracked = _context.ChangeTracker.Entries<RestockRequest>()
                .Select(e => e.Entity)
                .FirstOrDefault(r => r.RestockRequestId == requestId);
            if (tracked != null)
                return tracked;

            if (_context.Database.IsSqlServer())
            {
                return await _context.RestockRequests
                    .FromSqlInterpolated(
                        $@"SELECT * FROM RestockRequests WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                           WHERE RestockRequestId = {requestId}")
                    .SingleOrDefaultAsync();
            }

            return await _context.RestockRequests
                .SingleOrDefaultAsync(r => r.RestockRequestId == requestId);
        }

        private async Task<decimal> SumPostedQuantityAsync(int requestId)
        {
            var quantities = await _context.RestockFulfillmentPostings
                .AsNoTracking()
                .Where(p => p.RestockRequestId == requestId)
                .Select(p => p.Quantity)
                .ToListAsync();
            return quantities.Sum();
        }

        private static bool IdentityMatches(
            RestockRequest request,
            RegisterRestockFulfillmentPostingCommand command) =>
            request.IngredientId.HasValue
                ? request.IngredientId == command.IngredientId && !command.PreparedItemId.HasValue
                : request.PreparedItemId == command.PreparedItemId && !command.IngredientId.HasValue;

        private void AddTransition(
            RestockRequest request,
            string previous,
            string next,
            RegisterRestockFulfillmentPostingCommand command,
            decimal before,
            decimal after,
            string? reason)
        {
            _context.RestockRequestTransitions.Add(new RestockRequestTransition
            {
                RestockRequestId = request.RestockRequestId,
                PreviousStatus = previous,
                NewStatus = next,
                ActorStaffId = command.ActorStaffId,
                OccurredAtUtc = DateTime.UtcNow,
                Reason = string.IsNullOrWhiteSpace(reason)
                    ? $"{command.SourceDocumentType} #{command.SourceDocumentId}"
                    : reason.Trim(),
                BranchReceiptId = command.SourceDocumentType == RestockFulfillmentDocumentTypes.BranchReceipt
                    ? command.SourceDocumentId
                    : null,
                InventoryTransferId = command.SourceDocumentType == RestockFulfillmentDocumentTypes.InventoryTransfer
                    ? command.SourceDocumentId
                    : null,
                QuantityBefore = before,
                QuantityAfter = after
            });
        }

        private static ServiceResult<RestockFulfillmentPostingResult> Failure(string message) =>
            ServiceResult<RestockFulfillmentPostingResult>.Failure(message);
    }
}
