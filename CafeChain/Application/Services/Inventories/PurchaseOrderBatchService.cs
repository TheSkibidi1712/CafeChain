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
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories;

public sealed class PurchaseOrderBatchService : IPurchaseOrderBatchService
{
    private const string BatchCounterKey = "PURCHASE_ORDER_BATCH";
    private const string ChildCounterKey = "PURCHASE_ORDER";
    private readonly AppDbContext _context;
    private readonly IPurchaseAdviceConsolidationService _consolidation;
    private readonly IScopeAuthorizationService _scopeAuthorization;
    private readonly IPurchaseAdviceFulfillmentService _purchaseAdviceFulfillment;

    public PurchaseOrderBatchService(
        AppDbContext context,
        IPurchaseAdviceConsolidationService consolidation,
        IScopeAuthorizationService scopeAuthorization,
        IPurchaseAdviceFulfillmentService? purchaseAdviceFulfillment = null)
    {
        _context = context;
        _consolidation = consolidation;
        _scopeAuthorization = scopeAuthorization;
        _purchaseAdviceFulfillment = purchaseAdviceFulfillment
            ?? new PurchaseAdviceFulfillmentService(context);
    }

    public async Task<ServiceResult<PurchaseOrderBatchDetailDto>> CreateAsync(
        CreatePurchaseOrderBatchRequest request,
        AdminActorContext actor)
    {
        request.RequestKey = Clean(request.RequestKey, 64) ?? Guid.NewGuid().ToString("N");
        var replay = await _context.PurchaseOrderBatches.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey);
        if (replay != null) return await GetDetailAsync(replay.PurchaseOrderBatchId, actor);

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            if (_context.Database.IsSqlServer())
            {
                var resource = $"PurchaseOrderBatch:{request.RequestKey}";
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"EXEC sp_getapplock @Resource={resource}, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000");
            }
            replay = _context.Database.IsSqlServer()
                ? await _context.PurchaseOrderBatches.FromSqlInterpolated(
                    $"SELECT * FROM PurchaseOrderBatches WITH (UPDLOCK, HOLDLOCK) WHERE RequestKey = {request.RequestKey}").SingleOrDefaultAsync()
                : await _context.PurchaseOrderBatches.SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey);
            if (replay != null)
            {
                await transaction.CommitAsync();
                return await GetDetailAsync(replay.PurchaseOrderBatchId, actor);
            }

            var preview = await _consolidation.PreviewAsync(request.ToPreviewRequest(), actor);
            if (!preview.IsSuccess)
            {
                await transaction.RollbackAsync();
                return Failure<PurchaseOrderBatchDetailDto>(preview.ErrorCode ?? PurchaseOrderBatchErrorCodes.Invalid, preview.Message);
            }

            var data = preview.Data!;
            var now = DateTime.UtcNow;
            var from = request.ExpectedDeliveryFrom?.Date ?? data.Groups.SelectMany(x => x.Allocations).Min(x => x.NeededByDate.Date);
            var to = request.ExpectedDeliveryTo?.Date ?? data.Groups.SelectMany(x => x.Allocations).Max(x => x.NeededByDate.Date);
            if (to < from)
            {
                await transaction.RollbackAsync();
                return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.Invalid, "Khoảng giao hàng dự kiến không hợp lệ.");
            }

            var batchSequence = await DocumentNumberCounterAllocator.NextAsync(_context, BatchCounterKey, now);
            var batch = new PurchaseOrderBatch
            {
                BatchNumber = $"POB-{now:yyyyMMdd}-{batchSequence:0000}",
                RequestKey = request.RequestKey,
                SupplierId = request.SupplierId,
                Status = PurchaseOrderBatchStatuses.PendingApproval,
                Currency = "VND",
                ExpectedDeliveryFrom = DateTime.SpecifyKind(from, DateTimeKind.Utc),
                ExpectedDeliveryTo = DateTime.SpecifyKind(to, DateTimeKind.Utc),
                Note = Clean(request.Note, 1000),
                CreatedByStaffId = actor.StaffId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            var childByStore = new Dictionary<int, PurchaseOrder>();
            foreach (var group in data.Groups)
            {
                var groupAdviceLines = group.Allocations
                    .Select(allocation => _context.ChangeTracker.Entries<PurchaseAdviceLine>()
                        .Select(entry => entry.Entity)
                        .Single(line => line.PurchaseAdviceLineId == allocation.PurchaseAdviceLineId))
                    .ToArray();
                var procurementLines = groupAdviceLines
                    .Where(line => line.RequestedProcurementQuantity.HasValue && line.ProcurementUnitId.HasValue)
                    .ToArray();
                if (procurementLines.Length != 0 && procurementLines.Length != groupAdviceLines.Length)
                {
                    await transaction.RollbackAsync();
                    return Failure<PurchaseOrderBatchDetailDto>(
                        PurchaseOrderBatchErrorCodes.Invalid,
                        $"Không thể gộp dữ liệu procurement mới và dữ liệu legacy trong cùng dòng {group.IngredientName}.");
                }

                var procurementUnitIds = procurementLines
                    .Select(line => line.ProcurementUnitId!.Value)
                    .Distinct()
                    .ToArray();
                if (procurementUnitIds.Length > 1)
                {
                    await transaction.RollbackAsync();
                    return Failure<PurchaseOrderBatchDetailDto>(
                        PurchaseOrderBatchErrorCodes.Invalid,
                        $"Các đề nghị mua {group.IngredientName} không cùng đơn vị mua hàng.");
                }
                var procurementUnitId = procurementUnitIds.Length == 1
                    ? procurementUnitIds[0]
                    : (int?)null;

                decimal? totalProcurementQuantity = null;
                decimal? demandCoveredProcurementQuantity = null;
                decimal? roundingSurplusProcurementQuantity = null;
                if (procurementLines.Length > 0)
                {
                    var procurementAllocations = group.Allocations
                        .Select(allocation => new
                        {
                            Ordered = allocation.OrderedProcurementQuantity
                                ?? throw new InvalidOperationException(
                                    $"Dòng đề nghị mua #{allocation.PurchaseAdviceLineId} thiếu số lượng procurement."),
                            Covered = allocation.DemandCoveredProcurementQuantity
                                ?? throw new InvalidOperationException(
                                    $"Dòng đề nghị mua #{allocation.PurchaseAdviceLineId} thiếu số lượng procurement phủ nhu cầu.")
                        })
                        .ToArray();
                    totalProcurementQuantity = procurementAllocations.Sum(x => x.Ordered);
                    demandCoveredProcurementQuantity = procurementAllocations.Sum(x => x.Covered);
                    roundingSurplusProcurementQuantity = procurementAllocations.Sum(
                        x => Math.Max(0m, x.Ordered - x.Covered));
                }

                var batchLine = new PurchaseOrderBatchLine
                {
                    PurchaseMode = group.PurchaseMode,
                    IngredientId = group.IngredientId,
                    IngredientSupplierId = group.IngredientSupplierId,
                    PackageUnitId = group.PurchaseMode == PurchaseMode.Packaged ? group.PackageUnitId : null,
                    PackageQuantitySnapshot = group.PurchaseMode == PurchaseMode.Packaged ? group.PackageQuantity : null,
                    TotalPackageCount = group.PurchaseMode == PurchaseMode.Packaged ? group.PackageCount : null,
                    OrderedPackageCount = group.PurchaseMode == PurchaseMode.Packaged ? group.PackageCount : null,
                    TotalBaseQuantity = group.OrderedBaseQuantity,
                    TotalProcurementQuantity = totalProcurementQuantity,
                    DemandCoveredProcurementQuantity = demandCoveredProcurementQuantity,
                    RoundingSurplusProcurementQuantity = roundingSurplusProcurementQuantity,
                    ProcurementUnitId = procurementUnitId,
                    PackagePriceSnapshot = group.PurchaseMode == PurchaseMode.Packaged ? group.PackagePriceSnapshot : null,
                    UnitPricePerPackage = group.PurchaseMode == PurchaseMode.Packaged ? group.PackagePriceSnapshot : null,
                    UnitPricePerProcurementUnit = group.PurchaseMode == PurchaseMode.Loose
                        ? group.UnitPricePerProcurementUnit
                        : null,
                    LineTotal = group.LineTotal,
                    Currency = group.Currency,
                    Note = Clean(group.Specification, 500)
                };
                batch.Lines.Add(batchLine);

                foreach (var allocation in group.Allocations)
                {
                    var adviceLine = groupAdviceLines.Single(
                        line => line.PurchaseAdviceLineId == allocation.PurchaseAdviceLineId);
                    decimal? orderedProcurementQuantity = null;
                    decimal? coveredProcurementQuantity = null;
                    decimal? surplusProcurementQuantity = null;
                    if (adviceLine.RequestedProcurementQuantity.HasValue
                        && adviceLine.ProcurementUnitId.HasValue)
                    {
                        orderedProcurementQuantity = allocation.OrderedProcurementQuantity;
                        coveredProcurementQuantity = allocation.DemandCoveredProcurementQuantity;
                        surplusProcurementQuantity = Math.Max(
                            0m,
                            orderedProcurementQuantity.Value - coveredProcurementQuantity.Value);
                    }

                    if (!childByStore.TryGetValue(allocation.StoreId, out var child))
                    {
                        var childSequence = await DocumentNumberCounterAllocator.NextAsync(_context, ChildCounterKey, now);
                        child = new PurchaseOrder
                        {
                            Code = $"PO-{now:yyyyMMdd}-{childSequence:0000}",
                            StoreId = allocation.StoreId,
                            SupplierId = request.SupplierId,
                            Status = PurchaseOrderStatuses.Draft,
                            OrderDate = now,
                            ExpectedDeliveryAtUtc = allocation.NeededByDate,
                            CreatedByStaffId = actor.StaffId,
                            CreatedAtUtc = now,
                            UpdatedAtUtc = now,
                            Note = $"Đơn đặt hàng chi nhánh thuộc đơn gộp {batch.BatchNumber}",
                            PurchaseOrderBatch = batch
                        };
                        childByStore.Add(allocation.StoreId, child);
                    }
                    else if (allocation.NeededByDate < child.ExpectedDeliveryAtUtc)
                    {
                        child.ExpectedDeliveryAtUtc = allocation.NeededByDate;
                    }

                    var childLine = new PurchaseOrderLine
                    {
                        PurchaseMode = group.PurchaseMode,
                        RestockRequestId = allocation.RestockRequestId,
                        PurchaseAdviceLineId = allocation.PurchaseAdviceLineId,
                        IngredientId = group.IngredientId,
                        IngredientSupplierId = group.IngredientSupplierId,
                        PackageUnitIdSnapshot = group.PurchaseMode == PurchaseMode.Packaged ? group.PackageUnitId : null,
                        PackageQuantitySnapshot = group.PurchaseMode == PurchaseMode.Packaged ? group.PackageQuantity : null,
                        PackagePriceSnapshot = group.PurchaseMode == PurchaseMode.Packaged ? group.PackagePriceSnapshot : null,
                        PackageCount = group.PurchaseMode == PurchaseMode.Packaged ? allocation.PackageCount : null,
                        OrderedPackageCount = group.PurchaseMode == PurchaseMode.Packaged ? allocation.PackageCount : null,
                        OrderedBaseQuantity = allocation.OrderedBaseQuantity,
                        OrderedPackQuantity = group.PurchaseMode == PurchaseMode.Packaged ? allocation.PackageCount : null,
                        PackSizeProcurementQuantity = group.PurchaseMode == PurchaseMode.Packaged
                            && orderedProcurementQuantity.HasValue
                            && allocation.PackageCount > 0
                                ? orderedProcurementQuantity.Value / allocation.PackageCount.Value
                                : null,
                        ProcurementUnitId = adviceLine.ProcurementUnitId,
                        OrderedProcurementQuantity = orderedProcurementQuantity,
                        UnitPricePerPackage = group.PurchaseMode == PurchaseMode.Packaged
                            ? group.PackagePriceSnapshot
                            : null,
                        UnitPricePerProcurementUnit = group.PurchaseMode == PurchaseMode.Loose
                            ? group.UnitPricePerProcurementUnit
                            : null,
                        RoundingSurplusProcurementQuantity = surplusProcurementQuantity,
                        InventoryBaseUnitId = adviceLine.BaseUnitId,
                        // The procurement-to-inventory conversion is deliberately deferred
                        // until the branch confirms the receipt.
                        ProcurementToInventoryFactor = null,
                        PromisedLeadTimeDaysSnapshot = group.LeadTimeDays,
                        Note = $"Allocation từ {allocation.AdviceNumber}"
                    };
                    child.Lines.Add(childLine);
                    batchLine.Allocations.Add(new PurchaseOrderLineAllocation
                    {
                        PurchaseMode = group.PurchaseMode,
                        PurchaseAdviceLineId = allocation.PurchaseAdviceLineId,
                        PurchaseOrder = child,
                        PurchaseOrderLine = childLine,
                        AllocatedBaseQuantity = allocation.OrderedBaseQuantity,
                        AllocatedPackageQuantity = group.PurchaseMode == PurchaseMode.Packaged
                            ? allocation.PackageCount
                            : null,
                        AllocatedProcurementQuantity = orderedProcurementQuantity,
                        DemandCoveredProcurementQuantity = coveredProcurementQuantity,
                        RoundingSurplusProcurementQuantity = surplusProcurementQuantity,
                        ProcurementUnitId = adviceLine.ProcurementUnitId,
                        CreatedAtUtc = now
                    });

                    adviceLine.AllocatedToPoBaseQuantity = Math.Min(
                        adviceLine.RequestedPurchaseBaseQuantity,
                        adviceLine.AllocatedToPoBaseQuantity + allocation.DemandCoveredBaseQuantity);
                    if (coveredProcurementQuantity.HasValue && adviceLine.RequestedProcurementQuantity.HasValue)
                    {
                        adviceLine.AllocatedToPoProcurementQuantity = Math.Min(
                            adviceLine.RequestedProcurementQuantity.Value,
                            adviceLine.AllocatedToPoProcurementQuantity + coveredProcurementQuantity.Value);
                    }
                    adviceLine.PurchaseMode = group.PurchaseMode;
                    var remainingProcurement = adviceLine.RequestedProcurementQuantity.HasValue
                        ? Math.Max(
                            0m,
                            adviceLine.RequestedProcurementQuantity.Value
                                - adviceLine.AllocatedToPoProcurementQuantity
                                - adviceLine.ClosedProcurementQuantity)
                        : (decimal?)null;
                    var remainingBase = Math.Max(
                        0m,
                        adviceLine.RequestedPurchaseBaseQuantity
                            - adviceLine.AllocatedToPoBaseQuantity
                            - adviceLine.ClosedBaseQuantity);
                    adviceLine.IsActiveReservation = remainingProcurement.HasValue
                        ? remainingProcurement.Value > 0
                        : remainingBase > 0;
                }
            }

            var trackedAdvices = _context.ChangeTracker.Entries<PurchaseAdvice>()
                .Select(x => x.Entity)
                .DistinctBy(x => x.PurchaseAdviceId)
                .ToArray();
            foreach (var advice in trackedAdvices)
            {
                await _context.Entry(advice).Collection(x => x.Lines).LoadAsync();
                await _purchaseAdviceFulfillment.RecomputeHeaderStatusAsync(
                    advice.PurchaseAdviceId,
                    actor.StaffId,
                    $"Cập nhật phân bổ từ đơn đặt hàng gộp {batch.BatchNumber}.");
            }

            _context.PurchaseOrderBatches.Add(batch);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return await GetDetailAsync(batch.PurchaseOrderBatchId, actor);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            var existing = await _context.PurchaseOrderBatches.AsNoTracking().SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey);
            if (existing != null) return await GetDetailAsync(existing.PurchaseOrderBatchId, actor);
            throw;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ServiceResult<PurchaseOrderBatchDetailDto>> GetDetailAsync(int id, AdminActorContext actor)
    {
        var dto = await MapAsync(id);
        if (dto == null) return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.NotFound, "Không tìm thấy đơn đặt hàng gộp.");
        if (!await CanReadAsync(actor, dto.ChildPurchaseOrders.Select(x => x.StoreId).ToArray()))
            return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.Forbidden, "Bạn không có quyền xem đơn đặt hàng gộp này.");
        return ServiceResult<PurchaseOrderBatchDetailDto>.Success(dto);
    }

    public async Task<ServiceResult<IReadOnlyList<PurchaseOrderBatchListItemDto>>> ListAsync(string? status, int? supplierId, AdminActorContext actor)
    {
        var query = _context.PurchaseOrderBatches.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value);
        var allowed = await ResolveAllowedStoreIdsAsync(actor);
        if (allowed.Count == 0)
            return Failure<IReadOnlyList<PurchaseOrderBatchListItemDto>>(
                PurchaseOrderBatchErrorCodes.Forbidden,
                "Bạn không có quyền xem đơn đặt hàng gộp.");
        query = query.Where(x =>
            x.ChildPurchaseOrders.Any()
            && !x.ChildPurchaseOrders.Any(po => !allowed.Contains(po.StoreId)));
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).Select(x => new PurchaseOrderBatchListItemDto
        {
            PurchaseOrderBatchId = x.PurchaseOrderBatchId,
            BatchNumber = x.BatchNumber,
            SupplierId = x.SupplierId,
            SupplierName = x.Supplier.Name ?? string.Empty,
            Status = x.Status,
            TotalAmount = x.Lines.Sum(l => l.LineTotal),
            StoreCount = x.ChildPurchaseOrders.Select(po => po.StoreId).Distinct().Count(),
            ExpectedDeliveryFrom = x.ExpectedDeliveryFrom,
            ExpectedDeliveryTo = x.ExpectedDeliveryTo,
            CreatedAtUtc = x.CreatedAtUtc
        }).ToListAsync();
        return ServiceResult<IReadOnlyList<PurchaseOrderBatchListItemDto>>.Success(items);
    }

    public async Task<ServiceResult<PurchaseOrderBatchDetailDto>> ApproveAsync(int id, PurchaseOrderBatchTransitionRequest request, AdminActorContext actor)
    {
        var batch = await _context.PurchaseOrderBatches.Include(x => x.ChildPurchaseOrders).SingleOrDefaultAsync(x => x.PurchaseOrderBatchId == id);
        if (batch == null) return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.NotFound, "Không tìm thấy đơn đặt hàng gộp.");
        if (!await CanReadAsync(actor, batch.ChildPurchaseOrders.Select(x => x.StoreId).ToArray()))
            return Failure<PurchaseOrderBatchDetailDto>(
                PurchaseOrderBatchErrorCodes.Forbidden,
                "Bạn không có quyền duyệt đơn đặt hàng gộp này.");
        if (batch.Status == PurchaseOrderBatchStatuses.Approved) return await GetDetailAsync(id, actor);
        if (batch.Status != PurchaseOrderBatchStatuses.PendingApproval)
            return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.Invalid, "Không thể duyệt đơn đặt hàng gộp trong trạng thái hiện tại.");
        if (!VersionMatches(batch.RowVersion, request.RowVersion)) return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.StaleVersion, "Đơn đặt hàng gộp đã thay đổi. Hãy tải lại.");
        var now = DateTime.UtcNow;
        batch.Status = PurchaseOrderBatchStatuses.Approved;
        batch.ApprovedByStaffId = actor.StaffId;
        batch.ApprovedAtUtc = now;
        batch.UpdatedAtUtc = now;
        foreach (var child in batch.ChildPurchaseOrders.Where(x => x.Status == PurchaseOrderStatuses.Draft))
        {
            child.Status = PurchaseOrderStatuses.Approved;
            child.ApprovedByStaffId = actor.StaffId;
            child.ApprovedAtUtc = now;
            child.UpdatedAtUtc = now;
        }
        await _context.SaveChangesAsync();
        return await GetDetailAsync(id, actor);
    }

    public async Task<ServiceResult<PurchaseOrderBatchDetailDto>> CancelAsync(int id, PurchaseOrderBatchTransitionRequest request, AdminActorContext actor)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.Invalid, "Bắt buộc nhập lý do hủy đơn đặt hàng gộp.");
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var batch = await _context.PurchaseOrderBatches
            .Include(x => x.ChildPurchaseOrders).ThenInclude(x => x.Lines).ThenInclude(x => x.ReceiptPostings)
            .Include(x => x.Lines).ThenInclude(x => x.Allocations).ThenInclude(x => x.PurchaseAdviceLine).ThenInclude(x => x.PurchaseAdvice)
            .SingleOrDefaultAsync(x => x.PurchaseOrderBatchId == id);
        if (batch == null) return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.NotFound, "Không tìm thấy đơn đặt hàng gộp.");
        if (!await CanReadAsync(actor, batch.ChildPurchaseOrders.Select(x => x.StoreId).ToArray()))
            return Failure<PurchaseOrderBatchDetailDto>(
                PurchaseOrderBatchErrorCodes.Forbidden,
                "Bạn không có quyền hủy đơn đặt hàng gộp này.");
        if (!VersionMatches(batch.RowVersion, request.RowVersion)) return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.StaleVersion, "Đơn đặt hàng gộp đã thay đổi. Hãy tải lại.");
        if (batch.ChildPurchaseOrders.SelectMany(x => x.Lines).SelectMany(x => x.ReceiptPostings).Any())
            return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.Invalid, "Không thể hủy đơn đặt hàng gộp đã phát sinh nhận hàng.");
        if (batch.Status is PurchaseOrderBatchStatuses.Completed or PurchaseOrderBatchStatuses.Cancelled)
            return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.Invalid, "Không thể hủy đơn đặt hàng gộp trong trạng thái hiện tại.");

        var now = DateTime.UtcNow;
        batch.Status = PurchaseOrderBatchStatuses.Cancelled;
        batch.CancelledByStaffId = actor.StaffId;
        batch.CancelledAtUtc = now;
        batch.CancellationReason = Clean(request.Reason, 500);
        batch.UpdatedAtUtc = now;
        foreach (var child in batch.ChildPurchaseOrders)
        {
            child.Status = PurchaseOrderStatuses.Cancelled;
            child.CancelledAtUtc = now;
            child.UpdatedAtUtc = now;
        }
        var affectedAdviceLines = batch.Lines
            .SelectMany(x => x.Allocations)
            .Select(x => x.PurchaseAdviceLine)
            .DistinctBy(x => x.PurchaseAdviceLineId)
            .ToArray();
        foreach (var line in affectedAdviceLines)
        {
            if (line.PurchaseAdvice.Status != PurchaseAdviceStatuses.UnderReview)
            {
                line.PurchaseAdvice.Transitions.Add(new PurchaseAdviceTransition
                {
                    PreviousStatus = line.PurchaseAdvice.Status,
                    NewStatus = PurchaseAdviceStatuses.UnderReview,
                    ActorStaffId = actor.StaffId,
                    OccurredAtUtc = now,
                    Reason = $"Hoàn phân bổ do hủy đơn đặt hàng gộp {batch.BatchNumber}."
                });
            }
            line.PurchaseAdvice.Status = PurchaseAdviceStatuses.UnderReview;
            line.PurchaseAdvice.UpdatedAtUtc = now;
        }
        await _context.SaveChangesAsync();
        foreach (var line in affectedAdviceLines)
        {
            var activeAllocations = await _context.PurchaseOrderLineAllocations
                .Where(x => x.PurchaseAdviceLineId == line.PurchaseAdviceLineId
                    && x.PurchaseOrder.Status != PurchaseOrderStatuses.Cancelled)
                .Select(x => new
                {
                    x.AllocatedBaseQuantity,
                    x.DemandCoveredProcurementQuantity
                })
                .ToListAsync();
            var activeOrderedBaseQuantity = activeAllocations.Sum(x => x.AllocatedBaseQuantity);
            var maximumCoverable = Math.Max(0m, line.RequestedPurchaseBaseQuantity - line.ClosedBaseQuantity);
            line.AllocatedToPoBaseQuantity = Math.Min(maximumCoverable, activeOrderedBaseQuantity);
            decimal? remainingProcurement = null;
            if (line.RequestedProcurementQuantity.HasValue)
            {
                var activeProcurementQuantity = activeAllocations.Sum(
                    x => x.DemandCoveredProcurementQuantity ?? 0m);
                var maximumProcurementCoverable = Math.Max(
                    0m,
                    line.RequestedProcurementQuantity.Value - line.ClosedProcurementQuantity);
                line.AllocatedToPoProcurementQuantity = Math.Min(
                    maximumProcurementCoverable,
                    activeProcurementQuantity);
                remainingProcurement = Math.Max(
                    0m,
                    line.RequestedProcurementQuantity.Value
                        - line.AllocatedToPoProcurementQuantity
                        - line.ClosedProcurementQuantity);
            }
            line.IsActiveReservation = remainingProcurement.HasValue
                ? remainingProcurement.Value > 0
                : line.AllocatedToPoBaseQuantity < maximumCoverable;
        }
        await _context.SaveChangesAsync();
        await tx.CommitAsync();
        return await GetDetailAsync(id, actor);
    }

    public async Task RefreshStatusAsync(int id)
    {
        await PurchaseOrderBatchStatusUpdater.RefreshAsync(_context, id);
        await _context.SaveChangesAsync();
    }

    private async Task<PurchaseOrderBatchDetailDto?> MapAsync(int id)
    {
        var batch = await _context.PurchaseOrderBatches.AsNoTracking()
            .Include(x => x.Supplier).Include(x => x.CreatedByStaff).Include(x => x.ApprovedByStaff)
            .Include(x => x.Lines).ThenInclude(x => x.Ingredient).ThenInclude(x => x.BaseUnit)
            .Include(x => x.Lines).ThenInclude(x => x.PackageUnit)
            .Include(x => x.Lines).ThenInclude(x => x.ProcurementUnit)
            .Include(x => x.Lines).ThenInclude(x => x.Allocations).ThenInclude(x => x.PurchaseAdviceLine).ThenInclude(x => x.PurchaseAdvice)
            .Include(x => x.Lines).ThenInclude(x => x.Allocations).ThenInclude(x => x.ProcurementUnit)
            .Include(x => x.Lines).ThenInclude(x => x.Allocations).ThenInclude(x => x.PurchaseOrder).ThenInclude(x => x.Store)
            .Include(x => x.ChildPurchaseOrders).ThenInclude(x => x.Store)
            .Include(x => x.ChildPurchaseOrders).ThenInclude(x => x.Lines).ThenInclude(x => x.ReceiptPostings)
            .Include(x => x.ChildPurchaseOrders).ThenInclude(x => x.Lines).ThenInclude(x => x.ProcurementUnit)
            .AsSplitQuery()
            .SingleOrDefaultAsync(x => x.PurchaseOrderBatchId == id);
        if (batch == null) return null;
        return new PurchaseOrderBatchDetailDto
        {
            PurchaseOrderBatchId = batch.PurchaseOrderBatchId,
            BatchNumber = batch.BatchNumber,
            SupplierId = batch.SupplierId,
            SupplierName = batch.Supplier.Name ?? string.Empty,
            Status = batch.Status,
            Currency = batch.Currency,
            ExpectedDeliveryFrom = batch.ExpectedDeliveryFrom,
            ExpectedDeliveryTo = batch.ExpectedDeliveryTo,
            Note = batch.Note,
            CreatedAtUtc = batch.CreatedAtUtc,
            CreatedByStaffId = batch.CreatedByStaffId,
            CreatedByName = batch.CreatedByStaff.FullName,
            ApprovedByStaffId = batch.ApprovedByStaffId,
            ApprovedByName = batch.ApprovedByStaff?.FullName,
            ApprovedAtUtc = batch.ApprovedAtUtc,
            RowVersion = Convert.ToBase64String(batch.RowVersion),
            TotalAmount = batch.Lines.Sum(x => x.LineTotal),
            StoreCount = batch.ChildPurchaseOrders.Select(x => x.StoreId).Distinct().Count(),
            Lines = batch.Lines.Select(line => new PurchaseOrderBatchLineDto
            {
                PurchaseMode = line.PurchaseMode,
                PurchaseOrderBatchLineId = line.PurchaseOrderBatchLineId,
                IngredientId = line.IngredientId,
                IngredientName = line.Ingredient.Name,
                BaseUnitName = line.Ingredient.BaseUnit.Name,
                PackageUnitName = line.PackageUnit?.Name ?? string.Empty,
                PackageQuantitySnapshot = line.PackageQuantitySnapshot,
                TotalPackageCount = line.TotalPackageCount,
                TotalBaseQuantity = line.TotalBaseQuantity,
                DemandCoveredBaseQuantity = line.Allocations.Sum(a =>
                    Math.Min(
                        a.AllocatedBaseQuantity,
                        a.PurchaseAdviceLine.RequestedPurchaseBaseQuantity)),
                RoundingSurplusBaseQuantity = Math.Max(
                    0m,
                    line.TotalBaseQuantity - line.Allocations.Sum(a =>
                        Math.Min(
                            a.AllocatedBaseQuantity,
                            a.PurchaseAdviceLine.RequestedPurchaseBaseQuantity))),
                TotalProcurementQuantity = line.TotalProcurementQuantity,
                DemandCoveredProcurementQuantity = line.DemandCoveredProcurementQuantity,
                RoundingSurplusProcurementQuantity = line.RoundingSurplusProcurementQuantity,
                ProcurementUnitId = line.ProcurementUnitId,
                ProcurementUnitName = line.ProcurementUnit?.Name,
                PackagePriceSnapshot = line.PackagePriceSnapshot,
                UnitPricePerProcurementUnit = line.UnitPricePerProcurementUnit,
                LineTotal = line.LineTotal,
                Allocations = line.Allocations.Select(a => new PurchaseOrderBatchAllocationDto
                {
                    PurchaseMode = a.PurchaseMode,
                    PurchaseOrderLineAllocationId = a.PurchaseOrderLineAllocationId,
                    PurchaseAdviceLineId = a.PurchaseAdviceLineId,
                    PurchaseAdviceId = a.PurchaseAdviceLine.PurchaseAdviceId,
                    AdviceNumber = a.PurchaseAdviceLine.PurchaseAdvice.AdviceNumber,
                    PurchaseOrderId = a.PurchaseOrderId,
                    PurchaseOrderLineId = a.PurchaseOrderLineId,
                    StoreId = a.PurchaseOrder.StoreId,
                    StoreName = a.PurchaseOrder.Store.Name,
                    AllocatedBaseQuantity = a.AllocatedBaseQuantity,
                    AllocatedPackageQuantity = a.AllocatedPackageQuantity,
                    DemandCoveredBaseQuantity = Math.Min(
                        a.AllocatedBaseQuantity,
                        a.PurchaseAdviceLine.RequestedPurchaseBaseQuantity),
                    RoundingSurplusBaseQuantity = Math.Max(
                        0m,
                        a.AllocatedBaseQuantity - Math.Min(
                            a.AllocatedBaseQuantity,
                            a.PurchaseAdviceLine.RequestedPurchaseBaseQuantity)),
                    AllocatedProcurementQuantity = a.AllocatedProcurementQuantity,
                    DemandCoveredProcurementQuantity = a.DemandCoveredProcurementQuantity,
                    RoundingSurplusProcurementQuantity = a.RoundingSurplusProcurementQuantity,
                    ProcurementUnitId = a.ProcurementUnitId,
                    ProcurementUnitName = a.ProcurementUnit?.Name
                }).ToArray()
            }).ToArray(),
            ChildPurchaseOrders = batch.ChildPurchaseOrders.Select(po =>
            {
                var accepted = po.Lines.SelectMany(x => x.ReceiptPostings).Sum(x => x.AcceptedBaseQuantity);
                var ordered = po.Lines.Sum(x => x.OrderedBaseQuantity);
                var closed = po.Lines.Sum(x => x.ClosedRemainingQuantity);
                var procurementUnitNames = po.Lines
                    .Where(x => x.ProcurementUnit != null)
                    .Select(x => x.ProcurementUnit!.Name)
                    .Distinct()
                    .ToArray();
                var orderedProcurement = po.Lines.All(x => x.OrderedProcurementQuantity.HasValue)
                    ? po.Lines.Sum(x => x.OrderedProcurementQuantity!.Value)
                    : (decimal?)null;
                var acceptedProcurement = po.Lines.All(x =>
                        !x.OrderedProcurementQuantity.HasValue
                        || x.ReceiptPostings.All(posting => posting.AcceptedProcurementQuantity.HasValue))
                    ? po.Lines.SelectMany(x => x.ReceiptPostings)
                        .Sum(x => x.AcceptedProcurementQuantity ?? 0m)
                    : (decimal?)null;
                return new PurchaseOrderBatchChildDto
                {
                    PurchaseOrderId = po.PurchaseOrderId,
                    Code = po.Code,
                    StoreId = po.StoreId,
                    StoreName = po.Store.Name,
                    Status = po.Status,
                    TotalAmount = po.Lines.Sum(x => ProcurementPurchaseMath.CalculateLineTotal(
                        x.PurchaseMode,
                        x.PackageCount,
                        x.UnitPricePerPackage ?? x.PackagePriceSnapshot,
                        x.OrderedProcurementQuantity,
                        x.UnitPricePerProcurementUnit)),
                    OrderedBaseQuantity = ordered,
                    AcceptedBaseQuantity = accepted,
                    RemainingBaseQuantity = Math.Max(0m, ordered - accepted - closed),
                    OrderedProcurementQuantity = orderedProcurement,
                    AcceptedProcurementQuantity = acceptedProcurement,
                    RemainingProcurementQuantity = orderedProcurement.HasValue && acceptedProcurement.HasValue
                        ? Math.Max(0m, orderedProcurement.Value - acceptedProcurement.Value)
                        : null,
                    ProcurementUnitName = procurementUnitNames.Length == 1
                        ? procurementUnitNames[0]
                        : null
                };
            }).OrderBy(x => x.StoreName).ToArray()
        };
    }

    private async Task<bool> CanReadAsync(AdminActorContext actor, int[] stores)
    {
        var allowed = await ResolveAllowedStoreIdsAsync(actor);
        return stores.Length > 0 && stores.All(allowed.Contains);
    }

    private async Task<List<int>> ResolveAllowedStoreIdsAsync(AdminActorContext actor) =>
        (await _scopeAuthorization.GetAllowedStoresAsync(actor.StaffId))
        .Where(x => x.Active)
        .Select(x => x.StoreId)
        .Distinct()
        .ToList();
    private static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static bool VersionMatches(byte[] current, string? provided)
    {
        if (string.IsNullOrWhiteSpace(provided)) return false;
        try { return current.SequenceEqual(Convert.FromBase64String(provided)); }
        catch (FormatException) { return false; }
    }
    private static ServiceResult<T> Failure<T>(string code, string message) => ServiceResult<T>.Failure(message, errorCode: code);
}
