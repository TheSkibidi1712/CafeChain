using CafeChain.Application.Constants;
using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.Procurement;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastructure.Repositories.Admin.Procurement;

/// <summary>
/// Read-only repository for deterministic reorder calculations.
/// Every query is no-tracking and accepts a cancellation token.  Consumption
/// is grouped in SQL; the service only performs the business arithmetic and
/// lineage reconciliation in memory.
/// </summary>
public sealed class ReorderSuggestionRepository : IReorderSuggestionRepository
{
    private readonly AppDbContext _context;

    public ReorderSuggestionRepository(AppDbContext context) => _context = context;

    public Task<ReorderStoreRow?> GetStoreAsync(
        int storeId,
        CancellationToken cancellationToken = default) =>
        _context.Stores.AsNoTracking()
            .Where(x => x.StoreId == storeId)
            .Select(x => new ReorderStoreRow(
                x.StoreId,
                x.Name ?? $"Cửa hàng #{x.StoreId}"))
            .FirstOrDefaultAsync(cancellationToken);

    // Kept for older callers. New calculation code uses the projection below.
    public Task<List<StoreInventory>> GetInventoriesAsync(
        int storeId,
        CancellationToken cancellationToken = default) =>
        _context.StoreInventories.AsNoTracking()
            .Include(x => x.Ingredient)
                .ThenInclude(x => x.BaseUnit)
            .Where(x => x.StoreId == storeId
                && x.IngredientId.HasValue
                && x.Ingredient.Active)
            .OrderBy(x => x.Ingredient.Name)
            .ThenBy(x => x.IngredientId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<int, ReorderUsageRow>> GetUsageAsync(
        int storeId,
        DateTime fromUtc,
        DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var endUtc = toUtc ?? DateTime.UtcNow;
        if (endUtc <= fromUtc)
            return new Dictionary<int, ReorderUsageRow>();

        // This GroupBy/Sum/Count is intentionally server-side.  The
        // half-open interval prevents a transaction at the boundary from
        // being counted in two adjacent windows.
        var rows = await _context.InventoryTransactions.AsNoTracking()
            .Where(x => x.CreatedAt >= fromUtc
                && x.CreatedAt < endUtc
                && x.StoreInventory.StoreId == storeId
                && x.StoreInventory.IngredientId.HasValue
                && (x.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION
                    || x.Type == InventoryTransactionTypeEnum.PRODUCTION_OUT))
            .GroupBy(x => x.StoreInventory.IngredientId!.Value)
            .Select(g => new
            {
                IngredientId = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            x => x.IngredientId,
            x => new ReorderUsageRow(x.Quantity, x.Count));
    }

    public Task<List<IngredientSupplier>> GetOffersAsync(
        int storeId,
        IReadOnlyCollection<int> ingredientIds,
        CancellationToken cancellationToken = default)
    {
        var ids = ingredientIds?.Where(x => x > 0).Distinct().ToArray()
                   ?? Array.Empty<int>();
        if (ids.Length == 0)
            return Task.FromResult(new List<IngredientSupplier>());

        return _context.IngredientSuppliers.AsNoTracking()
            .Include(x => x.Supplier)
                .ThenInclude(x => x.SupplierStores)
            .Include(x => x.PriceHistories)
            .Where(x => ids.Contains(x.IngredientId)
                && x.Active
                && x.Supplier.Active
                && x.Supplier.SupplierStores.Any(ss => ss.StoreId == storeId && ss.Active))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, int>> GetActiveRestockRequestsAsync(
        int storeId,
        CancellationToken cancellationToken = default) =>
        await _context.RestockRequests.AsNoTracking()
            .Where(x => x.StoreId == storeId
                && x.IngredientId.HasValue
                && RestockRequestStatuses.ActiveValues.Contains(x.Status))
            .GroupBy(x => x.IngredientId!.Value)
            .Select(x => new
            {
                IngredientId = x.Key,
                RequestId = x.Min(y => y.RestockRequestId)
            })
            .ToDictionaryAsync(x => x.IngredientId, x => x.RequestId, cancellationToken);

    public async Task<IReadOnlyDictionary<int, decimal>> GetActivePurchaseAdviceQuantitiesAsync(
        int storeId,
        CancellationToken cancellationToken = default)
    {
        var activeStatuses = PurchaseAdviceStatuses.ActiveReservationStatuses.ToArray();
        var rows = await _context.PurchaseAdviceLines.AsNoTracking()
            .Where(x => x.PurchaseAdvice.StoreId == storeId
                && x.IsActiveReservation
                && activeStatuses.Contains(x.PurchaseAdvice.Status))
            .Select(x => new
            {
                x.IngredientId,
                x.RequestedPurchaseBaseQuantity,
                x.AllocatedToPoBaseQuantity,
                x.ClosedBaseQuantity
            })
            .ToListAsync(cancellationToken);

        return rows.GroupBy(x => x.IngredientId)
            .ToDictionary(
                x => x.Key,
                x => x.Sum(y => Math.Max(
                    0m,
                    y.RequestedPurchaseBaseQuantity
                    - y.AllocatedToPoBaseQuantity
                    - y.ClosedBaseQuantity)));
    }

    public Task<bool> IsActiveStaffAtStoreAsync(
        int staffId,
        int storeId,
        CancellationToken cancellationToken = default) =>
        _context.Staffs.AsNoTracking()
            .AnyAsync(
                x => x.StaffId == staffId
                    && x.Active
                    && x.StoreId == storeId,
                cancellationToken);

    public async Task<IReadOnlyList<int>> GetActiveStoreIdsAsync(
        CancellationToken cancellationToken = default) =>
        await _context.Stores.AsNoTracking()
            .Where(x => x.Active)
            .OrderBy(x => x.StoreId)
            .Select(x => x.StoreId)
            .ToListAsync(cancellationToken);

    public async Task<ReorderCalculationData?> GetCalculationDataAsync(
        int storeId,
        IReadOnlyCollection<int>? ingredientIds,
        DateTime analysisFromUtc,
        DateTime analysisToUtc,
        CancellationToken cancellationToken = default)
    {
        if (storeId <= 0 || analysisToUtc <= analysisFromUtc)
            return null;

        var rows = await LoadRowsAsync(
            new[] { storeId },
            ingredientIds,
            analysisFromUtc,
            analysisToUtc,
            cancellationToken);
        return rows.FirstOrDefault();
    }

    public async Task<IReadOnlyList<ReorderCalculationData>> GetCalculationDataForStoresAsync(
        IReadOnlyCollection<int> storeIds,
        DateTime analysisFromUtc,
        DateTime analysisToUtc,
        CancellationToken cancellationToken = default)
    {
        var ids = storeIds?.Where(x => x > 0).Distinct().ToArray()
                   ?? Array.Empty<int>();
        if (ids.Length == 0 || analysisToUtc <= analysisFromUtc)
            return Array.Empty<ReorderCalculationData>();

        return await LoadRowsAsync(
            ids,
            ingredientIds: null,
            analysisFromUtc,
            analysisToUtc,
            cancellationToken);
    }

    private async Task<IReadOnlyList<ReorderCalculationData>> LoadRowsAsync(
        IReadOnlyCollection<int> storeIds,
        IReadOnlyCollection<int>? ingredientIds,
        DateTime analysisFromUtc,
        DateTime analysisToUtc,
        CancellationToken cancellationToken)
    {
        var storeIdArray = storeIds.Distinct().ToArray();
        var requestedIngredientIds = ingredientIds?
            .Where(x => x > 0)
            .Distinct()
            .ToArray();

        var stores = await _context.Stores.AsNoTracking()
            .Where(x => storeIdArray.Contains(x.StoreId))
            .Select(x => new ReorderStoreRow(
                x.StoreId,
                x.Name ?? $"Cửa hàng #{x.StoreId}"))
            .ToListAsync(cancellationToken);
        if (stores.Count == 0)
            return Array.Empty<ReorderCalculationData>();

        var inventoryQuery = _context.StoreInventories.AsNoTracking()
            .Where(x => storeIdArray.Contains(x.StoreId)
                && x.IngredientId.HasValue
                && x.Ingredient.Active);
        if (requestedIngredientIds is { Length: > 0 })
            inventoryQuery = inventoryQuery.Where(x =>
                requestedIngredientIds.Contains(x.IngredientId!.Value));

        var inventoryRows = await inventoryQuery
            .Select(x => new
            {
                x.StoreInventoryId,
                x.StoreId,
                IngredientId = x.IngredientId!.Value,
                IngredientCode = x.Ingredient.Code ?? string.Empty,
                IngredientName = x.Ingredient.Name ?? string.Empty,
                BaseUnitId = x.Ingredient.BaseUnitId,
                BaseUnitCode = x.Ingredient.BaseUnit == null
                    ? string.Empty
                    : x.Ingredient.BaseUnit.UnitCode,
                OnHandQuantity = x.AvailableQty,
                ReservedQuantity = x.ReservedQty,
                MinimumStock = x.MinStockLevel
            })
            .OrderBy(x => x.StoreId)
            .ThenBy(x => x.IngredientName)
            .ThenBy(x => x.IngredientId)
            .ToListAsync(cancellationToken);

        var inventoryIds = inventoryRows
            .Select(x => x.IngredientId)
            .Distinct()
            .ToArray();
        if (inventoryIds.Length == 0)
        {
            return stores
                .Select(store => new ReorderCalculationData(
                    store,
                    Array.Empty<ReorderInventoryRow>(),
                    new Dictionary<int, ReorderUsageRow>(),
                    Array.Empty<ReorderOfferRow>(),
                    Array.Empty<ReorderRestockRow>(),
                    Array.Empty<ReorderPurchaseAdviceRow>(),
                    Array.Empty<ReorderPurchaseOrderRow>(),
                    Array.Empty<ReorderAllocationRow>()))
                .ToList();
        }

        var usageQuery = _context.InventoryTransactions.AsNoTracking()
            .Where(x => storeIdArray.Contains(x.StoreInventory.StoreId)
                && x.StoreInventory.IngredientId.HasValue
                && inventoryIds.Contains(x.StoreInventory.IngredientId.Value)
                && x.CreatedAt >= analysisFromUtc
                && x.CreatedAt < analysisToUtc
                && (x.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION
                    || x.Type == InventoryTransactionTypeEnum.PRODUCTION_OUT));
        List<UsageAggregate> usageRows;
        if (_context.Database.ProviderName?.Contains(
                "Sqlite",
                StringComparison.OrdinalIgnoreCase) == true)
        {
            // SQLite cannot translate SUM(decimal). This compatibility path is
            // test-only; SQL Server keeps the aggregation on the database.
            var rawUsage = await usageQuery
                .Select(x => new
                {
                    x.StoreInventory.StoreId,
                    IngredientId = x.StoreInventory.IngredientId!.Value,
                    x.Quantity
                })
                .ToListAsync(cancellationToken);
            usageRows = rawUsage
                .GroupBy(x => new { x.StoreId, x.IngredientId })
                .Select(g => new UsageAggregate(
                    g.Key.StoreId,
                    g.Key.IngredientId,
                    g.Sum(x => x.Quantity),
                    g.Count()))
                .ToList();
        }
        else
        {
            usageRows = await usageQuery
                .GroupBy(x => new
                {
                    x.StoreInventory.StoreId,
                    IngredientId = x.StoreInventory.IngredientId!.Value
                })
                .Select(g => new UsageAggregate(
                    g.Key.StoreId,
                    g.Key.IngredientId,
                    g.Sum(x => x.Quantity),
                    g.Count()))
                .ToListAsync(cancellationToken);
        }

        var offerEntities = await _context.IngredientSuppliers.AsNoTracking()
            .Include(x => x.Supplier)
                .ThenInclude(x => x.SupplierStores)
            .Include(x => x.PriceHistories)
            .Where(x => inventoryIds.Contains(x.IngredientId)
                && x.Active
                && x.Supplier.Active
                && x.Supplier.SupplierStores.Any(ss =>
                    storeIdArray.Contains(ss.StoreId) && ss.Active))
            .ToListAsync(cancellationToken);

        var restockEntities = await _context.RestockRequests.AsNoTracking()
            .Include(x => x.FulfillmentPostings)
            .Where(x => storeIdArray.Contains(x.StoreId)
                && x.IngredientId.HasValue
                && inventoryIds.Contains(x.IngredientId.Value)
                && RestockRequestStatuses.ActiveValues.Contains(x.Status))
            .ToListAsync(cancellationToken);

        var adviceRows = await _context.PurchaseAdviceLines.AsNoTracking()
            .Where(x => storeIdArray.Contains(x.PurchaseAdvice.StoreId)
                && inventoryIds.Contains(x.IngredientId))
            .Select(x => new ReorderPurchaseAdviceRow(
                x.PurchaseAdviceLineId,
                x.PurchaseAdviceId,
                x.RestockRequestId,
                x.PurchaseAdvice.StoreId,
                x.IngredientId,
                x.PurchaseAdvice.Status,
                x.IsActiveReservation,
                x.RequestedPurchaseBaseQuantity,
                x.AllocatedToPoBaseQuantity,
                x.AcceptedBaseQuantity,
                x.ClosedBaseQuantity))
            .ToListAsync(cancellationToken);

        var poRowsRaw = await _context.PurchaseOrderLines.AsNoTracking()
            .Where(x => storeIdArray.Contains(x.PurchaseOrder.StoreId)
                && inventoryIds.Contains(x.IngredientId))
            .Select(x => new
            {
                x.PurchaseOrderLineId,
                x.PurchaseOrderId,
                StoreId = x.PurchaseOrder.StoreId,
                x.IngredientId,
                Status = x.PurchaseOrder.Status,
                x.RestockRequestId,
                x.PurchaseAdviceLineId,
                x.OrderedBaseQuantity,
                x.ClosedRemainingQuantity
            })
            .ToListAsync(cancellationToken);
        var poLineIds = poRowsRaw.Select(x => x.PurchaseOrderLineId).ToArray();
        var receiptRows = poLineIds.Length == 0
            ? new List<(int LineId, decimal Accepted)>()
            : (await _context.PurchaseOrderReceiptPostings.AsNoTracking()
                .Where(x => poLineIds.Contains(x.PurchaseOrderLineId))
                .GroupBy(x => x.PurchaseOrderLineId)
                .Select(g => new { LineId = g.Key, Accepted = g.Sum(x => x.AcceptedBaseQuantity) })
                .ToListAsync(cancellationToken))
                .Select(x => (x.LineId, x.Accepted))
                .ToList();
        var acceptedByPoLine = receiptRows.ToDictionary(x => x.LineId, x => x.Accepted);
        var poRows = poRowsRaw
            .Select(x => new ReorderPurchaseOrderRow(
                x.PurchaseOrderLineId,
                x.PurchaseOrderId,
                x.StoreId,
                x.IngredientId,
                x.Status,
                x.RestockRequestId,
                x.PurchaseAdviceLineId,
                x.OrderedBaseQuantity,
                acceptedByPoLine.GetValueOrDefault(x.PurchaseOrderLineId),
                x.ClosedRemainingQuantity))
            .ToList();

        var allocationRowsRaw = await _context.PurchaseOrderLineAllocations.AsNoTracking()
            .Where(x => poLineIds.Contains(x.PurchaseOrderLineId)
                && inventoryIds.Contains(x.PurchaseAdviceLine.IngredientId))
            .Select(x => new
            {
                x.PurchaseOrderLineAllocationId,
                x.PurchaseAdviceLineId,
                x.PurchaseOrderLineId,
                RestockRequestId = x.PurchaseAdviceLine.RestockRequestId,
                x.AllocatedBaseQuantity
            })
            .ToListAsync(cancellationToken);
        var allocationIds = allocationRowsRaw
            .Select(x => x.PurchaseOrderLineAllocationId)
            .ToArray();
        var fulfillmentRows = allocationIds.Length == 0
            ? new List<(int AllocationId, string Type, decimal Quantity)>()
            : (await _context.PurchaseAdviceFulfillmentPostings.AsNoTracking()
                .Where(x => allocationIds.Contains(x.PurchaseOrderLineAllocationId))
                .Select(x => new
                {
                    AllocationId = x.PurchaseOrderLineAllocationId,
                    x.PostingType,
                    x.Quantity
                })
                .ToListAsync(cancellationToken))
                .Select(x => (
                    AllocationId: x.AllocationId,
                    Type: x.PostingType,
                    Quantity: x.Quantity))
                .ToList();
        var acceptedByAllocation = fulfillmentRows
            .Where(x => string.Equals(
                x.Type,
                PurchaseAdviceFulfillmentPostingTypes.Accepted,
                StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.AllocationId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
        var closedByAllocation = fulfillmentRows
            .Where(x => string.Equals(
                x.Type,
                PurchaseAdviceFulfillmentPostingTypes.Closed,
                StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.AllocationId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
        var allocations = allocationRowsRaw
            .Select(x => new ReorderAllocationRow(
                x.PurchaseOrderLineAllocationId,
                x.PurchaseAdviceLineId,
                x.PurchaseOrderLineId,
                x.RestockRequestId,
                x.AllocatedBaseQuantity,
                acceptedByAllocation.GetValueOrDefault(x.PurchaseOrderLineAllocationId),
                closedByAllocation.GetValueOrDefault(x.PurchaseOrderLineAllocationId)))
            .ToList();

        var inventories = inventoryRows
            .GroupBy(x => x.StoreId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ReorderInventoryRow>)g
                    .Select(x => new ReorderInventoryRow(
                        x.StoreInventoryId,
                        x.StoreId,
                        x.IngredientId,
                        x.IngredientCode,
                        x.IngredientName,
                        x.BaseUnitId,
                        x.BaseUnitCode,
                        x.OnHandQuantity,
                        x.ReservedQuantity,
                        x.MinimumStock))
                    .ToList());
        var usage = usageRows
            .GroupBy(x => x.StoreId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<int, ReorderUsageRow>)g.ToDictionary(
                    x => x.IngredientId,
                    x => new ReorderUsageRow(x.Quantity, x.Count)));
        var offers = offerEntities
            .SelectMany(offer => storeIdArray
                .Where(storeId => offer.Supplier.SupplierStores.Any(ss =>
                    ss.StoreId == storeId && ss.Active))
                .Select(storeId =>
                {
                    var history = offer.PriceHistories
                        .Where(x => x.IsCurrent && x.EffectiveDate <= analysisToUtc)
                        .OrderByDescending(x => x.EffectiveDate)
                        .FirstOrDefault();
                    return (StoreId: storeId, Value: new ReorderOfferRow(
                        offer,
                        history?.EffectiveDate,
                        history?.Price,
                        history?.PackageQuantity,
                        history?.PackageUnitId,
                        history != null));
                }))
            .GroupBy(x => x.StoreId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ReorderOfferRow>)g.Select(x => x.Value).ToList());
        var restocks = restockEntities
            .GroupBy(x => x.StoreId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ReorderRestockRow>)g
                    .Select(x => new ReorderRestockRow(
                        x.RestockRequestId,
                        x.StoreId,
                        x.IngredientId!.Value,
                        x.Status,
                        x.RequestedQuantity,
                        x.ClosedRemainingQuantity,
                        x.FulfillmentPostings.Sum(p => p.Quantity)))
                    .ToList());
        var advice = adviceRows
            .GroupBy(x => x.StoreId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ReorderPurchaseAdviceRow>)g.ToList());
        var purchaseOrders = poRows
            .GroupBy(x => x.StoreId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ReorderPurchaseOrderRow>)g.ToList());
        var allocationByStore = allocations
            .Join(
                poRows,
                allocation => allocation.PurchaseOrderLineId,
                line => line.PurchaseOrderLineId,
                (allocation, line) => (line.StoreId, allocation))
            .GroupBy(x => x.StoreId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ReorderAllocationRow>)g.Select(x => x.allocation).ToList());

        return stores
            .Select(store => new ReorderCalculationData(
                store,
                inventories.GetValueOrDefault(store.StoreId)
                    ?? Array.Empty<ReorderInventoryRow>(),
                usage.GetValueOrDefault(store.StoreId)
                    ?? new Dictionary<int, ReorderUsageRow>(),
                offers.GetValueOrDefault(store.StoreId)
                    ?? Array.Empty<ReorderOfferRow>(),
                restocks.GetValueOrDefault(store.StoreId)
                    ?? Array.Empty<ReorderRestockRow>(),
                advice.GetValueOrDefault(store.StoreId)
                    ?? Array.Empty<ReorderPurchaseAdviceRow>(),
                purchaseOrders.GetValueOrDefault(store.StoreId)
                    ?? Array.Empty<ReorderPurchaseOrderRow>(),
                allocationByStore.GetValueOrDefault(store.StoreId)
                    ?? Array.Empty<ReorderAllocationRow>()))
                .ToList();
    }

    private sealed record UsageAggregate(
        int StoreId,
        int IngredientId,
        decimal Quantity,
        int Count);
}
