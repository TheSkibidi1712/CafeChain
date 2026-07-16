using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Operations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Issue #128 — RestockRequest workflow transitions without inventory mutation.
    /// </summary>
    public sealed class RestockRequestWorkflowService : IRestockRequestWorkflowService
    {
        private readonly AppDbContext _context;
        private readonly IScopeAuthorizationService _scopeAuthorization;
        private readonly ILogger<RestockRequestWorkflowService> _logger;
        private readonly IRestockAllocationService _allocationService;

        public RestockRequestWorkflowService(
            AppDbContext context,
            IScopeAuthorizationService scopeAuthorization,
            ILogger<RestockRequestWorkflowService> logger,
            IRestockAllocationService allocationService)
        {
            _context = context;
            _scopeAuthorization = scopeAuthorization;
            _logger = logger;
            _allocationService = allocationService;
        }

        public async Task<ServiceResult<RestockRequestWorkflowDetailDto>> SubmitAsync(
            int requestId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames)
        {
            if (!CanSubmit(roleNames))
                return Fail("Bạn không có quyền gửi yêu cầu nhập hàng.", BranchReceiptErrorCodes.Unauthorized);

            var request = await LoadRequestTrackedAsync(requestId);
            if (request == null)
                return Fail("Không tìm thấy yêu cầu nhập hàng.", BranchReceiptErrorCodes.RequestNotFound);
            var auth = await AuthorizeViewAsync(request, actorStaffId, actorStoreId, roleNames);
            if (!auth.IsSuccess)
                return Fail(auth.Message, auth.ErrorCode);
            if (request.Status != RestockRequestStatuses.Draft)
                return Fail($"Chỉ gửi yêu cầu ở trạng thái DRAFT. Hiện tại: {request.Status}.", BranchReceiptErrorCodes.TransitionInvalid);

            var previous = request.Status;
            request.Status = RestockRequestStatuses.Submitted;
            request.UpdatedAt = DateTime.UtcNow;
            AddTransition(
                request, previous, request.Status, actorStaffId,
                "Gửi yêu cầu nhập để bộ phận kho tiếp nhận.", null, null, null, null, null);

            var recipients = await _context.Staffs
                .AsNoTracking()
                .Where(x => x.Active && x.Account.Active
                    && x.Account.AccountRoles.Any(ar => ar.Role.Active
                        && ar.Role.Name == RoleConstants.AccountantWarehouse))
                .Select(x => x.StaffId)
                .Distinct()
                .ToListAsync();
            foreach (var recipient in recipients)
            {
                _context.StaffNotifications.Add(new StaffNotification
                {
                    StoreId = request.StoreId,
                    RecipientStaffId = recipient,
                    Type = StaffNotificationTypes.RestockRequestSubmitted,
                    Title = "Yêu cầu nhập hàng mới",
                    Body = $"{ResolveItemName(request)} · {request.RequestedQuantity:N3} · {request.Store?.Name ?? $"Cửa hàng #{request.StoreId}"}",
                    EntityType = StaffNotificationEntityTypes.RestockRequest,
                    EntityId = request.RestockRequestId,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();

            return ServiceResult<RestockRequestWorkflowDetailDto>.Success(
                await MapWorkflowDetailAsync(request),
                "Đã gửi yêu cầu nhập hàng để tiếp nhận xử lý.");
        }

        public async Task<ServiceResult<RestockRequestWorkflowDetailDto>> GetWorkflowDetailAsync(
            int requestId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames)
        {
            var request = await LoadRequestAsync(requestId);
            if (request == null)
                return Fail("Không tìm thấy yêu cầu nhập hàng.", BranchReceiptErrorCodes.RequestNotFound);

            var auth = await AuthorizeViewAsync(request, actorStaffId, actorStoreId, roleNames);
            if (!auth.IsSuccess)
                return Fail(auth.Message, auth.ErrorCode);

            return ServiceResult<RestockRequestWorkflowDetailDto>.Success(await MapWorkflowDetailAsync(request));
        }

        public async Task<ServiceResult<RestockRequestWorkflowDetailDto>> StartProcessingAsync(
            int requestId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames,
            string? reason = null)
        {
            if (!CanWarehouseProcess(roleNames))
                return Fail("Chỉ Kế toán/kho hoặc quản trị được chuyển PROCESSING.", BranchReceiptErrorCodes.Unauthorized);

            var request = await LoadRequestTrackedAsync(requestId);
            if (request == null)
                return Fail("Không tìm thấy yêu cầu nhập hàng.", BranchReceiptErrorCodes.RequestNotFound);

            var auth = await AuthorizeViewAsync(request, actorStaffId, actorStoreId, roleNames);
            if (!auth.IsSuccess)
                return Fail(auth.Message, auth.ErrorCode);

            if (request.Status != RestockRequestStatuses.Submitted)
            {
                return Fail(
                    $"Chỉ chuyển PROCESSING từ SUBMITTED. Hiện tại: {request.Status}.",
                    BranchReceiptErrorCodes.TransitionInvalid);
            }

            var previous = request.Status;
            request.Status = RestockRequestStatuses.Processing;
            request.HandledByStaffId = actorStaffId;
            request.HandledAt = DateTime.UtcNow;
            request.AcceptedByStaffId = actorStaffId;
            request.AcceptedAtUtc = DateTime.UtcNow;
            request.ProcessingNote = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            request.UpdatedAt = DateTime.UtcNow;

            AddTransition(request, previous, RestockRequestStatuses.Processing, actorStaffId, reason, null, null, null, null, null);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[RestockWorkflow] StartProcessing RequestId={Id} ByStaff={Staff}",
                requestId, actorStaffId);

            return ServiceResult<RestockRequestWorkflowDetailDto>.Success(
                await MapWorkflowDetailAsync(request),
                "Đã chuyển yêu cầu sang PROCESSING.");
        }

        public async Task<ServiceResult<RestockRequestWorkflowDetailDto>> RejectAsync(
            int requestId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames,
            string reason)
        {
            if (!CanWarehouseProcess(roleNames))
                return Fail("Chỉ Kế toán/kho hoặc quản trị được từ chối yêu cầu.", BranchReceiptErrorCodes.Unauthorized);

            if (string.IsNullOrWhiteSpace(reason))
                return Fail("Lý do từ chối là bắt buộc.", BranchReceiptErrorCodes.TransitionInvalid);

            var request = await LoadRequestTrackedAsync(requestId);
            if (request == null)
                return Fail("Không tìm thấy yêu cầu nhập hàng.", BranchReceiptErrorCodes.RequestNotFound);

            var auth = await AuthorizeViewAsync(request, actorStaffId, actorStoreId, roleNames);
            if (!auth.IsSuccess)
                return Fail(auth.Message, auth.ErrorCode);

            if (request.Status != RestockRequestStatuses.Submitted)
            {
                return Fail(
                    $"Không thể từ chối ở trạng thái {request.Status}.",
                    BranchReceiptErrorCodes.TransitionInvalid);
            }

            var previous = request.Status;
            request.Status = RestockRequestStatuses.Rejected;
            request.HandledByStaffId = actorStaffId;
            request.HandledAt = DateTime.UtcNow;
            request.UpdatedAt = DateTime.UtcNow;
            request.Note = AppendNote(request.Note, "REJECT: " + reason.Trim());

            AddTransition(
                request, previous, RestockRequestStatuses.Rejected, actorStaffId,
                reason.Trim(), null, null, null, null, null);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "[RestockWorkflow] Reject RequestId={Id} ByStaff={Staff}",
                requestId, actorStaffId);

            return ServiceResult<RestockRequestWorkflowDetailDto>.Success(
                await MapWorkflowDetailAsync(request),
                "Đã từ chối yêu cầu nhập hàng.");
        }

        public async Task<ServiceResult<RestockRequestWorkflowDetailDto>> CancelAsync(
            int requestId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames,
            string? reason = null)
        {
            if (!CanCancel(roleNames))
                return Fail("Bạn không có quyền hủy yêu cầu nhập hàng.", BranchReceiptErrorCodes.Unauthorized);

            // Serialize with BranchReceipt confirm (UPDLOCK on RestockRequest).
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var request = await LoadRestockRequestForUpdateAsync(requestId);
                if (request == null)
                {
                    await transaction.RollbackAsync();
                    return Fail("Không tìm thấy yêu cầu nhập hàng.", BranchReceiptErrorCodes.RequestNotFound);
                }

                // Load navigations for detail mapping after commit.
                await _context.Entry(request).Reference(r => r.Ingredient).LoadAsync();
                await _context.Entry(request).Reference(r => r.Recipe).LoadAsync();
                await _context.Entry(request).Reference(r => r.PreparedItem).LoadAsync();
                await _context.Entry(request).Reference(r => r.CreatedByStaff).LoadAsync();
                await _context.Entry(request).Reference(r => r.Store).LoadAsync();
                await _context.Entry(request).Reference(r => r.StockAlert).LoadAsync();

                var auth = await AuthorizeViewAsync(request, actorStaffId, actorStoreId, roleNames);
                if (!auth.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return Fail(auth.Message, auth.ErrorCode);
                }

                // StoreManager can cancel own store SUBMITTED; warehouse/admin wider.
                if (request.Status is not (RestockRequestStatuses.Draft or RestockRequestStatuses.Submitted))
                {
                    await transaction.RollbackAsync();
                    return Fail(
                        $"Không thể hủy ở trạng thái {request.Status}.",
                        BranchReceiptErrorCodes.TransitionInvalid);
                }

                if (IsStoreManagerOnly(roleNames)
                    && request.Status is not (RestockRequestStatuses.Draft or RestockRequestStatuses.Submitted))
                {
                    await transaction.RollbackAsync();
                    return Fail(
                        "Quản lý chi nhánh chỉ hủy yêu cầu ở trạng thái SUBMITTED.",
                        BranchReceiptErrorCodes.TransitionInvalid);
                }

                var previous = request.Status;
                request.Status = RestockRequestStatuses.Cancelled;
                request.UpdatedAt = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(reason))
                    request.Note = AppendNote(request.Note, "CANCEL: " + reason.Trim());

                AddTransition(
                    request, previous, RestockRequestStatuses.Cancelled, actorStaffId,
                    reason?.Trim(), null, null, null, null, null);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "[RestockWorkflow] Cancel RequestId={Id} ByStaff={Staff}",
                    requestId, actorStaffId);

                return ServiceResult<RestockRequestWorkflowDetailDto>.Success(
                    await MapWorkflowDetailAsync(request),
                    "Đã hủy yêu cầu nhập hàng.");
            }
            catch (Exception ex)
            {
                try { await transaction.RollbackAsync(); } catch { /* ignore */ }
                _context.ChangeTracker.Clear();
                _logger.LogError(ex, "[RestockWorkflow] Cancel failed RequestId={Id}", requestId);
                return Fail("Không hủy được yêu cầu. Vui lòng thử lại.", BranchReceiptErrorCodes.ConfirmFailed);
            }
        }

        public async Task<ServiceResult<RestockRequestWorkflowDetailDto>> CloseRemainingAsync(
            int requestId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames,
            string reason)
        {
            if (!CanWarehouseProcess(roleNames))
                return Fail("Chỉ bộ phận kho hoặc quản trị được đóng phần còn lại.", BranchReceiptErrorCodes.Unauthorized);
            if (string.IsNullOrWhiteSpace(reason))
                return Fail("Lý do đóng phần còn lại là bắt buộc.", BranchReceiptErrorCodes.TransitionInvalid);

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var request = await LoadRestockRequestForUpdateAsync(requestId);
                if (request == null)
                    return Fail("Không tìm thấy yêu cầu nhập hàng.", BranchReceiptErrorCodes.RequestNotFound);
                var auth = await AuthorizeViewAsync(request, actorStaffId, actorStoreId, roleNames);
                if (!auth.IsSuccess)
                    return Fail(auth.Message, auth.ErrorCode);
                if (request.Status is not (RestockRequestStatuses.Processing or RestockRequestStatuses.PartiallyReceived))
                    return Fail($"Không thể đóng phần còn lại ở trạng thái {request.Status}.", BranchReceiptErrorCodes.TransitionInvalid);

                var summary = await _allocationService.GetSummaryAsync(requestId, lockRequest: false);
                if (summary == null || summary.RemainingToReceiveQuantity <= 0)
                    return Fail("Yêu cầu không còn số lượng cần đóng.", BranchReceiptErrorCodes.TransitionInvalid);

                var previous = request.Status;
                request.ClosedRemainingQuantity = summary.RemainingToReceiveQuantity;
                request.RemainingClosedByStaffId = actorStaffId;
                request.RemainingClosedAtUtc = DateTime.UtcNow;
                request.RemainingCloseReason = reason.Trim()[..Math.Min(reason.Trim().Length, 500)];
                request.Status = RestockRequestStatuses.Completed;
                request.UpdatedAt = DateTime.UtcNow;
                AddTransition(
                    request, previous, request.Status, actorStaffId,
                    $"CLOSE_REMAINING: {request.RemainingCloseReason}", null, null,
                    summary.FulfilledQuantity, summary.FulfilledQuantity, null);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                await LoadDetailNavigationsAsync(request);
                return ServiceResult<RestockRequestWorkflowDetailDto>.Success(
                    await MapWorkflowDetailAsync(request),
                    "Đã đóng phần còn lại; không phát sinh biến động tồn kho.");
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                return Fail("Yêu cầu vừa được cập nhật bởi người khác. Vui lòng tải lại.", BranchReceiptErrorCodes.ConcurrencyConflict);
            }
        }

        public async Task<ServiceResult<RestockFulfillmentDto>> LinkFulfillmentAsync(
            int requestId,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames,
            LinkRestockFulfillmentRequest input)
        {
            if (!CanWarehouseProcess(roleNames))
                return ServiceResult<RestockFulfillmentDto>.Failure(
                    "Chỉ Kế toán/kho hoặc quản trị được gắn fulfillment.",
                    errorCode: BranchReceiptErrorCodes.Unauthorized);

            if (input == null)
                return ServiceResult<RestockFulfillmentDto>.Failure("Thiếu dữ liệu fulfillment.");

            var source = (input.SourceType ?? string.Empty).Trim().ToUpperInvariant();
            if (source is not (RestockFulfillmentSourceTypes.Supplier or RestockFulfillmentSourceTypes.Manual))
            {
                return ServiceResult<RestockFulfillmentDto>.Failure(
                    "SourceType chỉ hỗ trợ SUPPLIER hoặc MANUAL trong #128 (không transfer dual-post).",
                    errorCode: BranchReceiptErrorCodes.TransitionInvalid);
            }

            if (input.PlannedBaseQuantity <= 0)
            {
                return ServiceResult<RestockFulfillmentDto>.Failure(
                    "PlannedBaseQuantity phải > 0.",
                    errorCode: BranchReceiptErrorCodes.QuantityInvalid);
            }

            // #128 fail-closed dual-post boundary: InventoryDocument confirm still posts via ProcessImportAsync.
            // Linking a detail without shared posting authority would allow Import Confirm + BranchReceipt Confirm
            // for the same source. Import/document dual-post coordination is deferred (not #129-sized refactor).
            if (input.InventoryDocumentDetailId.HasValue)
            {
                return ServiceResult<RestockFulfillmentDto>.Failure(
                    "Không gắn InventoryDocumentDetailId trong #128 — BranchReceipt và InventoryDocument confirm là hai posting path độc lập; tránh double-post. Dùng SUPPLIER/MANUAL không link document.",
                    errorCode: BranchReceiptErrorCodes.TransitionInvalid);
            }

            var request = await LoadRequestTrackedAsync(requestId);
            if (request == null)
                return ServiceResult<RestockFulfillmentDto>.Failure(
                    "Không tìm thấy yêu cầu nhập hàng.",
                    errorCode: BranchReceiptErrorCodes.RequestNotFound);

            var auth = await AuthorizeViewAsync(request, actorStaffId, actorStoreId, roleNames);
            if (!auth.IsSuccess)
                return ServiceResult<RestockFulfillmentDto>.Failure(auth.Message, errorCode: auth.ErrorCode);

            if (request.Status is RestockRequestStatuses.Rejected
                or RestockRequestStatuses.Cancelled
                or RestockRequestStatuses.Completed)
            {
                return ServiceResult<RestockFulfillmentDto>.Failure(
                    $"Không gắn fulfillment khi request {request.Status}.",
                    errorCode: BranchReceiptErrorCodes.RequestStateInvalid);
            }

            // Intent-only: auto move SUBMITTED → PROCESSING when warehouse starts fulfillment prep.
            if (request.Status == RestockRequestStatuses.Submitted)
            {
                var previous = request.Status;
                request.Status = RestockRequestStatuses.Processing;
                request.HandledByStaffId = actorStaffId;
                request.HandledAt = DateTime.UtcNow;
                request.UpdatedAt = DateTime.UtcNow;
                AddTransition(
                    request, previous, RestockRequestStatuses.Processing, actorStaffId,
                    "Auto PROCESSING on fulfillment link", null, null, null, null, null);
            }

            var fulfillment = new RestockRequestFulfillment
            {
                RestockRequestId = request.RestockRequestId,
                SourceType = source,
                InventoryDocumentDetailId = null, // never dual-link import detail in #128
                Status = RestockFulfillmentStatuses.Linked,
                PlannedBaseQuantity = input.PlannedBaseQuantity,
                CreatedAt = DateTime.UtcNow,
                CreatedByStaffId = actorStaffId,
                Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim()
            };

            _context.RestockRequestFulfillments.Add(fulfillment);
            await _context.SaveChangesAsync();

            // No inventory mutation by design.
            return ServiceResult<RestockFulfillmentDto>.Success(new RestockFulfillmentDto
            {
                RestockRequestFulfillmentId = fulfillment.RestockRequestFulfillmentId,
                SourceType = fulfillment.SourceType,
                Status = fulfillment.Status,
                PlannedBaseQuantity = fulfillment.PlannedBaseQuantity,
                Notes = fulfillment.Notes,
                CreatedAt = fulfillment.CreatedAt,
                CreatedByStaffId = fulfillment.CreatedByStaffId
            }, "Đã gắn fulfillment (không thay đổi tồn kho).");
        }

        private async Task<RestockRequest?> LoadRequestAsync(int requestId) =>
            await _context.RestockRequests
                .AsNoTracking()
                .Include(r => r.Ingredient)
                .Include(r => r.Recipe)
                .Include(r => r.PreparedItem)
                .Include(r => r.CreatedByStaff)
                .Include(r => r.Store)
                .Include(r => r.StockAlert)
                .Include(r => r.AcceptedByStaff)
                .FirstOrDefaultAsync(r => r.RestockRequestId == requestId);

        private async Task<RestockRequest?> LoadRequestTrackedAsync(int requestId)
        {
            var tracked = _context.ChangeTracker.Entries<RestockRequest>()
                .Select(x => x.Entity)
                .FirstOrDefault(x => x.RestockRequestId == requestId);
            if (tracked != null)
            {
                await LoadDetailNavigationsAsync(tracked);
                return tracked;
            }

            return await _context.RestockRequests
                .Include(r => r.Ingredient)
                .Include(r => r.Recipe)
                .Include(r => r.PreparedItem)
                .Include(r => r.CreatedByStaff)
                .Include(r => r.Store)
                .Include(r => r.StockAlert)
                .Include(r => r.AcceptedByStaff)
                .FirstOrDefaultAsync(r => r.RestockRequestId == requestId);
        }

        /// <summary>SQL Server: UPDLOCK so cancel/reject serialize with BranchReceipt confirm.</summary>
        private async Task<RestockRequest?> LoadRestockRequestForUpdateAsync(int restockRequestId)
        {
            if (_context.Database.IsSqlServer())
            {
                return await _context.RestockRequests
                    .FromSqlInterpolated(
                        $@"SELECT * FROM RestockRequests WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                           WHERE RestockRequestId = {restockRequestId}")
                    .SingleOrDefaultAsync();
            }

            return await _context.RestockRequests
                .SingleOrDefaultAsync(r => r.RestockRequestId == restockRequestId);
        }

        private async Task<bool> HasFulfillmentPostingAsync(int restockRequestId) =>
            await _context.RestockFulfillmentPostings
                .AsNoTracking()
                .AnyAsync(p => p.RestockRequestId == restockRequestId);

        private void AddTransition(
            RestockRequest request,
            string previous,
            string next,
            int actorStaffId,
            string? reason,
            int? branchReceiptId,
            int? inventoryTransactionId,
            decimal? qtyBefore,
            decimal? qtyAfter,
            string? requestKey)
        {
            _context.RestockRequestTransitions.Add(new RestockRequestTransition
            {
                RestockRequestId = request.RestockRequestId,
                PreviousStatus = previous,
                NewStatus = next,
                ActorStaffId = actorStaffId,
                OccurredAtUtc = DateTime.UtcNow,
                Reason = reason,
                BranchReceiptId = branchReceiptId,
                InventoryTransactionId = inventoryTransactionId,
                QuantityBefore = qtyBefore,
                QuantityAfter = qtyAfter,
                RequestKey = requestKey
            });
        }

        private async Task<RestockRequestWorkflowDetailDto> MapWorkflowDetailAsync(RestockRequest r)
        {
            var receivedQtys = await _context.RestockFulfillmentPostings
                .AsNoTracking()
                .Where(p => p.RestockRequestId == r.RestockRequestId)
                .Select(p => p.Quantity)
                .ToListAsync();
            var received = receivedQtys.Sum();
            var target = r.RequestedQuantity;
            var remaining = Math.Max(0m, target - received);
            var stockRecoveredExternally =
                r.StockAlert?.Status == StockAlertStatuses.Resolved &&
                RestockRequestStatuses.ActiveValues.Contains(r.Status) &&
                received < target;

            var timeline = await _context.RestockRequestTransitions
                .AsNoTracking()
                .Include(t => t.ActorStaff)
                .Where(t => t.RestockRequestId == r.RestockRequestId)
                .OrderBy(t => t.OccurredAtUtc)
                .ThenBy(t => t.RestockRequestTransitionId)
                .Select(t => new RestockTimelineItemDto
                {
                    TransitionId = t.RestockRequestTransitionId,
                    PreviousStatus = t.PreviousStatus,
                    NewStatus = t.NewStatus,
                    ActorStaffId = t.ActorStaffId,
                    ActorName = t.ActorStaff != null ? t.ActorStaff.FullName : null,
                    OccurredAtUtc = t.OccurredAtUtc,
                    Reason = t.Reason,
                    BranchReceiptId = t.BranchReceiptId,
                    InventoryTransferId = t.InventoryTransferId,
                    InventoryTransactionId = t.InventoryTransactionId,
                    QuantityBefore = t.QuantityBefore,
                    QuantityAfter = t.QuantityAfter,
                    RequestKey = t.RequestKey
                })
                .ToListAsync();

            var postings = await _context.RestockFulfillmentPostings
                .AsNoTracking()
                .Include(p => p.BaseUnit)
                .Where(p => p.RestockRequestId == r.RestockRequestId)
                .OrderByDescending(p => p.CreatedAtUtc)
                .ThenByDescending(p => p.RestockFulfillmentPostingId)
                .Select(p => new RestockFulfillmentPostingDto
                {
                    RestockFulfillmentPostingId = p.RestockFulfillmentPostingId,
                    SourceDocumentType = p.SourceDocumentType,
                    SourceDocumentId = p.SourceDocumentId,
                    SourceDocumentLineId = p.SourceDocumentLineId,
                    Quantity = p.Quantity,
                    BaseUnitId = p.BaseUnitId,
                    BaseUnitName = p.BaseUnit != null ? p.BaseUnit.Name : null,
                    CreatedAtUtc = p.CreatedAtUtc
                })
                .ToListAsync();

            var receiptEntities = await _context.BranchReceipts
                .AsNoTracking()
                .Include(x => x.Supplier)
                .Include(x => x.Lines)
                .Where(x => x.Lines.Any(l => l.RestockRequestId == r.RestockRequestId))
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            var receipts = receiptEntities.Select(x => new BranchReceiptListItemDto
            {
                BranchReceiptId = x.BranchReceiptId,
                ReceiptCode = x.ReceiptCode,
                ReceiptKey = x.ReceiptKey,
                Status = x.Status,
                StoreId = x.StoreId,
                SupplierId = x.SupplierId,
                SupplierName = x.Supplier?.Name,
                ReceivedAt = x.ReceivedAt,
                ConfirmedAt = x.ConfirmedAt,
                LineCount = x.Lines.Count,
                TotalBaseQuantity = x.Lines.Sum(l => l.ReceivedBaseQuantity),
                TotalLineCost = x.Lines.Sum(l => l.LineTotalCost)
            }).ToList();

            var fulfillments = await _context.RestockRequestFulfillments
                .AsNoTracking()
                .Where(f => f.RestockRequestId == r.RestockRequestId)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new RestockFulfillmentDto
                {
                    RestockRequestFulfillmentId = f.RestockRequestFulfillmentId,
                    SourceType = f.SourceType,
                    Status = f.Status,
                    PlannedBaseQuantity = f.PlannedBaseQuantity,
                    Notes = f.Notes,
                    CreatedAt = f.CreatedAt,
                    CreatedByStaffId = f.CreatedByStaffId
                })
                .ToListAsync();

            var allocation = await _allocationService.GetSummaryAsync(r.RestockRequestId)
                ?? new RestockAllocationSummaryDto
                {
                    RestockRequestId = r.RestockRequestId,
                    RequestedQuantity = r.RequestedQuantity,
                    FulfilledQuantity = received,
                    RemainingToReceiveQuantity = remaining,
                    RemainingUnallocatedQuantity = remaining
                };
            var hasTransfer = allocation.TransferAllocatedQuantity > 0;
            var hasPurchase = allocation.PurchaseAllocatedQuantity > 0
                || fulfillments.Any(x => x.SourceType == RestockFulfillmentSourceTypes.Supplier
                    && x.Status != RestockFulfillmentStatuses.Cancelled);
            var channel = hasTransfer && hasPurchase
                ? RestockFulfillmentChannels.Mixed
                : hasTransfer
                    ? RestockFulfillmentChannels.Transfer
                    : hasPurchase
                        ? RestockFulfillmentChannels.ExternalPurchase
                        : RestockFulfillmentChannels.Undecided;

            return new RestockRequestWorkflowDetailDto
            {
                RestockRequestId = r.RestockRequestId,
                StockAlertId = r.StockAlertId,
                StoreId = r.StoreId,
                StoreName = r.Store?.Name,
                ItemName = ResolveItemName(r),
                ItemTypeLabel = ResolveItemType(r),
                RequestedQuantity = r.RequestedQuantity,
                SuggestedQuantity = r.SuggestedQuantity,
                Status = r.Status,
                Priority = r.Priority,
                Note = r.Note,
                CreatedByName = r.CreatedByStaff?.FullName,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                IngredientId = r.IngredientId,
                RecipeId = r.RecipeId,
                PreparedItemId = r.PreparedItemId,
                CreatedByStaffId = r.CreatedByStaffId,
                AlertType = r.StockAlert?.AlertType,
                AlertStatus = r.StockAlert?.Status,
                AlertCurrentQtySnapshot = r.StockAlert?.CurrentQtySnapshot,
                AlertThresholdSnapshot = r.StockAlert?.ThresholdSnapshot,
                ReceivedQuantity = received,
                RemainingQuantity = remaining,
                TargetQuantity = target,
                StockRecoveredExternally = stockRecoveredExternally,
                FulfilledQuantity = allocation.FulfilledQuantity,
                TransferAllocatedQuantity = allocation.TransferAllocatedQuantity,
                PurchaseAllocatedQuantity = allocation.PurchaseAllocatedQuantity,
                RemainingUnallocatedQuantity = allocation.RemainingUnallocatedQuantity,
                RemainingToReceiveQuantity = allocation.RemainingToReceiveQuantity,
                ClosedRemainingQuantity = allocation.ClosedRemainingQuantity,
                FulfillmentChannel = channel,
                AcceptedByStaffId = r.AcceptedByStaffId,
                AcceptedByName = r.AcceptedByStaff?.FullName,
                AcceptedAtUtc = r.AcceptedAtUtc,
                ProcessingNote = r.ProcessingNote,
                RemainingCloseReason = r.RemainingCloseReason,
                Timeline = timeline,
                Receipts = receipts,
                Fulfillments = fulfillments,
                FulfillmentPostings = postings
            };
        }

        private async Task<ServiceResult> AuthorizeViewAsync(
            RestockRequest request,
            int actorStaffId,
            int? actorStoreId,
            IReadOnlyCollection<string> roleNames)
        {
            if (IsGlobalAdmin(roleNames))
                return ServiceResult.Success();

            if (roleNames.Contains(RoleConstants.AccountantWarehouse))
                return ServiceResult.Success();

            if (roleNames.Contains(RoleConstants.AreaManager))
            {
                return await _scopeAuthorization.CanAccessStoreAsync(actorStaffId, request.StoreId)
                    ? ServiceResult.Success()
                    : ServiceResult.Failure(
                        "Yêu cầu nằm ngoài phạm vi khu vực của bạn.",
                        errorCode: BranchReceiptErrorCodes.StoreMismatch);
            }

            if (roleNames.Contains(RoleConstants.StoreManager))
            {
                if (actorStoreId.HasValue && actorStoreId.Value == request.StoreId)
                    return ServiceResult.Success();
                return ServiceResult.Failure(
                    "Yêu cầu không thuộc cửa hàng của bạn.",
                    errorCode: BranchReceiptErrorCodes.StoreMismatch);
            }

            return ServiceResult.Failure(
                "Bạn không có quyền xem yêu cầu này.",
                errorCode: BranchReceiptErrorCodes.Unauthorized);
        }

        private static bool CanWarehouseProcess(IReadOnlyCollection<string> roles) =>
            roles.Contains(RoleConstants.AccountantWarehouse)
            || roles.Contains(RoleConstants.BusinessOwner)
            || roles.Contains(RoleConstants.AreaManager);

        private static bool CanCancel(IReadOnlyCollection<string> roles) =>
            CanWarehouseProcess(roles) || roles.Contains(RoleConstants.StoreManager);

        private static bool CanSubmit(IReadOnlyCollection<string> roles) =>
            roles.Contains(RoleConstants.StoreManager)
            || roles.Contains(RoleConstants.BusinessOwner)
            || roles.Contains(RoleConstants.AreaManager);

        private static bool IsStoreManagerOnly(IReadOnlyCollection<string> roles) =>
            roles.Contains(RoleConstants.StoreManager)
            && !CanWarehouseProcess(roles);

        private static bool IsGlobalAdmin(IReadOnlyCollection<string> roles) =>
            roles.Contains(RoleConstants.BusinessOwner);

        private static string ResolveItemName(RestockRequest r)
        {
            if (r.Ingredient != null) return r.Ingredient.Name;
            if (r.PreparedItem != null) return r.PreparedItem.Name;
            if (r.Recipe != null) return r.Recipe.Name ?? $"Recipe #{r.RecipeId}";
            return "—";
        }

        private static string ResolveItemType(RestockRequest r)
        {
            if (r.IngredientId.HasValue) return "Nguyên liệu";
            if (r.PreparedItemId.HasValue) return "Bán thành phẩm (PreparedItem)";
            if (r.RecipeId.HasValue) return "Bán thành phẩm (Recipe)";
            return "—";
        }

        private static string? AppendNote(string? existing, string addition)
        {
            if (string.IsNullOrWhiteSpace(existing)) return addition;
            var combined = existing.Trim() + "\n" + addition;
            return combined.Length > 500 ? combined[..500] : combined;
        }

        private async Task LoadDetailNavigationsAsync(RestockRequest request)
        {
            await _context.Entry(request).Reference(x => x.Ingredient).LoadAsync();
            await _context.Entry(request).Reference(x => x.Recipe).LoadAsync();
            await _context.Entry(request).Reference(x => x.PreparedItem).LoadAsync();
            await _context.Entry(request).Reference(x => x.CreatedByStaff).LoadAsync();
            await _context.Entry(request).Reference(x => x.Store).LoadAsync();
            await _context.Entry(request).Reference(x => x.StockAlert).LoadAsync();
            await _context.Entry(request).Reference(x => x.AcceptedByStaff).LoadAsync();
        }

        private static ServiceResult<RestockRequestWorkflowDetailDto> Fail(string message, string? code) =>
            ServiceResult<RestockRequestWorkflowDetailDto>.Failure(message, errorCode: code);
    }
}
