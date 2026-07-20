using System.Data;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Infrastrusture.Repositories;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Stock;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories
{
    public sealed class PurchaseAdviceService : IPurchaseAdviceService
    {
        private const string CounterKey = "PURCHASE_ADVICE";
        private readonly AppDbContext _context;
        private readonly IScopeAuthorizationService _scopeAuthorization;

        public PurchaseAdviceService(AppDbContext context, IScopeAuthorizationService scopeAuthorization)
        {
            _context = context;
            _scopeAuthorization = scopeAuthorization;
        }

        public async Task<ServiceResult<PurchaseAdvicePageDto>> GetPageAsync(
            PurchaseAdviceFilterDto filter,
            AdminActorContext actor)
        {
            var stores = await ResolveReadableStoresAsync(actor);
            if (stores.Count == 0)
                return Failure<PurchaseAdvicePageDto>(PurchaseAdviceErrorCodes.Forbidden, "Bạn không có quyền xem đề nghị mua hàng.");

            var allowedStoreIds = stores.Select(x => x.StoreId).ToArray();
            if (filter.StoreId.HasValue && !allowedStoreIds.Contains(filter.StoreId.Value))
                return Failure<PurchaseAdvicePageDto>(PurchaseAdviceErrorCodes.StoreScopeMismatch, "Cửa hàng không thuộc phạm vi truy cập của bạn.");

            var query = _context.PurchaseAdvices.AsNoTracking()
                .Where(x => allowedStoreIds.Contains(x.StoreId));
            if (filter.StoreId.HasValue) query = query.Where(x => x.StoreId == filter.StoreId.Value);
            if (!string.IsNullOrWhiteSpace(filter.Status)) query = query.Where(x => x.Status == filter.Status);
            if (!string.IsNullOrWhiteSpace(filter.Priority)) query = query.Where(x => x.Priority == filter.Priority);
            if (filter.IngredientId.HasValue) query = query.Where(x => x.Lines.Any(l => l.IngredientId == filter.IngredientId.Value));
            if (filter.FromDate.HasValue) query = query.Where(x => x.CreatedAtUtc >= filter.FromDate.Value.Date);
            if (filter.ToDate.HasValue) query = query.Where(x => x.CreatedAtUtc < filter.ToDate.Value.Date.AddDays(1));

            var items = await query
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => new PurchaseAdviceListItemDto
                {
                    PurchaseAdviceId = x.PurchaseAdviceId,
                    AdviceNumber = x.AdviceNumber,
                    StoreId = x.StoreId,
                    StoreName = x.Store.Name,
                    RequestedByName = x.RequestedByStaff.FullName,
                    Status = x.Status,
                    Priority = x.Priority,
                    NeededByDate = x.NeededByDate,
                    CreatedAtUtc = x.CreatedAtUtc
                })
                .ToListAsync();
            var itemIds = items.Select(x => x.PurchaseAdviceId).ToArray();
            var sourceRows = await _context.PurchaseAdviceLines.AsNoTracking()
                .Where(x => itemIds.Contains(x.PurchaseAdviceId))
                .Select(x => new { x.PurchaseAdviceId, x.RestockRequestId, x.RequestedPurchaseBaseQuantity })
                .ToListAsync();
            var sourcesByAdvice = sourceRows.GroupBy(x => x.PurchaseAdviceId).ToDictionary(x => x.Key, x => x.ToList());
            foreach (var item in items)
            {
                var lines = sourcesByAdvice.GetValueOrDefault(item.PurchaseAdviceId) ?? new();
                item.SourceRestockSummary = lines.Count == 0
                    ? "—"
                    : string.Join(", ", lines.OrderBy(x => x.RestockRequestId).Select(x => "#" + x.RestockRequestId));
                item.LineCount = lines.Count;
                item.TotalRequestedBaseQuantity = lines.Sum(x => x.RequestedPurchaseBaseQuantity);
            }

            IReadOnlyList<PurchaseAdviceSourceDto> sources = Array.Empty<PurchaseAdviceSourceDto>();
            var sourceStoreId = filter.StoreId ?? (CanCreate(actor) ? actor.StoreIdOrNull : null);
            if (sourceStoreId.HasValue && CanCreateForStore(actor, sourceStoreId.Value))
            {
                var sourceResult = await GetAvailableSourcesAsync(sourceStoreId.Value, actor);
                if (sourceResult.IsSuccess) sources = sourceResult.Data!;
            }

            return ServiceResult<PurchaseAdvicePageDto>.Success(new PurchaseAdvicePageDto
            {
                Filter = filter,
                Items = items,
                AvailableSources = sources,
                Stores = stores.Select(x => (x.StoreId, x.Name)).ToList(),
                Actor = actor
            });
        }

        public async Task<ServiceResult<IReadOnlyList<PurchaseAdviceSourceDto>>> GetAvailableSourcesAsync(
            int storeId,
            AdminActorContext actor)
        {
            if (!CanCreateForStore(actor, storeId))
                return Failure<IReadOnlyList<PurchaseAdviceSourceDto>>(PurchaseAdviceErrorCodes.Forbidden, "Chỉ Quản lý chi nhánh được tạo đề nghị mua cho chi nhánh của mình.");

            var requestIds = await _context.RestockRequests.AsNoTracking()
                .Where(x => x.StoreId == storeId
                    && x.IngredientId.HasValue
                    && (x.Status == RestockRequestStatuses.Processing || x.Status == RestockRequestStatuses.PartiallyReceived))
                .OrderByDescending(x => x.Priority)
                .ThenBy(x => x.CreatedAt)
                .Select(x => x.RestockRequestId)
                .ToListAsync();
            var result = new List<PurchaseAdviceSourceDto>();
            foreach (var requestId in requestIds)
            {
                var source = await BuildSourceAsync(requestId, null, false);
                if (source != null
                    && source.RemainingToPurchaseQuantity > 0
                    && source.ExistingPurchaseAdviceQuantity == 0)
                    result.Add(source);
            }

            return ServiceResult<IReadOnlyList<PurchaseAdviceSourceDto>>.Success(result);
        }

        public async Task<ServiceResult<PurchaseAdviceDetailDto>> GetDetailAsync(
            int purchaseAdviceId,
            AdminActorContext actor)
        {
            var advice = await LoadAdviceAsync(purchaseAdviceId, false);
            if (advice == null)
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.NotFound, "Không tìm thấy đề nghị mua hàng.");
            if (!await CanReadStoreAsync(actor, advice.StoreId))
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.Forbidden, "Bạn không có quyền xem đề nghị mua hàng này.");
            return ServiceResult<PurchaseAdviceDetailDto>.Success(await MapDetailAsync(advice, actor));
        }

        public async Task<ServiceResult<PurchaseAdviceDetailDto>> CreateAsync(
            CreatePurchaseAdviceRequest request,
            AdminActorContext actor)
        {
            if (!CanCreateForStore(actor, request.StoreId))
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.Forbidden, "Chỉ Quản lý chi nhánh được tạo đề nghị mua cho chi nhánh của mình.");
            if (request.Lines.Count == 0)
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.Empty, "Đề nghị mua phải có ít nhất một dòng.");
            if (request.Lines.Select(x => x.RestockRequestId).Distinct().Count() != request.Lines.Count)
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.AlreadyExists, "Một yêu cầu nhập chỉ được xuất hiện một lần trong đề nghị mua.");
            if (string.IsNullOrWhiteSpace(request.RequestKey))
                request.RequestKey = Guid.NewGuid().ToString("N");
            if (!PurchaseAdvicePriorities.All.Contains(request.Priority))
                request.Priority = PurchaseAdvicePriorities.Normal;

            var replay = await _context.PurchaseAdvices.AsNoTracking()
                .SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey);
            if (replay != null)
            {
                if (replay.StoreId != request.StoreId || replay.RequestedByStaffId != actor.StaffId)
                    return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.AlreadyExists, "Mã yêu cầu đã được sử dụng cho thao tác khác.");
                return await GetDetailAsync(replay.PurchaseAdviceId, actor);
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var validated = new List<(CreatePurchaseAdviceLineRequest Input, RestockRequest Source)>();
                foreach (var line in request.Lines.OrderBy(x => x.RestockRequestId))
                {
                    var validation = await ValidateSourceAsync(
                        line.RestockRequestId,
                        request.StoreId,
                        line.RequestedPurchaseBaseQuantity,
                        line.RestockRowVersion,
                        null,
                        true);
                    if (!validation.IsSuccess)
                    {
                        await transaction.RollbackAsync();
                        return Failure<PurchaseAdviceDetailDto>(validation.ErrorCode, validation.Message);
                    }
                    validated.Add((line, validation.Source!));
                }

                var now = DateTime.UtcNow;
                var sequence = await DocumentNumberCounterAllocator.NextAsync(_context, CounterKey, now);
                var advice = new PurchaseAdvice
                {
                    AdviceNumber = $"PA-{now:yyyyMMdd}-{sequence:0000}",
                    RequestKey = request.RequestKey.Trim(),
                    StoreId = request.StoreId,
                    RequestedByStaffId = actor.StaffId,
                    Status = PurchaseAdviceStatuses.Draft,
                    NeededByDate = NormalizeNeededByDate(request.NeededByDate),
                    Priority = request.Priority.ToUpperInvariant(),
                    Note = Clean(request.Note, 1000),
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                foreach (var item in validated)
                {
                    advice.Lines.Add(new PurchaseAdviceLine
                    {
                        RestockRequestId = item.Source.RestockRequestId,
                        IngredientId = item.Source.IngredientId!.Value,
                        RequestedPurchaseBaseQuantity = item.Input.RequestedPurchaseBaseQuantity,
                        BaseUnitId = item.Source.Ingredient!.BaseUnitId,
                        NeededByDate = NormalizeNeededByDate(item.Input.NeededByDate ?? request.NeededByDate),
                        Note = Clean(item.Input.Note, 500),
                        IsActiveReservation = true
                    });
                }
                advice.Transitions.Add(new PurchaseAdviceTransition
                {
                    PreviousStatus = null,
                    NewStatus = PurchaseAdviceStatuses.Draft,
                    ActorStaffId = actor.StaffId,
                    OccurredAtUtc = now,
                    Reason = "Tạo đề nghị mua từ nhu cầu bổ sung đã được xử lý."
                });
                _context.PurchaseAdvices.Add(advice);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return await GetDetailAsync(advice.PurchaseAdviceId, actor);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync();
                var existing = await _context.PurchaseAdvices.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey);
                if (existing != null) return await GetDetailAsync(existing.PurchaseAdviceId, actor);
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.AlreadyExists, "Yêu cầu nhập đã có đề nghị mua đang hiệu lực hoặc dữ liệu vừa được cập nhật.");
            }
        }

        public async Task<ServiceResult<PurchaseAdviceDetailDto>> UpdateAsync(
            UpdatePurchaseAdviceRequest request,
            AdminActorContext actor)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var advice = await LoadAdviceAsync(request.PurchaseAdviceId, true);
            if (advice == null) return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.NotFound, "Không tìm thấy đề nghị mua hàng.");
            if (!CanManageStoreAdvice(actor, advice.StoreId)) return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.Forbidden, "Bạn không có quyền sửa đề nghị mua này.");
            if (advice.Status != PurchaseAdviceStatuses.Draft) return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.NotEditable, "Chỉ đề nghị mua ở trạng thái Nháp được sửa.");
            if (!VersionMatches(advice.RowVersion, request.RowVersion)) return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.StaleVersion, "Đề nghị mua đã được người khác cập nhật. Vui lòng tải lại.");
            if (request.Lines.Count == 0 || request.Lines.Count != advice.Lines.Count)
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.Empty, "Không được xóa hoặc thêm nguồn yêu cầu nhập trong màn hình sửa. Hãy tạo đề nghị mới nếu cần.");

            foreach (var input in request.Lines)
            {
                var line = advice.Lines.SingleOrDefault(x => x.PurchaseAdviceLineId == input.PurchaseAdviceLineId);
                if (line == null) return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.SourceInvalid, "Dòng đề nghị mua không hợp lệ.");
                if (!VersionMatches(line.RowVersion, input.RowVersion)) return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.StaleVersion, "Dòng đề nghị mua đã thay đổi. Vui lòng tải lại.");
                var validation = await ValidateSourceAsync(line.RestockRequestId, advice.StoreId,
                    input.RequestedPurchaseBaseQuantity, null, line.PurchaseAdviceLineId, true);
                if (!validation.IsSuccess) return Failure<PurchaseAdviceDetailDto>(validation.ErrorCode, validation.Message);
                line.RequestedPurchaseBaseQuantity = input.RequestedPurchaseBaseQuantity;
                line.NeededByDate = NormalizeNeededByDate(input.NeededByDate ?? request.NeededByDate);
                line.Note = Clean(input.Note, 500);
            }
            advice.NeededByDate = NormalizeNeededByDate(request.NeededByDate);
            advice.Priority = PurchaseAdvicePriorities.All.Contains(request.Priority) ? request.Priority.ToUpperInvariant() : PurchaseAdvicePriorities.Normal;
            advice.Note = Clean(request.Note, 1000);
            advice.UpdatedAtUtc = DateTime.UtcNow;
            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return await GetDetailAsync(advice.PurchaseAdviceId, actor);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.StaleVersion, "Đề nghị mua đã được cập nhật đồng thời. Vui lòng tải lại.");
            }
        }

        public Task<ServiceResult<PurchaseAdviceDetailDto>> SubmitAsync(int id, PurchaseAdviceTransitionRequest request, AdminActorContext actor) =>
            TransitionAsync(id, request, actor, PurchaseAdviceStatuses.Draft, PurchaseAdviceStatuses.Submitted, false);

        public Task<ServiceResult<PurchaseAdviceDetailDto>> StartReviewAsync(int id, PurchaseAdviceTransitionRequest request, AdminActorContext actor) =>
            TransitionAsync(id, request, actor, PurchaseAdviceStatuses.Submitted, PurchaseAdviceStatuses.UnderReview, true);

        public async Task<ServiceResult<PurchaseAdviceDetailDto>> RejectAsync(int id, PurchaseAdviceTransitionRequest request, AdminActorContext actor)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.RejectionReasonRequired, "Phải nhập lý do từ chối đề nghị mua.");
            return await TransitionAsync(id, request, actor, PurchaseAdviceStatuses.UnderReview, PurchaseAdviceStatuses.Rejected, true);
        }

        public async Task<ServiceResult<PurchaseAdviceDetailDto>> CancelAsync(int id, PurchaseAdviceTransitionRequest request, AdminActorContext actor)
        {
            var advice = await LoadAdviceAsync(id, false);
            if (advice == null) return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.NotFound, "Không tìm thấy đề nghị mua hàng.");
            if (!CanManageStoreAdvice(actor, advice.StoreId)) return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.Forbidden, "Bạn không có quyền hủy đề nghị mua này.");
            if (advice.Status is not (PurchaseAdviceStatuses.Draft or PurchaseAdviceStatuses.Submitted))
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.NotEditable, "Không thể hủy đề nghị mua sau khi đã bắt đầu duyệt.");
            return await TransitionAsync(id, request, actor, advice.Status, PurchaseAdviceStatuses.Cancelled, false);
        }

        private async Task<ServiceResult<PurchaseAdviceDetailDto>> TransitionAsync(
            int id,
            PurchaseAdviceTransitionRequest request,
            AdminActorContext actor,
            string expected,
            string target,
            bool reviewerAction)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var advice = await LoadAdviceAsync(id, true);
            if (advice == null) return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.NotFound, "Không tìm thấy đề nghị mua hàng.");
            var allowed = reviewerAction ? CanReview(actor) : CanManageStoreAdvice(actor, advice.StoreId);
            if (!allowed) return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.Forbidden, "Bạn không có quyền thực hiện thao tác này.");
            if (advice.Status == target)
            {
                await transaction.RollbackAsync();
                return await GetDetailAsync(id, actor);
            }
            if (advice.Status != expected)
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.NotEditable, "Không thể chuyển trạng thái đề nghị mua trong trạng thái hiện tại.");
            if (!VersionMatches(advice.RowVersion, request.RowVersion))
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.StaleVersion, "Đề nghị mua đã thay đổi. Vui lòng tải lại.");
            if (target == PurchaseAdviceStatuses.Submitted)
            {
                if (advice.Lines.Count == 0) return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.Empty, "Đề nghị mua phải có ít nhất một dòng.");
                foreach (var line in advice.Lines)
                {
                    var validation = await ValidateSourceAsync(line.RestockRequestId, advice.StoreId,
                        line.RequestedPurchaseBaseQuantity, null, line.PurchaseAdviceLineId, true);
                    if (!validation.IsSuccess) return Failure<PurchaseAdviceDetailDto>(validation.ErrorCode, validation.Message);
                }
            }

            var now = DateTime.UtcNow;
            advice.Status = target;
            advice.UpdatedAtUtc = now;
            if (target == PurchaseAdviceStatuses.Submitted) advice.SubmittedAtUtc = now;
            if (target == PurchaseAdviceStatuses.UnderReview) { advice.ReviewedAtUtc = now; advice.ReviewedByStaffId = actor.StaffId; }
            if (target == PurchaseAdviceStatuses.Rejected) { advice.RejectedAtUtc = now; advice.RejectedByStaffId = actor.StaffId; advice.RejectionReason = Clean(request.Reason, 500); }
            if (target == PurchaseAdviceStatuses.Cancelled) { advice.CancelledAtUtc = now; advice.CancelledByStaffId = actor.StaffId; }
            if (!PurchaseAdviceStatuses.ActiveReservationStatuses.Contains(target))
                foreach (var line in advice.Lines) line.IsActiveReservation = false;
            advice.Transitions.Add(new PurchaseAdviceTransition
            {
                PreviousStatus = expected,
                NewStatus = target,
                ActorStaffId = actor.StaffId,
                OccurredAtUtc = now,
                Reason = Clean(request.Reason, 500)
            });
            try
            {
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return await GetDetailAsync(id, actor);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.StaleVersion, "Trạng thái đề nghị mua đã được cập nhật đồng thời.");
            }
        }

        private async Task<(bool IsSuccess, string ErrorCode, string Message, RestockRequest? Source)> ValidateSourceAsync(
            int restockRequestId,
            int storeId,
            decimal quantity,
            string? restockRowVersion,
            int? excludeAdviceLineId,
            bool lockSource)
        {
            if (quantity <= 0) return (false, PurchaseAdviceErrorCodes.QuantityInvalid, "Số lượng đề nghị mua phải lớn hơn 0.", null);
            var source = await LoadRestockAsync(restockRequestId, lockSource);
            if (source == null || !source.IngredientId.HasValue)
                return (false, PurchaseAdviceErrorCodes.SourceInvalid, "Nguồn yêu cầu nhập không tồn tại hoặc chưa hỗ trợ mua ngoài cho bán thành phẩm.", null);
            if (source.StoreId != storeId)
                return (false, PurchaseAdviceErrorCodes.StoreScopeMismatch, "Nguồn yêu cầu nhập không thuộc chi nhánh của đề nghị mua.", null);
            if (source.Status is not (RestockRequestStatuses.Processing or RestockRequestStatuses.PartiallyReceived))
                return (false, PurchaseAdviceErrorCodes.SourceInvalid, "Chỉ tạo đề nghị mua từ yêu cầu nhập đang xử lý hoặc đã nhận một phần.", null);
            if (!string.IsNullOrWhiteSpace(restockRowVersion) && !VersionMatches(source.RowVersion, restockRowVersion))
                return (false, PurchaseAdviceErrorCodes.StaleVersion, "Yêu cầu nhập đã thay đổi. Vui lòng tải lại số lượng còn lại.", null);
            var breakdown = await BuildSourceAsync(restockRequestId, excludeAdviceLineId, false);
            if (breakdown == null) return (false, PurchaseAdviceErrorCodes.SourceInvalid, "Không tải được số liệu nguồn yêu cầu nhập.", null);
            if (quantity > breakdown.RemainingToPurchaseQuantity)
                return (false, PurchaseAdviceErrorCodes.ExceedsRestockRemaining,
                    $"Số lượng {quantity:N3} vượt phần còn có thể đề nghị mua {breakdown.RemainingToPurchaseQuantity:N3}.", null);
            return (true, string.Empty, string.Empty, source);
        }

        private async Task<PurchaseAdviceSourceDto?> BuildSourceAsync(int restockRequestId, int? excludeAdviceLineId, bool lockSource)
        {
            var request = await LoadRestockAsync(restockRequestId, lockSource);
            if (request?.IngredientId == null) return null;
            var transfer = (await _context.InventoryTransferDetails.AsNoTracking()
                .Where(x => x.RestockRequestId == restockRequestId && x.InventoryTransfer.Status != InventoryTransferStatus.CANCELLED)
                .Select(x => x.BaseQuantity).ToListAsync()).Sum();
            var po = (await _context.PurchaseOrderLines.AsNoTracking()
                .Where(x => x.RestockRequestId == restockRequestId && x.PurchaseOrder.Status != PurchaseOrderStatuses.Cancelled)
                .Select(x => x.OrderedBaseQuantity - x.ClosedRemainingQuantity).ToListAsync()).Sum(x => Math.Max(0m, x));
            var pa = (await _context.PurchaseAdviceLines.AsNoTracking()
                .Where(x => x.RestockRequestId == restockRequestId && x.IsActiveReservation
                    && (!excludeAdviceLineId.HasValue || x.PurchaseAdviceLineId != excludeAdviceLineId.Value))
                .Select(x => x.RequestedPurchaseBaseQuantity - x.ClosedBaseQuantity).ToListAsync()).Sum(x => Math.Max(0m, x));
            var remaining = Math.Max(0m, request.RequestedQuantity - transfer - pa - po - request.ClosedRemainingQuantity);
            return new PurchaseAdviceSourceDto
            {
                RestockRequestId = request.RestockRequestId,
                StoreId = request.StoreId,
                StoreName = request.Store.Name,
                IngredientId = request.IngredientId.Value,
                IngredientName = request.Ingredient!.Name,
                BaseUnitId = request.Ingredient.BaseUnitId,
                BaseUnitName = request.Ingredient.BaseUnit.Name,
                Priority = request.Priority,
                RestockRequestedQuantity = request.RequestedQuantity,
                TransferAllocatedQuantity = transfer,
                ExistingPurchaseAdviceQuantity = pa,
                ExistingPurchaseOrderQuantity = po,
                ExplicitlyClosedQuantity = request.ClosedRemainingQuantity,
                RemainingToPurchaseQuantity = remaining,
                RestockRowVersion = Convert.ToBase64String(request.RowVersion)
            };
        }

        private async Task<PurchaseAdviceDetailDto> MapDetailAsync(PurchaseAdvice advice, AdminActorContext actor)
        {
            var dto = new PurchaseAdviceDetailDto
            {
                PurchaseAdviceId = advice.PurchaseAdviceId,
                AdviceNumber = advice.AdviceNumber,
                StoreId = advice.StoreId,
                StoreName = advice.Store.Name,
                RequestedByStaffId = advice.RequestedByStaffId,
                RequestedByName = advice.RequestedByStaff.FullName,
                Status = advice.Status,
                Priority = advice.Priority,
                NeededByDate = advice.NeededByDate,
                Note = advice.Note,
                RejectionReason = advice.RejectionReason,
                SubmittedAtUtc = advice.SubmittedAtUtc,
                CreatedAtUtc = advice.CreatedAtUtc,
                RowVersion = Convert.ToBase64String(advice.RowVersion),
                CanEdit = advice.Status == PurchaseAdviceStatuses.Draft && CanManageStoreAdvice(actor, advice.StoreId),
                CanSubmit = advice.Status == PurchaseAdviceStatuses.Draft && CanManageStoreAdvice(actor, advice.StoreId),
                CanCancel = advice.Status is PurchaseAdviceStatuses.Draft or PurchaseAdviceStatuses.Submitted && CanManageStoreAdvice(actor, advice.StoreId),
                CanReview = advice.Status == PurchaseAdviceStatuses.Submitted && CanReview(actor),
                CanReject = advice.Status == PurchaseAdviceStatuses.UnderReview && CanReview(actor)
            };
            foreach (var line in advice.Lines.OrderBy(x => x.PurchaseAdviceLineId))
            {
                var source = await BuildSourceAsync(line.RestockRequestId, line.PurchaseAdviceLineId, false);
                if (source == null) continue;
                dto.Lines.Add(new PurchaseAdviceLineDto
                {
                    PurchaseAdviceLineId = line.PurchaseAdviceLineId,
                    RestockRequestId = source.RestockRequestId,
                    StoreId = source.StoreId,
                    StoreName = source.StoreName,
                    IngredientId = source.IngredientId,
                    IngredientName = source.IngredientName,
                    BaseUnitId = source.BaseUnitId,
                    BaseUnitName = source.BaseUnitName,
                    Priority = source.Priority,
                    RestockRequestedQuantity = source.RestockRequestedQuantity,
                    TransferAllocatedQuantity = source.TransferAllocatedQuantity,
                    ExistingPurchaseAdviceQuantity = source.ExistingPurchaseAdviceQuantity,
                    ExistingPurchaseOrderQuantity = source.ExistingPurchaseOrderQuantity,
                    ExplicitlyClosedQuantity = source.ExplicitlyClosedQuantity,
                    RemainingToPurchaseQuantity = source.RemainingToPurchaseQuantity,
                    RestockRowVersion = source.RestockRowVersion,
                    RequestedPurchaseBaseQuantity = line.RequestedPurchaseBaseQuantity,
                    AllocatedToPoBaseQuantity = line.AllocatedToPoBaseQuantity,
                    AcceptedBaseQuantity = line.AcceptedBaseQuantity,
                    ClosedBaseQuantity = line.ClosedBaseQuantity,
                    NeededByDate = line.NeededByDate,
                    Note = line.Note,
                    RowVersion = Convert.ToBase64String(line.RowVersion)
                });
            }
            dto.Transitions = advice.Transitions.OrderBy(x => x.OccurredAtUtc).Select(x => new PurchaseAdviceTransitionDto
            {
                PreviousStatus = x.PreviousStatus,
                NewStatus = x.NewStatus,
                ActorName = x.ActorStaff.FullName,
                OccurredAtUtc = x.OccurredAtUtc,
                Reason = x.Reason
            }).ToList();
            return dto;
        }

        private async Task<PurchaseAdvice?> LoadAdviceAsync(int id, bool lockRow)
        {
            IQueryable<PurchaseAdvice> query;
            if (lockRow && _context.Database.IsSqlServer())
                query = _context.PurchaseAdvices.FromSqlInterpolated($"SELECT * FROM PurchaseAdvices WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE PurchaseAdviceId = {id}");
            else
                query = _context.PurchaseAdvices;
            return await query.Include(x => x.Store).Include(x => x.RequestedByStaff)
                .Include(x => x.Lines).Include(x => x.Transitions).ThenInclude(x => x.ActorStaff)
                .SingleOrDefaultAsync(x => x.PurchaseAdviceId == id);
        }

        private async Task<RestockRequest?> LoadRestockAsync(int id, bool lockRow)
        {
            var tracked = _context.ChangeTracker.Entries<RestockRequest>().Select(x => x.Entity)
                .FirstOrDefault(x => x.RestockRequestId == id);
            if (tracked != null)
            {
                await _context.Entry(tracked).Reference(x => x.Store).LoadAsync();
                await _context.Entry(tracked).Reference(x => x.Ingredient).Query().Include(x => x.BaseUnit).LoadAsync();
                return tracked;
            }
            IQueryable<RestockRequest> query;
            if (lockRow && _context.Database.IsSqlServer())
                query = _context.RestockRequests.FromSqlInterpolated($"SELECT * FROM RestockRequests WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE RestockRequestId = {id}");
            else
                query = _context.RestockRequests;
            return await query.Include(x => x.Store).Include(x => x.Ingredient).ThenInclude(x => x!.BaseUnit)
                .SingleOrDefaultAsync(x => x.RestockRequestId == id);
        }

        private async Task<List<(int StoreId, string Name)>> ResolveReadableStoresAsync(AdminActorContext actor)
        {
            var stores = await _context.Stores.AsNoTracking().Where(x => x.Active)
                .OrderBy(x => x.Name).Select(x => new { x.StoreId, x.Name }).ToListAsync();
            if (HasRole(actor, RoleConstants.BusinessOwner) || HasRole(actor, RoleConstants.AccountantWarehouse))
                return stores.Select(x => (x.StoreId, x.Name)).ToList();
            if (HasRole(actor, RoleConstants.StoreManager) && actor.StoreId > 0)
                return stores.Where(x => x.StoreId == actor.StoreId).Select(x => (x.StoreId, x.Name)).ToList();
            if (HasRole(actor, RoleConstants.AreaManager))
            {
                var accessible = new List<(int, string)>();
                foreach (var store in stores)
                    if (await _scopeAuthorization.CanAccessStoreAsync(actor.StaffId, store.StoreId)) accessible.Add((store.StoreId, store.Name));
                return accessible;
            }
            return new();
        }

        private async Task<bool> CanReadStoreAsync(AdminActorContext actor, int storeId)
        {
            if (HasRole(actor, RoleConstants.BusinessOwner) || HasRole(actor, RoleConstants.AccountantWarehouse)) return true;
            if (HasRole(actor, RoleConstants.StoreManager)) return actor.StoreId == storeId;
            return HasRole(actor, RoleConstants.AreaManager) && await _scopeAuthorization.CanAccessStoreAsync(actor.StaffId, storeId);
        }

        private static bool CanCreate(AdminActorContext actor) => HasRole(actor, RoleConstants.StoreManager);
        private static bool CanCreateForStore(AdminActorContext actor, int storeId) => CanCreate(actor) && actor.StoreId == storeId;
        private static bool CanManageStoreAdvice(AdminActorContext actor, int storeId) => CanCreateForStore(actor, storeId);
        private static bool CanReview(AdminActorContext actor) => HasRole(actor, RoleConstants.AccountantWarehouse) || HasRole(actor, RoleConstants.BusinessOwner);
        private static bool HasRole(AdminActorContext actor, string role) => actor.RoleNames.Contains(role, StringComparer.OrdinalIgnoreCase);
        private static DateTime NormalizeNeededByDate(DateTime value) => DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);
        private static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
        private static bool VersionMatches(byte[] current, string? provided)
        {
            if (string.IsNullOrWhiteSpace(provided)) return false;
            try { return current.SequenceEqual(Convert.FromBase64String(provided)); }
            catch (FormatException) { return false; }
        }
        private static ServiceResult<T> Failure<T>(string code, string message) => ServiceResult<T>.Failure(message, errorCode: code);
    }
}
