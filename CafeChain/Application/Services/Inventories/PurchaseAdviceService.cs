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
using CafeChain.Models.Inventories.Ingredients;
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
    private readonly IUnitConversionService _unitConversion;

        public PurchaseAdviceService(
            AppDbContext context,
            IScopeAuthorizationService scopeAuthorization,
            IUnitConversionService? unitConversion = null)
    {
        _context = context;
        _scopeAuthorization = scopeAuthorization;
        _unitConversion = unitConversion!;
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
            if (sourceStoreId.HasValue && await CanCreateForStoreAsync(actor, sourceStoreId.Value))
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
            if (!await CanCreateForStoreAsync(actor, storeId))
                return Failure<IReadOnlyList<PurchaseAdviceSourceDto>>(PurchaseAdviceErrorCodes.Forbidden, "Bạn không có quyền tạo đề nghị mua cho cửa hàng này.");

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
                    && HasRemainingToPurchase(source)
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
            if (!await CanCreateForStoreAsync(actor, request.StoreId))
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.Forbidden, "Bạn không có quyền tạo đề nghị mua cho cửa hàng này.");
            if (request.Lines.Count == 0)
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.Empty, "Đề nghị mua phải có ít nhất một dòng.");
            if (request.Lines.Any(x => !x.RestockRequestId.HasValue))
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.SourceInvalid, "Mỗi dòng đề nghị mua phải gắn một yêu cầu bổ sung.");
            if (request.Lines.Select(x => x.RestockRequestId!.Value).Distinct().Count() != request.Lines.Count)
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

            await using var ownedTransaction = _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable)
                : null;
            try
            {
                var validated = new List<(CreatePurchaseAdviceLineRequest Input, RestockRequest Source)>();
                foreach (var line in request.Lines.OrderBy(x => x.RestockRequestId))
                {
                    var validation = await ValidateSourceAsync(
                        line.RestockRequestId!.Value,
                        request.StoreId,
                        line.RequestedPurchaseBaseQuantity,
                        line.RequestedPurchaseProcurementQuantity,
                        line.RestockRowVersion,
                        null,
                        true);
                    if (!validation.IsSuccess)
                    {
                        if (ownedTransaction != null)
                            await ownedTransaction.RollbackAsync();
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
                        RequestedPurchaseBaseQuantity = item.Input.RequestedPurchaseBaseQuantity > 0
                            ? item.Input.RequestedPurchaseBaseQuantity
                            : item.Source.RequestedProcurementQuantity.GetValueOrDefault() > 0
                                ? item.Source.RequestedQuantity
                                    * item.Input.RequestedPurchaseProcurementQuantity.GetValueOrDefault(
                                        item.Source.RequestedProcurementQuantity.Value)
                                    / item.Source.RequestedProcurementQuantity.Value
                                : item.Source.RequestedQuantity,
                        RequestedProcurementQuantity = item.Input.RequestedPurchaseProcurementQuantity
                            ?? item.Source.RequestedProcurementQuantity,
                        ProcurementUnitId = item.Source.ProcurementUnitId,
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
                foreach (var line in advice.Lines)
                    await LinkPendingPurchaseAllocationsAsync(line);
                await _context.SaveChangesAsync();
                if (ownedTransaction != null)
                    await ownedTransaction.CommitAsync();
                return await GetDetailAsync(advice.PurchaseAdviceId, actor);
            }
            catch (DbUpdateException)
            {
                if (ownedTransaction != null)
                    await ownedTransaction.RollbackAsync();
                var existing = await _context.PurchaseAdvices.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey);
                if (existing != null) return await GetDetailAsync(existing.PurchaseAdviceId, actor);
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.AlreadyExists, "Yêu cầu nhập đã có đề nghị mua đang hiệu lực hoặc dữ liệu vừa được cập nhật.");
            }
        }

        public async Task<ServiceResult<PurchaseAdviceDetailDto>> AddRestockRequestToDraftAsync(
            AddRestockRequestToDraftPurchaseAdviceRequest request,
            AdminActorContext actor)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var advice = await LoadAdviceAsync(request.PurchaseAdviceId, true);
            if (advice == null)
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.NotFound, "Không tìm thấy đề nghị mua nháp.");
            if (!await CanManageStoreAdviceAsync(actor, advice.StoreId))
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.Forbidden, "Bạn không có quyền cập nhật đề nghị mua này.");
            if (advice.Status != PurchaseAdviceStatuses.Draft)
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.NotEditable, "Chỉ có thể thêm yêu cầu vào đề nghị mua ở trạng thái Bản nháp.");
            if (!VersionMatches(advice.RowVersion, request.PurchaseAdviceRowVersion))
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.StaleVersion, "Đề nghị mua đã thay đổi. Vui lòng tải lại.");
            if (advice.Lines.Any(x => x.RestockRequestId == request.RestockRequestId))
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.AlreadyExists, "Yêu cầu này đã có trong đề nghị mua đã chọn.");

            var source = await BuildSourceAsync(request.RestockRequestId, null, true);
            if (source == null || source.StoreId != advice.StoreId)
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.StoreScopeMismatch, "Yêu cầu không thuộc cùng cửa hàng với đề nghị mua.");
            var procurementQuantity = source.PendingPurchaseAllocationProcurementQuantity;
            var baseQuantity = source.PendingPurchaseAllocationBaseQuantity;
            var validation = await ValidateSourceAsync(
                request.RestockRequestId,
                advice.StoreId,
                baseQuantity,
                procurementQuantity,
                request.RestockRowVersion,
                null,
                true);
            if (!validation.IsSuccess)
                return Failure<PurchaseAdviceDetailDto>(validation.ErrorCode, validation.Message);

            var restock = validation.Source!;
            var line = new PurchaseAdviceLine
            {
                RestockRequestId = restock.RestockRequestId,
                IngredientId = restock.IngredientId!.Value,
                RequestedPurchaseBaseQuantity = baseQuantity,
                RequestedProcurementQuantity = procurementQuantity,
                ProcurementUnitId = restock.ProcurementUnitId,
                BaseUnitId = restock.Ingredient!.BaseUnitId,
                NeededByDate = advice.NeededByDate,
                IsActiveReservation = true
            };
            advice.Lines.Add(line);
            advice.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await LinkPendingPurchaseAllocationsAsync(line);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return await GetDetailAsync(advice.PurchaseAdviceId, actor);
        }

        public async Task<ServiceResult<PurchaseAdviceDetailDto>> CreateDirectAsync(
            CreatePurchaseAdviceRequest request,
            AdminActorContext actor)
        {
            if (!request.IsDirectProposal)
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.SourceInvalid, "Yêu cầu không phải đề nghị mua trực tiếp.");
            if (!await CanCreateForStoreAsync(actor, request.StoreId))
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.Forbidden, "Bạn không có quyền tạo đề nghị mua trực tiếp cho cửa hàng này.");
            if (request.Lines.Count == 0)
                return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.Empty, "Đề nghị mua phải có ít nhất một dòng.");
            if (string.IsNullOrWhiteSpace(request.RequestKey))
                request.RequestKey = Guid.NewGuid().ToString("N");

            var replay = await _context.PurchaseAdvices.AsNoTracking()
                .SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey);
            if (replay != null)
            {
                if (replay.StoreId != request.StoreId || replay.RequestedByStaffId != actor.StaffId)
                    return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.AlreadyExists, "Mã yêu cầu đã được sử dụng cho thao tác khác.");
                return await GetDetailAsync(replay.PurchaseAdviceId, actor);
            }

            var validated = new List<(CreatePurchaseAdviceLineRequest Input, Ingredient Ingredient, decimal BaseQuantity)>();
            foreach (var input in request.Lines)
            {
                if (!input.IngredientId.HasValue || !input.RequestedProcurementQuantity.HasValue
                    || !input.ProcurementUnitId.HasValue || input.RequestedProcurementQuantity <= 0)
                    return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.QuantityInvalid, "Dòng mua trực tiếp thiếu nguyên liệu, số lượng hoặc đơn vị mua hàng.");

                var ingredient = await _context.Ingredients.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.IngredientId == input.IngredientId.Value && x.Active);
                if (ingredient == null)
                    return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.SourceInvalid, "Nguyên liệu mua trực tiếp không hợp lệ.");

                var converted = await _unitConversion.ConvertAsync(
                    ingredient.IngredientId,
                    input.RequestedProcurementQuantity.Value,
                    input.ProcurementUnitId.Value,
                    ingredient.BaseUnitId);
                if (!converted.IsSuccess || converted.Data <= 0)
                    return Failure<PurchaseAdviceDetailDto>(
                        PurchaseAdviceErrorCodes.QuantityInvalid,
                        converted.Message ?? "Không quy đổi được đơn vị mua hàng sang đơn vị tồn kho.");

                validated.Add((input, ingredient, converted.Data));
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                var sourceLines = new List<CreatePurchaseAdviceLineRequest>();
                foreach (var item in validated)
                {
                    var input = item.Input;
                    var ingredient = item.Ingredient;
                    var baseQuantity = item.BaseQuantity;
                    var demand = new RestockRequest
                    {
                        StoreId = request.StoreId,
                        CreatedForStoreId = request.StoreId,
                        SourceType = RestockRequestSourceTypes.DirectPurchaseProposal,
                        SourceReferenceId = string.IsNullOrWhiteSpace(request.RequestKey)
                            ? Guid.NewGuid().ToString("N")
                            : request.RequestKey.Trim(),
                        NeedByDate = request.NeededByDate.ToUniversalTime(),
                        RequestedProcurementQuantity = input.RequestedProcurementQuantity.Value,
                        ProcurementUnitId = input.ProcurementUnitId.Value,
                        RequestedQuantity = baseQuantity,
                        SuggestedQuantity = baseQuantity,
                        IngredientId = ingredient.IngredientId,
                        Status = RestockRequestStatuses.Processing,
                        SourcingDecision = RestockSourcingDecisionTypes.Purchase,
                        SourcingStatus = RestockSourcingStatuses.PartiallyAllocated,
                        Priority = PurchaseAdvicePriorities.All.Contains(request.Priority)
                            ? request.Priority.ToUpperInvariant()
                            : PurchaseAdvicePriorities.Normal,
                        CreatedByStaffId = actor.StaffId,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        Note = Clean(request.Note, 1000)
                    };
                    demand.SourcingAllocations.Add(new RestockSourcingAllocation
                    {
                        DecisionType = RestockSourcingDecisionTypes.Purchase,
                        ProcurementQuantity = input.RequestedProcurementQuantity.Value,
                        ProcurementUnitId = input.ProcurementUnitId.Value,
                        Status = RestockSourcingAllocationStatuses.PendingPurchaseAdvice,
                        SourceDocumentType = RestockRequestSourceTypes.DirectPurchaseProposal,
                        Reason = "Đề nghị mua trực tiếp tạo audit nhu cầu bổ sung.",
                        CreatedByStaffId = actor.StaffId,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                    _context.RestockRequests.Add(demand);
                    await _context.SaveChangesAsync();
                    sourceLines.Add(new CreatePurchaseAdviceLineRequest
                    {
                        RestockRequestId = demand.RestockRequestId,
                        RequestedPurchaseBaseQuantity = baseQuantity,
                        RequestedPurchaseProcurementQuantity = input.RequestedProcurementQuantity.Value,
                        NeededByDate = input.NeededByDate,
                        Note = input.Note
                    });
                }

                request.IsDirectProposal = false;
                request.Lines = sourceLines;
                var result = await CreateAsync(request, actor);
                if (!result.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    _context.ChangeTracker.Clear();
                    return result;
                }

                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        public async Task<ServiceResult<PurchaseAdviceDetailDto>> UpdateAsync(
            UpdatePurchaseAdviceRequest request,
            AdminActorContext actor)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var advice = await LoadAdviceAsync(request.PurchaseAdviceId, true);
            if (advice == null) return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.NotFound, "Không tìm thấy đề nghị mua hàng.");
            if (!await CanManageStoreAdviceAsync(actor, advice.StoreId)) return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.Forbidden, "Bạn không có quyền sửa đề nghị mua này.");
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
                    input.RequestedPurchaseBaseQuantity,
                    input.RequestedPurchaseProcurementQuantity,
                    null,
                    line.PurchaseAdviceLineId,
                    true);
                if (!validation.IsSuccess) return Failure<PurchaseAdviceDetailDto>(validation.ErrorCode, validation.Message);
                if (line.RequestedProcurementQuantity.HasValue)
                {
                    var previousProcurement = line.RequestedProcurementQuantity.Value;
                    var nextProcurement = input.RequestedPurchaseProcurementQuantity
                        ?? previousProcurement;
                    if (previousProcurement > 0)
                        line.RequestedPurchaseBaseQuantity =
                            line.RequestedPurchaseBaseQuantity * nextProcurement / previousProcurement;
                    line.RequestedProcurementQuantity = input.RequestedPurchaseProcurementQuantity
                        ?? line.RequestedProcurementQuantity;
                }
                else
                {
                    line.RequestedPurchaseBaseQuantity = input.RequestedPurchaseBaseQuantity;
                }
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
            if (!await CanManageStoreAdviceAsync(actor, advice.StoreId)) return Failure<PurchaseAdviceDetailDto>(PurchaseAdviceErrorCodes.Forbidden, "Bạn không có quyền hủy đề nghị mua này.");
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
            var allowed = reviewerAction
                ? await CanReviewStoreAsync(actor, advice.StoreId)
                : await CanManageStoreAdviceAsync(actor, advice.StoreId);
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
                        line.RequestedPurchaseBaseQuantity,
                        line.RequestedProcurementQuantity,
                        null,
                        line.PurchaseAdviceLineId,
                        true);
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
            if (target == PurchaseAdviceStatuses.Cancelled)
            {
                var lineIds = advice.Lines.Select(x => x.PurchaseAdviceLineId).ToArray();
                var allocations = await _context.RestockSourcingAllocations
                    .Where(x => x.PurchaseAdviceLineId.HasValue
                        && lineIds.Contains(x.PurchaseAdviceLineId.Value)
                        && x.Status == RestockSourcingAllocationStatuses.Active
                        && x.PurchaseOrderLineId == null)
                    .ToListAsync();
                foreach (var allocation in allocations)
                {
                    allocation.Status = RestockSourcingAllocationStatuses.Released;
                    allocation.ReleasedAtUtc = now;
                    allocation.ReleasedByStaffId = actor.StaffId;
                    allocation.ReleaseReason = "Đề nghị mua bị hủy trước khi tạo đơn đặt hàng.";
                }
            }
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

        private async Task LinkPendingPurchaseAllocationsAsync(PurchaseAdviceLine line)
        {
            var pending = await _context.RestockSourcingAllocations
                .Where(x => x.RestockRequestId == line.RestockRequestId
                    && x.DecisionType == RestockSourcingDecisionTypes.Purchase
                    && x.Status == RestockSourcingAllocationStatuses.PendingPurchaseAdvice
                    && x.PurchaseAdviceLineId == null
                    && x.PurchaseOrderLineId == null)
                .OrderBy(x => x.RestockSourcingAllocationId)
                .ToListAsync();
            if (pending.Count == 0) return;

            foreach (var allocation in pending)
            {
                allocation.PurchaseAdviceLineId = line.PurchaseAdviceLineId;
                allocation.Status = RestockSourcingAllocationStatuses.Active;
            }
            line.RestockSourcingAllocationId = pending[0].RestockSourcingAllocationId;
        }

        private async Task<(bool IsSuccess, string ErrorCode, string Message, RestockRequest? Source)> ValidateSourceAsync(
            int restockRequestId,
            int storeId,
            decimal quantity,
            decimal? procurementQuantity,
            string? restockRowVersion,
            int? excludeAdviceLineId,
            bool lockSource)
        {
            if (quantity <= 0 && procurementQuantity.GetValueOrDefault() <= 0)
                return (false, PurchaseAdviceErrorCodes.QuantityInvalid, "Số lượng đề nghị mua phải lớn hơn 0.", null);
            var source = await LoadRestockAsync(restockRequestId, lockSource);
            if (source == null || !source.IngredientId.HasValue)
                return (false, PurchaseAdviceErrorCodes.SourceInvalid, "Nguồn yêu cầu nhập không tồn tại hoặc chưa hỗ trợ mua ngoài cho bán thành phẩm.", null);
            if (source.StoreId != storeId)
                return (false, PurchaseAdviceErrorCodes.StoreScopeMismatch, "Nguồn yêu cầu nhập không thuộc chi nhánh của đề nghị mua.", null);
            if (!string.Equals(source.SourceType, RestockRequestSourceTypes.Legacy, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(source.SourcingDecision, RestockSourcingDecisionTypes.Purchase, StringComparison.OrdinalIgnoreCase))
                return (false, PurchaseAdviceErrorCodes.SourceInvalid, "Nhu cầu phải được quyết định nguồn PURCHASE trước khi tạo đề nghị mua.", null);
            if (source.Status is not (RestockRequestStatuses.Processing or RestockRequestStatuses.PartiallyReceived))
                return (false, PurchaseAdviceErrorCodes.SourceInvalid, "Chỉ tạo đề nghị mua từ yêu cầu nhập đang xử lý hoặc đã nhận một phần.", null);
            if (!string.IsNullOrWhiteSpace(restockRowVersion) && !VersionMatches(source.RowVersion, restockRowVersion))
                return (false, PurchaseAdviceErrorCodes.StaleVersion, "Yêu cầu nhập đã thay đổi. Vui lòng tải lại số lượng còn lại.", null);
            var breakdown = await BuildSourceAsync(restockRequestId, excludeAdviceLineId, false);
            if (breakdown == null) return (false, PurchaseAdviceErrorCodes.SourceInvalid, "Không tải được số liệu nguồn yêu cầu nhập.", null);
            if (source.RequestedProcurementQuantity.HasValue)
            {
                var requested = procurementQuantity ?? source.RequestedProcurementQuantity.Value;
                var remaining = breakdown.RemainingToPurchaseProcurementQuantity.GetValueOrDefault();
                var allocatedForPurchase = breakdown.PendingPurchaseAllocationProcurementQuantity.GetValueOrDefault();
                if (!string.Equals(source.SourceType, RestockRequestSourceTypes.Legacy, StringComparison.OrdinalIgnoreCase)
                    && allocatedForPurchase > 0
                    && requested > allocatedForPurchase)
                    return (false, PurchaseAdviceErrorCodes.ExceedsRestockRemaining,
                        $"Số lượng {requested:N3} vượt phần đã chọn mua ngoài {allocatedForPurchase:N3} {breakdown.ProcurementUnitName}.", null);
                if (requested > remaining)
                    return (false, PurchaseAdviceErrorCodes.ExceedsRestockRemaining,
                        $"Số lượng {requested:N3} vượt phần còn có thể đề nghị mua {remaining:N3} {breakdown.ProcurementUnitName}.", null);
            }
            else if (quantity > breakdown.RemainingToPurchaseQuantity)
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
            var pendingPurchaseProcurement = (await _context.RestockSourcingAllocations.AsNoTracking()
                .Where(x => x.RestockRequestId == restockRequestId
                    && x.DecisionType == RestockSourcingDecisionTypes.Purchase
                    && x.Status == RestockSourcingAllocationStatuses.PendingPurchaseAdvice
                    && x.PurchaseAdviceLineId == null
                    && x.PurchaseOrderLineId == null)
                .Select(x => x.ProcurementQuantity)
                .ToListAsync()).Sum();
            var remaining = Math.Max(0m, request.RequestedQuantity - transfer - pa - po - request.ClosedRemainingQuantity);
            decimal? transferProcurement = null;
            decimal? purchaseAdviceProcurement = null;
            decimal? purchaseOrderProcurement = null;
            decimal? closedProcurement = null;
            decimal? remainingProcurement = null;
            if (request.RequestedProcurementQuantity.HasValue
                && request.ProcurementUnitId.HasValue
                && request.IngredientId.HasValue)
            {
                var factor = await GetProcurementToBaseFactorAsync(request);
                if (factor > 0)
                {
                    transferProcurement = transfer / factor;
                    var adviceRows = await _context.PurchaseAdviceLines.AsNoTracking()
                        .Where(x => x.RestockRequestId == restockRequestId
                            && x.IsActiveReservation
                            && (!excludeAdviceLineId.HasValue || x.PurchaseAdviceLineId != excludeAdviceLineId.Value))
                        .Select(x => new
                        {
                            x.RequestedProcurementQuantity,
                            x.ClosedProcurementQuantity,
                            x.RequestedPurchaseBaseQuantity,
                            x.ClosedBaseQuantity
                        })
                        .ToListAsync();
                    purchaseAdviceProcurement = adviceRows.Sum(x =>
                        Math.Max(
                            0m,
                            x.RequestedProcurementQuantity.HasValue
                                ? x.RequestedProcurementQuantity.Value - x.ClosedProcurementQuantity
                                : (x.RequestedPurchaseBaseQuantity - x.ClosedBaseQuantity) / factor));

                    var orderRows = await _context.PurchaseOrderLines.AsNoTracking()
                        .Where(x => x.RestockRequestId == restockRequestId
                            && x.PurchaseOrder.Status != PurchaseOrderStatuses.Cancelled)
                        .Select(x => new
                        {
                            x.OrderedProcurementQuantity,
                            x.ClosedProcurementQuantity,
                            x.OrderedBaseQuantity,
                            x.ClosedRemainingQuantity
                        })
                        .ToListAsync();
                    purchaseOrderProcurement = orderRows.Sum(x =>
                        Math.Max(
                            0m,
                            x.OrderedProcurementQuantity.HasValue
                                ? x.OrderedProcurementQuantity.Value - x.ClosedProcurementQuantity
                                : (x.OrderedBaseQuantity - x.ClosedRemainingQuantity) / factor));

                    closedProcurement = request.ClosedRemainingQuantity / factor;
                    remainingProcurement = Math.Max(
                        0m,
                        request.RequestedProcurementQuantity.Value
                            - transferProcurement.Value
                            - purchaseAdviceProcurement.Value
                            - purchaseOrderProcurement.Value
                            - closedProcurement.Value);
                }
            }
            var procurementToBaseFactor = await GetProcurementToBaseFactorAsync(request);
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
                RestockRequestedProcurementQuantity = request.RequestedProcurementQuantity,
                ProcurementUnitId = request.ProcurementUnitId,
                ProcurementUnitName = request.ProcurementUnit?.Name,
                TransferAllocatedQuantity = transfer,
                ExistingPurchaseAdviceQuantity = pa,
                ExistingPurchaseOrderQuantity = po,
                ExplicitlyClosedQuantity = request.ClosedRemainingQuantity,
                RemainingToPurchaseQuantity = remaining,
                TransferAllocatedProcurementQuantity = transferProcurement,
                ExistingPurchaseAdviceProcurementQuantity = purchaseAdviceProcurement,
                ExistingPurchaseOrderProcurementQuantity = purchaseOrderProcurement,
                ExplicitlyClosedProcurementQuantity = closedProcurement,
                RemainingToPurchaseProcurementQuantity = remainingProcurement,
                PendingPurchaseAllocationProcurementQuantity = request.RequestedProcurementQuantity.HasValue
                    ? pendingPurchaseProcurement
                    : null,
                PendingPurchaseAllocationBaseQuantity = request.RequestedProcurementQuantity.HasValue
                    ? pendingPurchaseProcurement * procurementToBaseFactor
                    : pendingPurchaseProcurement,
                RestockRowVersion = Convert.ToBase64String(request.RowVersion)
            };
        }

        private async Task<PurchaseAdviceDetailDto> MapDetailAsync(PurchaseAdvice advice, AdminActorContext actor)
        {
            var canManage = await CanManageStoreAdviceAsync(actor, advice.StoreId);
            var canReview = await CanReviewStoreAsync(actor, advice.StoreId);
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
                CanEdit = advice.Status == PurchaseAdviceStatuses.Draft && canManage,
                CanSubmit = advice.Status == PurchaseAdviceStatuses.Draft && canManage,
                CanCancel = advice.Status is PurchaseAdviceStatuses.Draft or PurchaseAdviceStatuses.Submitted && canManage,
                CanReview = advice.Status == PurchaseAdviceStatuses.Submitted && canReview,
                CanReject = advice.Status == PurchaseAdviceStatuses.UnderReview && canReview
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
                    RestockRequestedProcurementQuantity = source.RestockRequestedProcurementQuantity,
                    ProcurementUnitId = source.ProcurementUnitId,
                    ProcurementUnitName = source.ProcurementUnitName,
                    TransferAllocatedQuantity = source.TransferAllocatedQuantity,
                    ExistingPurchaseAdviceQuantity = source.ExistingPurchaseAdviceQuantity,
                    ExistingPurchaseOrderQuantity = source.ExistingPurchaseOrderQuantity,
                    ExplicitlyClosedQuantity = source.ExplicitlyClosedQuantity,
                    RemainingToPurchaseQuantity = source.RemainingToPurchaseQuantity,
                    TransferAllocatedProcurementQuantity = source.TransferAllocatedProcurementQuantity,
                    ExistingPurchaseAdviceProcurementQuantity = source.ExistingPurchaseAdviceProcurementQuantity,
                    ExistingPurchaseOrderProcurementQuantity = source.ExistingPurchaseOrderProcurementQuantity,
                    ExplicitlyClosedProcurementQuantity = source.ExplicitlyClosedProcurementQuantity,
                    RemainingToPurchaseProcurementQuantity = source.RemainingToPurchaseProcurementQuantity,
                    RestockRowVersion = source.RestockRowVersion,
                    RequestedPurchaseBaseQuantity = line.RequestedPurchaseBaseQuantity,
                    RequestedProcurementQuantity = line.RequestedProcurementQuantity,
                    AllocatedToPoProcurementQuantity = line.AllocatedToPoProcurementQuantity,
                    AcceptedProcurementQuantity = line.AcceptedProcurementQuantity,
                    ClosedProcurementQuantity = line.ClosedProcurementQuantity,
                    AllocatedToPoBaseQuantity = line.AllocatedToPoBaseQuantity,
                    AcceptedBaseQuantity = line.AcceptedBaseQuantity,
                    ClosedBaseQuantity = line.ClosedBaseQuantity,
                    RemainingToOrderQuantity = Math.Max(0m, line.RequestedPurchaseBaseQuantity - line.AllocatedToPoBaseQuantity),
                    RemainingToReceiveQuantity = Math.Max(0m, line.AllocatedToPoBaseQuantity - line.AcceptedBaseQuantity - line.ClosedBaseQuantity),
                    UnresolvedQuantity = Math.Max(0m, line.RequestedPurchaseBaseQuantity - line.AcceptedBaseQuantity - line.ClosedBaseQuantity),
                    RemainingToOrderProcurementQuantity = line.RequestedProcurementQuantity.HasValue
                        ? Math.Max(0m, line.RequestedProcurementQuantity.Value - line.AllocatedToPoProcurementQuantity)
                        : null,
                    RemainingToReceiveProcurementQuantity = line.RequestedProcurementQuantity.HasValue
                        ? Math.Max(0m, line.AllocatedToPoProcurementQuantity - line.AcceptedProcurementQuantity - line.ClosedProcurementQuantity)
                        : null,
                    UnresolvedProcurementQuantity = line.RequestedProcurementQuantity.HasValue
                        ? Math.Max(0m, line.RequestedProcurementQuantity.Value - line.AcceptedProcurementQuantity - line.ClosedProcurementQuantity)
                        : null,
                    LineStatus = PurchaseAdviceStatusPolicy.DeriveLineStatus(line, advice.Status),
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
            return await query.Include(x => x.Store)
                .Include(x => x.ProcurementUnit)
                .Include(x => x.Ingredient).ThenInclude(x => x!.BaseUnit)
                .SingleOrDefaultAsync(x => x.RestockRequestId == id);
        }

        private async Task<List<(int StoreId, string Name)>> ResolveReadableStoresAsync(AdminActorContext actor)
        {
            var stores = await _context.Stores.AsNoTracking().Where(x => x.Active)
                .OrderBy(x => x.Name).Select(x => new { x.StoreId, x.Name }).ToListAsync();
            if (HasRole(actor, RoleConstants.BusinessOwner))
                return stores.Select(x => (x.StoreId, x.Name)).ToList();
            if (HasRole(actor, RoleConstants.StoreManager) && actor.StoreId > 0)
                return stores.Where(x => x.StoreId == actor.StoreId).Select(x => (x.StoreId, x.Name)).ToList();
            if (HasRole(actor, RoleConstants.AccountantWarehouse)
                || HasRole(actor, RoleConstants.AreaManager))
            {
                var accessible = new List<(int StoreId, string Name)>();
                foreach (var store in stores)
                {
                    if (await _scopeAuthorization.CanAccessStoreAsync(actor.StaffId, store.StoreId))
                        accessible.Add((store.StoreId, store.Name));
                }
                return accessible;
            }
            return new();
        }

        private async Task<bool> CanReadStoreAsync(AdminActorContext actor, int storeId)
        {
            if (HasRole(actor, RoleConstants.BusinessOwner)) return true;
            if (HasRole(actor, RoleConstants.StoreManager)) return actor.StoreId == storeId;
            return (HasRole(actor, RoleConstants.AccountantWarehouse)
                    || HasRole(actor, RoleConstants.AreaManager))
                && await _scopeAuthorization.CanAccessStoreAsync(actor.StaffId, storeId);
        }

        private static bool CanCreate(AdminActorContext actor) =>
            HasRole(actor, RoleConstants.StoreManager)
            || HasRole(actor, RoleConstants.AccountantWarehouse)
            || HasRole(actor, RoleConstants.BusinessOwner);
        private async Task<bool> CanCreateForStoreAsync(AdminActorContext actor, int storeId)
        {
            if (HasRole(actor, RoleConstants.BusinessOwner)) return true;
            if (HasRole(actor, RoleConstants.StoreManager)) return actor.StoreId == storeId;
            return HasRole(actor, RoleConstants.AccountantWarehouse)
                && await _scopeAuthorization.CanAccessStoreAsync(actor.StaffId, storeId);
        }

        private Task<bool> CanManageStoreAdviceAsync(AdminActorContext actor, int storeId) =>
            CanCreateForStoreAsync(actor, storeId);

        private async Task<bool> CanReviewStoreAsync(AdminActorContext actor, int storeId)
        {
            if (HasRole(actor, RoleConstants.BusinessOwner)) return true;
            return HasRole(actor, RoleConstants.AccountantWarehouse)
                && await _scopeAuthorization.CanAccessStoreAsync(actor.StaffId, storeId);
        }
        private static bool HasRole(AdminActorContext actor, string role) => actor.RoleNames.Contains(role, StringComparer.OrdinalIgnoreCase);

        private static bool HasRemainingToPurchase(PurchaseAdviceSourceDto source) =>
            source.RemainingToPurchaseProcurementQuantity.GetValueOrDefault() > 0
                || (!source.RestockRequestedProcurementQuantity.HasValue
                    && source.RemainingToPurchaseQuantity > 0);

        private async Task<decimal> GetProcurementToBaseFactorAsync(RestockRequest request)
        {
            if (!request.IngredientId.HasValue
                || !request.ProcurementUnitId.HasValue
                || request.Ingredient == null)
                return 0m;

            if (request.RequestedProcurementQuantity.GetValueOrDefault() > 0
                && request.RequestedQuantity > 0)
                return request.RequestedQuantity / request.RequestedProcurementQuantity!.Value;

            var result = await _unitConversion.ConvertAsync(
                request.IngredientId.Value,
                1m,
                request.ProcurementUnitId.Value,
                request.Ingredient.BaseUnitId);
            return result?.IsSuccess == true && result.Data > 0 ? result.Data : 0m;
        }
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
