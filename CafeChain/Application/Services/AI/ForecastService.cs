using System.Text.Json;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Options;
using CafeChain.Infrastructure.Interfaces.Analytics;
using CafeChain.Models.Analytics;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.AI;

public sealed class ForecastService : IForecastService
{
    private const string Version = "baseline-v1";
    private readonly IForecastRepository _repository;
    private readonly IScopeAuthorizationService _scope;
    private readonly ForecastingOptions _options;

    public ForecastService(IForecastRepository repository, IScopeAuthorizationService scope, IOptions<ForecastingOptions> options)
    { _repository = repository; _scope = scope; _options = options.Value; }

    public Task<ForecastResultDto> GenerateRevenueAsync(int storeId, int horizonDays, CancellationToken ct = default) =>
        GenerateAsync("STORE_REVENUE", storeId, null, horizonDays, _options.RevenueMinimumDays,
            (from, to) => _repository.GetRevenueSeriesAsync(storeId, from, to, ct), ct);

    public Task<ForecastResultDto> GenerateProductAsync(int storeId, int drinkId, int horizonDays, CancellationToken ct = default) =>
        GenerateAsync("PRODUCT_QUANTITY", storeId, drinkId, horizonDays, _options.ProductMinimumDays,
            (from, to) => _repository.GetProductSeriesAsync(storeId, drinkId, from, to, ct), ct);

    public async Task<ForecastResultDto?> GetLatestAsync(AdminActorContext actor, string seriesType, int storeId, int? entityId, int horizonDays, CancellationToken ct = default)
    {
        await RequireScopeAsync(actor, storeId);
        var run = await _repository.GetLatestAsync(seriesType, storeId, entityId, horizonDays, ct);
        return run == null ? null : Map(run);
    }

    private async Task<ForecastResultDto> GenerateAsync(string type, int storeId, int? entityId, int horizon, int minimumDays,
        Func<DateTime, DateTime, Task<List<ForecastSeriesPointDto>>> load, CancellationToken ct)
    {
        if (horizon is not (7 or 30)) throw new ArgumentException("Forecast chỉ hỗ trợ 7 hoặc 30 ngày.");
        var cutoff = DateTime.UtcNow.Date;
        var existing = await _repository.GetExistingAsync(type, storeId, entityId, cutoff, horizon, Version, ct);
        if (existing != null) return Map(existing);
        var from = cutoff.AddDays(-Math.Clamp(_options.AnalysisWindowDays, minimumDays, 730));
        var series = await load(from, cutoff);
        var warnings = new List<string>();
        string quality;
        ForecastModelRunner.ModelResult? selected = null;
        if (series.Count < minimumDays)
        {
            quality = "INSUFFICIENT_HISTORY"; warnings.Add($"Cần tối thiểu {minimumDays} ngày dữ liệu.");
        }
        else if (type == "PRODUCT_QUANTITY" && series.Count(x => x.Value > 0) / (decimal)series.Count < _options.ProductMinimumActiveDayRatio)
        {
            quality = "SPARSE_SERIES"; warnings.Add("Chuỗi sản phẩm có quá ít ngày phát sinh bán hàng.");
        }
        else
        {
            selected = ForecastModelRunner.Select(series, horizon);
            quality = "ACCEPTABLE";
        }

        var run = new ForecastRun
        {
            SeriesType = type, StoreId = storeId, EntityId = entityId,
            TrainingFrom = from, TrainingToExclusive = cutoff, HorizonDays = horizon,
            ModelType = selected?.Name ?? "NONE", ModelVersion = Version,
            SampleCount = series.Count, Mae = selected?.Mae, Wape = selected?.Wape,
            QualityStatus = quality, WarningJson = JsonSerializer.Serialize(warnings),
            CreatedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddDays(Math.Clamp(_options.ResultTtlDays, 1, 30)),
            InputDataVersion = $"orders-to-{cutoff:yyyyMMdd}"
        };
        if (selected != null)
        {
            var sorted = selected.Residuals.OrderBy(x => x).ToArray();
            var lowError = Quantile(sorted, .10m); var highError = Quantile(sorted, .90m);
            run.Points = selected.Forecast.Select((value, index) => new ForecastPoint
            {
                ForecastDate = cutoff.AddDays(index), PointForecast = value,
                LowerBound = Math.Max(0, value + lowError), UpperBound = Math.Max(0, value + highError)
            }).ToList();
        }
        _repository.Add(run); await _repository.SaveChangesAsync(ct);
        return Map(run);
    }

    private async Task RequireScopeAsync(AdminActorContext actor, int storeId)
    {
        if (actor.StaffId <= 0) throw new UnauthorizedAccessException("Staff context is required.");
        var allowed = await _scope.GetAllowedStoresAsync(actor.StaffId);
        if (!allowed.Any(x => x.StoreId == storeId)) throw new UnauthorizedAccessException("Cửa hàng nằm ngoài phạm vi được cấp.");
    }

    private static decimal Quantile(decimal[] values, decimal probability)
    {
        if (values.Length == 0) return 0;
        var index = (int)Math.Round((values.Length - 1) * probability, MidpointRounding.AwayFromZero);
        return values[Math.Clamp(index, 0, values.Length - 1)];
    }

    private static ForecastResultDto Map(ForecastRun run) => new()
    {
        ForecastRunId = run.ForecastRunId, SeriesType = run.SeriesType, StoreId = run.StoreId, EntityId = run.EntityId,
        TrainingFrom = run.TrainingFrom, TrainingToExclusive = run.TrainingToExclusive,
        HorizonDays = run.HorizonDays, ModelType = run.ModelType, ModelVersion = run.ModelVersion,
        SampleCount = run.SampleCount, Mae = run.Mae, Wape = run.Wape, QualityStatus = run.QualityStatus,
        Warnings = JsonSerializer.Deserialize<List<string>>(run.WarningJson) ?? [],
        Points = run.Points.OrderBy(x => x.ForecastDate).Select(x => new ForecastPointDto(x.ForecastDate, x.PointForecast, x.LowerBound, x.UpperBound)).ToList()
    };
}
