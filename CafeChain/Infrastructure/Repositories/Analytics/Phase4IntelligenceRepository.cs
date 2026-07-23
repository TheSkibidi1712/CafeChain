using CafeChain.Application.Constants;
using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Analytics;
using CafeChain.Models.Analytics;
using CafeChain.Models.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastructure.Repositories.Analytics;

public sealed class PosRecommendationRepository : IPosRecommendationRepository
{
    private readonly AppDbContext _db;
    public PosRecommendationRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<int>> GetActiveStoreIdsAsync(CancellationToken ct = default) =>
        await _db.Stores.AsNoTracking().Where(x => x.Active).Select(x => x.StoreId).ToListAsync(ct);

    public async Task<IReadOnlyList<BasketFact>> GetBasketsAsync(int storeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var rows = await _db.Orders.AsNoTracking()
            .Where(x => x.StoreId == storeId && x.OrderStatusId == SystemConstants.OrderStatuses.Completed
                && x.CreatedAt >= fromUtc && x.CreatedAt < toUtc
                && !_db.OrderRefunds.Any(r => r.OrderId == x.OrderId && r.Status == OrderRefundStatus.Completed))
            .Select(x => new { x.OrderId, DrinkIds = x.OrderDetails.Select(d => d.DrinkId).Distinct().ToList() })
            .ToListAsync(ct);
        return rows.Where(x => x.DrinkIds.Count > 1).Select(x => new BasketFact(x.OrderId, x.DrinkIds)).ToList();
    }

    public async Task<IReadOnlyDictionary<int, RecommendationCandidateData>> GetCandidatesAsync(int storeId, IReadOnlyCollection<int> drinkIds, DateTime asOfUtc, CancellationToken ct = default)
    {
        var ids = drinkIds.Distinct().ToArray();
        var rows = await _db.Drinks.AsNoTracking().Where(d => ids.Contains(d.DrinkId) && d.Active)
            .Select(d => new
            {
                d.DrinkId, d.Name, Image = d.DrinkImages.OrderByDescending(i => i.IsDefault).Select(i => i.ImageUrl).FirstOrDefault(),
                d.CalculatedCogs,
                Items = d.DrinkSizes.SelectMany(s => s.StoreMenuItems.Where(m => m.StoreId == storeId && m.IsEnabled && m.PublishedAtUtc != null
                    && (!m.EffectiveFromUtc.HasValue || m.EffectiveFromUtc <= asOfUtc)
                    && (!m.EffectiveToUtc.HasValue || m.EffectiveToUtc > asOfUtc)))
                    .Select(m => new { Price = m.PriceOverride ?? m.DrinkSize.Price, m.DrinkSizeId }).ToList()
            }).ToListAsync(ct);
        return rows.Where(x => x.Items.Count > 0).ToDictionary(x => x.DrinkId, x =>
        {
            var price = x.Items.Min(i => i.Price); var cogs = x.CalculatedCogs ?? 0m;
            return new RecommendationCandidateData(x.DrinkId, x.Name, x.Image, price, price - cogs,
                price - cogs >= 0, x.Items.Select(i => i.DrinkSizeId).Distinct().ToList());
        });
    }

    public async Task ReplaceCatalogAsync(int storeId, string modelVersion, IReadOnlyCollection<PosRecommendationCatalog> rows, CancellationToken ct = default)
    {
        var old = await _db.PosRecommendationCatalog.Where(x => x.StoreId == storeId && x.ModelVersion == modelVersion).ToListAsync(ct);
        _db.PosRecommendationCatalog.RemoveRange(old); await _db.PosRecommendationCatalog.AddRangeAsync(rows, ct); await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PosRecommendationCatalog>> GetCatalogAsync(int storeId, IReadOnlyCollection<int> triggerDrinkIds, DateTime asOfUtc, int take, CancellationToken ct = default) =>
        await _db.PosRecommendationCatalog.AsNoTracking().Include(x => x.RecommendedDrink).ThenInclude(x => x.DrinkImages)
            .Where(x => x.StoreId == storeId && triggerDrinkIds.Contains(x.TriggerDrinkId) && x.ExpiresAtUtc > asOfUtc)
            .OrderBy(x => x.Rank).ThenByDescending(x => x.Lift).ThenBy(x => x.RecommendedDrinkId).Take(take).ToListAsync(ct);

    public Task<PosRecommendationExposure?> GetExposureAsync(Guid sessionId, CancellationToken ct = default) =>
        _db.PosRecommendationExposures.Include(x => x.Items).FirstOrDefaultAsync(x => x.RecommendationSessionId == sessionId, ct);
    public async Task ReconcileConversionsAsync(int storeId, CancellationToken ct = default)
    {
        var pending = await _db.PosRecommendationExposures.Include(x => x.Items)
            .Where(x => x.StoreId == storeId && !x.OrderId.HasValue).ToListAsync(ct);
        if (pending.Count == 0) return;
        var sessions = pending.Select(x => x.RecommendationSessionId).ToArray();
        var orders = await _db.Orders.AsNoTracking().Where(x => x.StoreId == storeId && x.RecommendationSessionId.HasValue
                && sessions.Contains(x.RecommendationSessionId.Value) && x.OrderStatusId == SystemConstants.OrderStatuses.Completed)
            .Select(x => new { x.OrderId, SessionId = x.RecommendationSessionId!.Value, DrinkIds = x.OrderDetails.Select(d => d.DrinkId).Distinct().ToList() }).ToListAsync(ct);
        foreach (var order in orders)
        {
            var exposure = pending.First(x => x.RecommendationSessionId == order.SessionId);
            exposure.OrderId = order.OrderId; exposure.ConvertedAtUtc = DateTime.UtcNow;
            foreach (var item in exposure.Items) item.WasPurchased = order.DrinkIds.Contains(item.RecommendedDrinkId);
        }
        await _db.SaveChangesAsync(ct);
    }
    public Task AddExposureAsync(PosRecommendationExposure exposure, CancellationToken ct = default) => _db.PosRecommendationExposures.AddAsync(exposure, ct).AsTask();
    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public sealed class AnomalyDetectionRepository : IAnomalyDetectionRepository
{
    private readonly AppDbContext _db;
    public AnomalyDetectionRepository(AppDbContext db) => _db = db;
    public async Task<IReadOnlyList<int>> GetActiveStoreIdsAsync(CancellationToken ct = default) => await _db.Stores.AsNoTracking().Where(x => x.Active).Select(x => x.StoreId).ToListAsync(ct);
    public async Task<IReadOnlyList<DailyMetricPoint>> GetDailyRevenueAsync(int storeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await _db.Orders.AsNoTracking().Where(x => x.StoreId == storeId && x.OrderStatusId == SystemConstants.OrderStatuses.Completed && x.CreatedAt >= fromUtc && x.CreatedAt < toUtc
                && !_db.OrderRefunds.Any(r => r.OrderId == x.OrderId && r.Status == OrderRefundStatus.Completed))
            .GroupBy(x => x.CreatedAt.Date).Select(g => new DailyMetricPoint(g.Key, g.Sum(x => x.Total))).OrderBy(x => x.Date).ToListAsync(ct);
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<DailyMetricPoint>>> GetDailyOperationalMetricsAsync(int storeId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var result = new Dictionary<string, IReadOnlyList<DailyMetricPoint>>(StringComparer.Ordinal);
        result["REVENUE"] = await GetDailyRevenueAsync(storeId, fromUtc, toUtc, ct);
        result["ORDER_COUNT"] = await _db.Orders.AsNoTracking().Where(x => x.StoreId == storeId && x.OrderStatusId == SystemConstants.OrderStatuses.Completed && x.CreatedAt >= fromUtc && x.CreatedAt < toUtc
                && !_db.OrderRefunds.Any(r => r.OrderId == x.OrderId && r.Status == OrderRefundStatus.Completed))
            .GroupBy(x => x.CreatedAt.Date).Select(g => new DailyMetricPoint(g.Key, g.Count())).OrderBy(x => x.Date).ToListAsync(ct);
        result["WASTE_ADJUSTMENT"] = await _db.InventoryTransactions.AsNoTracking().Where(x => x.StoreInventory.StoreId == storeId && x.CreatedAt >= fromUtc && x.CreatedAt < toUtc
                && (x.Type == InventoryTransactionTypeEnum.WASTE || x.Type == InventoryTransactionTypeEnum.ADJUSTMENT_IN || x.Type == InventoryTransactionTypeEnum.ADJUSTMENT_OUT))
            .GroupBy(x => x.CreatedAt.Date).Select(g => new DailyMetricPoint(g.Key, g.Sum(x => Math.Abs(x.Quantity)))).OrderBy(x => x.Date).ToListAsync(ct);
        result["CASH_DISCREPANCY"] = await _db.WorkShifts.AsNoTracking().Where(x => x.StoreId == storeId && x.EndTime.HasValue && x.EndTime >= fromUtc && x.EndTime < toUtc && x.CashDiscrepancy.HasValue)
            .GroupBy(x => x.EndTime!.Value.Date).Select(g => new DailyMetricPoint(g.Key, g.Sum(x => Math.Abs(x.CashDiscrepancy!.Value)))).OrderBy(x => x.Date).ToListAsync(ct);
        result["SUPPLIER_ISSUE"] = await _db.SupplierReceiptIssues.AsNoTracking().Where(x => x.StoreId == storeId && x.ReportedAtUtc >= fromUtc && x.ReportedAtUtc < toUtc)
            .GroupBy(x => x.ReportedAtUtc.Date).Select(g => new DailyMetricPoint(g.Key, g.Count())).OrderBy(x => x.Date).ToListAsync(ct);

        var productRows = await _db.OrderDetails.AsNoTracking().Where(x => x.Order.StoreId == storeId && x.Order.OrderStatusId == SystemConstants.OrderStatuses.Completed
                && x.Order.CreatedAt >= fromUtc && x.Order.CreatedAt < toUtc
                && !_db.OrderRefunds.Any(r => r.OrderId == x.OrderId && r.Status == OrderRefundStatus.Completed))
            .GroupBy(x => new { x.DrinkId, Date = x.Order.CreatedAt.Date }).Select(g => new { g.Key.DrinkId, g.Key.Date, Quantity = g.Sum(x => x.Quantity) }).ToListAsync(ct);
        var productIds = productRows.GroupBy(x => x.DrinkId).Where(g => g.Count() >= 30).Select(g => g.Key).ToArray();
        var stockRiskDrinkIds = await _db.Recipes.AsNoTracking().Where(r => r.DrinkId.HasValue && productIds.Contains(r.DrinkId.Value)
                && _db.StockAlerts.Any(a => a.StoreId == storeId && (a.Status == "OPEN" || a.Status == "CONFIRMED")
                    && (a.RecipeId == r.RecipeId || (a.IngredientId.HasValue && r.RecipeDetails.Any(d => d.IngredientId == a.IngredientId)))))
            .Select(r => r.DrinkId!.Value).Distinct().ToListAsync(ct);
        foreach (var group in productRows.Where(x => productIds.Contains(x.DrinkId) && !stockRiskDrinkIds.Contains(x.DrinkId)).GroupBy(x => x.DrinkId))
            result[$"PRODUCT_VOLUME:{group.Key}"] = group.OrderBy(x => x.Date).Select(x => new DailyMetricPoint(x.Date, x.Quantity)).ToList();
        return result;
    }
    public Task<OperationalAnomaly?> GetByKeyAsync(int storeId, string metricCode, string periodKey, CancellationToken ct = default) => _db.OperationalAnomalies.FirstOrDefaultAsync(x => x.StoreId == storeId && x.MetricCode == metricCode && x.PeriodKey == periodKey, ct);
    public Task AddAsync(OperationalAnomaly anomaly, CancellationToken ct = default) => _db.OperationalAnomalies.AddAsync(anomaly, ct).AsTask();
    public async Task<IReadOnlyList<OperationalAnomaly>> GetOpenAsync(int storeId, CancellationToken ct = default) => await _db.OperationalAnomalies.AsNoTracking().Where(x => x.StoreId == storeId && x.Status != "RESOLVED").OrderByDescending(x => x.Severity).ThenByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(ct);
    public Task<OperationalAnomaly?> GetByIdAsync(int id, CancellationToken ct = default) => _db.OperationalAnomalies.FirstOrDefaultAsync(x => x.OperationalAnomalyId == id, ct);
    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
