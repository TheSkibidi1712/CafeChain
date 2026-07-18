using System.Data;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Infrastrusture.Repositories;
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

    public PurchaseOrderBatchService(
        AppDbContext context,
        IPurchaseAdviceConsolidationService consolidation,
        IScopeAuthorizationService scopeAuthorization)
    {
        _context = context;
        _consolidation = consolidation;
        _scopeAuthorization = scopeAuthorization;
    }

    public async Task<ServiceResult<PurchaseOrderBatchDetailDto>> CreateAsync(
        CreatePurchaseOrderBatchRequest request,
        AdminActorContext actor)
    {
        if (!CanCreate(actor)) return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.Forbidden, "Chỉ Kế toán/kho được tạo batch đơn mua.");
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

            var preview = await _consolidation.PreviewAsync(request, actor);
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
                var batchLine = new PurchaseOrderBatchLine
                {
                    IngredientId = group.IngredientId,
                    IngredientSupplierId = group.IngredientSupplierId,
                    PackageUnitId = group.PackageUnitId,
                    PackageQuantitySnapshot = group.PackageQuantity,
                    TotalPackageCount = group.PackageCount,
                    TotalBaseQuantity = group.AllocatedBaseQuantity,
                    PackagePriceSnapshot = group.PackagePriceSnapshot,
                    LineTotal = group.LineTotal,
                    Currency = group.Currency,
                    Note = Clean(group.Specification, 500)
                };
                batch.Lines.Add(batchLine);

                foreach (var allocation in group.Allocations)
                {
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
                            Note = $"Child PO của batch {batch.BatchNumber}",
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
                        RestockRequestId = allocation.RestockRequestId,
                        IngredientId = group.IngredientId,
                        IngredientSupplierId = group.IngredientSupplierId,
                        PackageUnitIdSnapshot = group.PackageUnitId,
                        PackageQuantitySnapshot = group.PackageQuantity,
                        PackagePriceSnapshot = group.PackagePriceSnapshot,
                        PackageCount = allocation.PackageCount,
                        OrderedBaseQuantity = allocation.AllocatedBaseQuantity,
                        PromisedLeadTimeDaysSnapshot = group.LeadTimeDays,
                        Note = $"Allocation từ {allocation.AdviceNumber}"
                    };
                    child.Lines.Add(childLine);
                    batchLine.Allocations.Add(new PurchaseOrderLineAllocation
                    {
                        PurchaseAdviceLineId = allocation.PurchaseAdviceLineId,
                        PurchaseOrder = child,
                        PurchaseOrderLine = childLine,
                        AllocatedBaseQuantity = allocation.AllocatedBaseQuantity,
                        AllocatedPackageQuantity = allocation.PackageCount,
                        CreatedAtUtc = now
                    });

                    var adviceLine = _context.ChangeTracker.Entries<PurchaseAdviceLine>()
                        .Select(x => x.Entity).Single(x => x.PurchaseAdviceLineId == allocation.PurchaseAdviceLineId);
                    adviceLine.AllocatedToPoBaseQuantity += allocation.AllocatedBaseQuantity;
                    var remaining = Math.Max(0m, adviceLine.RequestedPurchaseBaseQuantity - adviceLine.AllocatedToPoBaseQuantity - adviceLine.ClosedBaseQuantity);
                    adviceLine.IsActiveReservation = remaining > 0;
                }
            }

            foreach (var advice in _context.ChangeTracker.Entries<PurchaseAdvice>().Select(x => x.Entity).DistinctBy(x => x.PurchaseAdviceId))
            {
                await _context.Entry(advice).Collection(x => x.Lines).LoadAsync();
                if (advice.Lines.All(x => Math.Max(0m, x.RequestedPurchaseBaseQuantity - x.AllocatedToPoBaseQuantity - x.ClosedBaseQuantity) == 0))
                {
                    var previous = advice.Status;
                    advice.Status = PurchaseAdviceStatuses.Allocated;
                    advice.UpdatedAtUtc = now;
                    advice.Transitions.Add(new PurchaseAdviceTransition
                    {
                        PreviousStatus = previous,
                        NewStatus = PurchaseAdviceStatuses.Allocated,
                        ActorStaffId = actor.StaffId,
                        OccurredAtUtc = now,
                        Reason = $"Đã phân bổ vào batch {batch.BatchNumber}."
                    });
                }
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
        if (dto == null) return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.NotFound, "Không tìm thấy batch đơn mua.");
        if (!await CanReadAsync(actor, dto.ChildPurchaseOrders.Select(x => x.StoreId).ToArray()))
            return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.Forbidden, "Bạn không có quyền xem batch đơn mua này.");
        return ServiceResult<PurchaseOrderBatchDetailDto>.Success(dto);
    }

    public async Task<ServiceResult<IReadOnlyList<PurchaseOrderBatchListItemDto>>> ListAsync(string? status, int? supplierId, AdminActorContext actor)
    {
        var query = _context.PurchaseOrderBatches.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (supplierId.HasValue) query = query.Where(x => x.SupplierId == supplierId.Value);
        if (!HasRole(actor, RoleConstants.AccountantWarehouse) && !HasRole(actor, RoleConstants.BusinessOwner))
        {
            var allowed = await ResolveAllowedStoreIdsAsync(actor);
            if (allowed.Count == 0) return Failure<IReadOnlyList<PurchaseOrderBatchListItemDto>>(PurchaseOrderBatchErrorCodes.Forbidden, "Bạn không có quyền xem batch đơn mua.");
            query = query.Where(x => x.ChildPurchaseOrders.Any(po => allowed.Contains(po.StoreId)));
        }
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
        if (!HasRole(actor, RoleConstants.BusinessOwner)) return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.Forbidden, "Chỉ Chủ doanh nghiệp được duyệt batch.");
        var batch = await _context.PurchaseOrderBatches.Include(x => x.ChildPurchaseOrders).SingleOrDefaultAsync(x => x.PurchaseOrderBatchId == id);
        if (batch == null) return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.NotFound, "Không tìm thấy batch đơn mua.");
        if (batch.Status == PurchaseOrderBatchStatuses.Approved) return await GetDetailAsync(id, actor);
        if (batch.Status != PurchaseOrderBatchStatuses.PendingApproval)
            return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.Invalid, $"Không thể duyệt batch ở trạng thái {batch.Status}.");
        if (!VersionMatches(batch.RowVersion, request.RowVersion)) return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.StaleVersion, "Batch đã thay đổi. Hãy tải lại.");
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
        if (!HasRole(actor, RoleConstants.BusinessOwner)) return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.Forbidden, "Chỉ Chủ doanh nghiệp được hủy batch.");
        if (string.IsNullOrWhiteSpace(request.Reason)) return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.Invalid, "Bắt buộc nhập lý do hủy batch.");
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var batch = await _context.PurchaseOrderBatches
            .Include(x => x.ChildPurchaseOrders).ThenInclude(x => x.Lines).ThenInclude(x => x.ReceiptPostings)
            .Include(x => x.Lines).ThenInclude(x => x.Allocations).ThenInclude(x => x.PurchaseAdviceLine).ThenInclude(x => x.PurchaseAdvice)
            .SingleOrDefaultAsync(x => x.PurchaseOrderBatchId == id);
        if (batch == null) return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.NotFound, "Không tìm thấy batch đơn mua.");
        if (!VersionMatches(batch.RowVersion, request.RowVersion)) return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.StaleVersion, "Batch đã thay đổi. Hãy tải lại.");
        if (batch.ChildPurchaseOrders.SelectMany(x => x.Lines).SelectMany(x => x.ReceiptPostings).Any())
            return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.Invalid, "Không thể hủy batch đã phát sinh nhận hàng.");
        if (batch.Status is PurchaseOrderBatchStatuses.Completed or PurchaseOrderBatchStatuses.Cancelled)
            return Failure<PurchaseOrderBatchDetailDto>(PurchaseOrderBatchErrorCodes.Invalid, $"Không thể hủy batch ở trạng thái {batch.Status}.");

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
        foreach (var allocation in batch.Lines.SelectMany(x => x.Allocations))
        {
            var line = allocation.PurchaseAdviceLine;
            line.AllocatedToPoBaseQuantity = Math.Max(0m, line.AllocatedToPoBaseQuantity - allocation.AllocatedBaseQuantity);
            line.IsActiveReservation = true;
            line.PurchaseAdvice.Status = PurchaseAdviceStatuses.UnderReview;
            line.PurchaseAdvice.UpdatedAtUtc = now;
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
            .Include(x => x.Lines).ThenInclude(x => x.Ingredient)
            .Include(x => x.Lines).ThenInclude(x => x.PackageUnit)
            .Include(x => x.Lines).ThenInclude(x => x.Allocations).ThenInclude(x => x.PurchaseAdviceLine).ThenInclude(x => x.PurchaseAdvice)
            .Include(x => x.Lines).ThenInclude(x => x.Allocations).ThenInclude(x => x.PurchaseOrder).ThenInclude(x => x.Store)
            .Include(x => x.ChildPurchaseOrders).ThenInclude(x => x.Store)
            .Include(x => x.ChildPurchaseOrders).ThenInclude(x => x.Lines).ThenInclude(x => x.ReceiptPostings)
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
                PurchaseOrderBatchLineId = line.PurchaseOrderBatchLineId,
                IngredientId = line.IngredientId,
                IngredientName = line.Ingredient.Name,
                PackageUnitName = line.PackageUnit.Name,
                PackageQuantitySnapshot = line.PackageQuantitySnapshot,
                TotalPackageCount = line.TotalPackageCount,
                TotalBaseQuantity = line.TotalBaseQuantity,
                PackagePriceSnapshot = line.PackagePriceSnapshot,
                LineTotal = line.LineTotal,
                Allocations = line.Allocations.Select(a => new PurchaseOrderBatchAllocationDto
                {
                    PurchaseOrderLineAllocationId = a.PurchaseOrderLineAllocationId,
                    PurchaseAdviceLineId = a.PurchaseAdviceLineId,
                    PurchaseAdviceId = a.PurchaseAdviceLine.PurchaseAdviceId,
                    AdviceNumber = a.PurchaseAdviceLine.PurchaseAdvice.AdviceNumber,
                    PurchaseOrderId = a.PurchaseOrderId,
                    PurchaseOrderLineId = a.PurchaseOrderLineId,
                    StoreId = a.PurchaseOrder.StoreId,
                    StoreName = a.PurchaseOrder.Store.Name,
                    AllocatedBaseQuantity = a.AllocatedBaseQuantity,
                    AllocatedPackageQuantity = a.AllocatedPackageQuantity
                }).ToArray()
            }).ToArray(),
            ChildPurchaseOrders = batch.ChildPurchaseOrders.Select(po =>
            {
                var accepted = po.Lines.SelectMany(x => x.ReceiptPostings).Sum(x => x.AcceptedBaseQuantity);
                var ordered = po.Lines.Sum(x => x.OrderedBaseQuantity);
                var closed = po.Lines.Sum(x => x.ClosedRemainingQuantity);
                return new PurchaseOrderBatchChildDto
                {
                    PurchaseOrderId = po.PurchaseOrderId,
                    Code = po.Code,
                    StoreId = po.StoreId,
                    StoreName = po.Store.Name,
                    Status = po.Status,
                    TotalAmount = po.Lines.Sum(x => x.PackageCount * x.PackagePriceSnapshot),
                    OrderedBaseQuantity = ordered,
                    AcceptedBaseQuantity = accepted,
                    RemainingBaseQuantity = Math.Max(0m, ordered - accepted - closed)
                };
            }).OrderBy(x => x.StoreName).ToArray()
        };
    }

    private async Task<bool> CanReadAsync(AdminActorContext actor, int[] stores)
    {
        if (HasRole(actor, RoleConstants.AccountantWarehouse) || HasRole(actor, RoleConstants.BusinessOwner)) return true;
        var allowed = await ResolveAllowedStoreIdsAsync(actor);
        return stores.Any(allowed.Contains);
    }

    private async Task<List<int>> ResolveAllowedStoreIdsAsync(AdminActorContext actor)
    {
        if ((HasRole(actor, RoleConstants.StoreManager) || HasRole(actor, RoleConstants.ShiftSupervisor)) && actor.StoreId > 0)
            return new() { actor.StoreId };
        if (!HasRole(actor, RoleConstants.AreaManager)) return new();
        var ids = await _context.Stores.AsNoTracking().Where(x => x.Active).Select(x => x.StoreId).ToListAsync();
        var allowed = new List<int>();
        foreach (var id in ids) if (await _scopeAuthorization.CanAccessStoreAsync(actor.StaffId, id)) allowed.Add(id);
        return allowed;
    }

    private static bool CanCreate(AdminActorContext actor) => HasRole(actor, RoleConstants.AccountantWarehouse) || HasRole(actor, RoleConstants.BusinessOwner);
    private static bool HasRole(AdminActorContext actor, string role) => actor.RoleNames.Contains(role, StringComparer.OrdinalIgnoreCase);
    private static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
    private static bool VersionMatches(byte[] current, string? provided)
    {
        if (string.IsNullOrWhiteSpace(provided)) return false;
        try { return current.SequenceEqual(Convert.FromBase64String(provided)); }
        catch (FormatException) { return false; }
    }
    private static ServiceResult<T> Failure<T>(string code, string message) => ServiceResult<T>.Failure(message, errorCode: code);
}
