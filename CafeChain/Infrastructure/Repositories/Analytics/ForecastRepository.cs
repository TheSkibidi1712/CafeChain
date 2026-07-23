using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.AI;
using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Analytics;
using CafeChain.Models.Analytics;
using CafeChain.Models.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastructure.Repositories.Analytics;

public sealed class ForecastRepository : IForecastRepository
{
    private readonly AppDbContext _context;
    public ForecastRepository(AppDbContext context) => _context = context;

    public Task<List<int>> GetActiveStoreIdsAsync(CancellationToken ct) =>
        _context.Stores.AsNoTracking().Where(x => x.Active).OrderBy(x => x.StoreId).Select(x => x.StoreId).ToListAsync(ct);

    public Task<List<int>> GetProductIdsAsync(int storeId, DateTime from, DateTime toExclusive, CancellationToken ct) =>
        EligibleOrders(storeId, from, toExclusive).SelectMany(x => x.OrderDetails).Select(x => x.DrinkId).Distinct().ToListAsync(ct);

    public async Task<List<ForecastSeriesPointDto>> GetRevenueSeriesAsync(int storeId, DateTime from, DateTime toExclusive, CancellationToken ct)
    {
        var rows = await EligibleOrders(storeId, from, toExclusive)
            .GroupBy(x => x.CreatedAt.Date).Select(x => new { Date = x.Key, Value = x.Sum(y => y.Total) })
            .OrderBy(x => x.Date).ToListAsync(ct);
        return Fill(rows.Select(x => new ForecastSeriesPointDto(x.Date, x.Value)), from, toExclusive);
    }

    public async Task<List<ForecastSeriesPointDto>> GetProductSeriesAsync(int storeId, int drinkId, DateTime from, DateTime toExclusive, CancellationToken ct)
    {
        var rows = await EligibleOrders(storeId, from, toExclusive)
            .SelectMany(x => x.OrderDetails).Where(x => x.DrinkId == drinkId)
            .GroupBy(x => x.Order.CreatedAt.Date).Select(x => new { Date = x.Key, Value = (decimal)x.Sum(y => y.Quantity) })
            .OrderBy(x => x.Date).ToListAsync(ct);
        return Fill(rows.Select(x => new ForecastSeriesPointDto(x.Date, x.Value)), from, toExclusive);
    }

    public Task<ForecastRun?> GetExistingAsync(string type, int storeId, int? entityId, DateTime cutoff, int horizon, string version, CancellationToken ct) =>
        _context.ForecastRuns.Include(x => x.Points).FirstOrDefaultAsync(x => x.SeriesType == type && x.StoreId == storeId
            && x.EntityId == entityId && x.TrainingToExclusive == cutoff && x.HorizonDays == horizon && x.ModelVersion == version, ct);

    public Task<ForecastRun?> GetLatestAsync(string type, int storeId, int? entityId, int horizon, CancellationToken ct) =>
        _context.ForecastRuns.AsNoTracking().Include(x => x.Points)
            .Where(x => x.SeriesType == type && x.StoreId == storeId && x.EntityId == entityId && x.HorizonDays == horizon)
            .OrderByDescending(x => x.TrainingToExclusive).ThenByDescending(x => x.ForecastRunId).FirstOrDefaultAsync(ct);

    public void Add(ForecastRun run) => _context.ForecastRuns.Add(run);
    public Task<int> SaveChangesAsync(CancellationToken ct) => _context.SaveChangesAsync(ct);

    private IQueryable<CafeChain.Models.Orders.Order> EligibleOrders(int storeId, DateTime from, DateTime toExclusive) =>
        _context.Orders.AsNoTracking().Where(x => x.StoreId == storeId && x.OrderStatusId == SystemConstants.OrderStatuses.Completed
            && x.CreatedAt >= from && x.CreatedAt < toExclusive
            && !_context.OrderRefunds.Any(r => r.OrderId == x.OrderId && r.Status == OrderRefundStatus.Completed));

    private static List<ForecastSeriesPointDto> Fill(IEnumerable<ForecastSeriesPointDto> source, DateTime from, DateTime toExclusive)
    {
        var values = source.ToDictionary(x => x.Date.Date, x => x.Value);
        var result = new List<ForecastSeriesPointDto>();
        for (var date = from.Date; date < toExclusive.Date; date = date.AddDays(1))
            result.Add(new ForecastSeriesPointDto(date, values.GetValueOrDefault(date)));
        return result;
    }
}
