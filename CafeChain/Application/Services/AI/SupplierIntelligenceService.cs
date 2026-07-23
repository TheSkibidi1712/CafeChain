using CafeChain.Application.DTOs.AI;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Options;
using CafeChain.Infrastructure.Interfaces.Analytics;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.AI;

public sealed class SupplierIntelligenceService : ISupplierIntelligenceService
{
    private readonly ISupplierIntelligenceRepository _repository;
    private readonly ISupplierQualityService _quality;
    private readonly IUnitConversionService _conversion;
    private readonly IScopeAuthorizationService _scope;
    private readonly SupplierIntelligenceOptions _options;

    public SupplierIntelligenceService(ISupplierIntelligenceRepository repository, ISupplierQualityService quality,
        IUnitConversionService conversion, IScopeAuthorizationService scope, IOptions<SupplierIntelligenceOptions> options)
    { _repository = repository; _quality = quality; _conversion = conversion; _scope = scope; _options = options.Value; }

    public async Task<SupplierRecommendationDto> CompareAsync(AdminActorContext actor, int storeId, int ingredientId,
        decimal requiredBaseQuantity, CancellationToken ct = default)
    {
        if (!_options.ScoringEnabled) throw new InvalidOperationException("Supplier Intelligence đang tắt.");
        if (requiredBaseQuantity <= 0) throw new ArgumentException("Số lượng cần mua phải lớn hơn 0.");
        var allowed = await _scope.GetAllowedStoresAsync(actor.StaffId);
        if (!allowed.Any(x => x.StoreId == storeId)) throw new UnauthorizedAccessException("Cửa hàng nằm ngoài phạm vi được cấp.");
        var offers = await _repository.GetOffersAsync(storeId, ingredientId, ct);
        var raw = new List<RawCandidate>();
        foreach (var offer in offers)
        {
            if (!offer.PackageQuantity.HasValue || offer.PackageQuantity <= 0 || offer.CurrentPrice <= 0)
                continue;
            var conversion = await _conversion.ConvertAsync(ingredientId, offer.PackageQuantity.Value, offer.UnitId);
            if (!conversion.IsSuccess || conversion.Data <= 0) continue;
            var packages = Math.Max((int)Math.Ceiling(requiredBaseQuantity / conversion.Data), offer.MinimumOrderPackageCount ?? 1);
            var dashboard = await _quality.GetDashboardAsync(storeId, offer.SupplierId, DateTime.UtcNow.AddDays(-180),
                DateTime.UtcNow, actor.StaffId, actor.RoleNames);
            var performance = dashboard.Data?.Performance;
            raw.Add(new RawCandidate(offer.IngredientSupplierId, offer.SupplierId, offer.Supplier.Name ?? $"NCC #{offer.SupplierId}",
                offer.CurrentPrice / conversion.Data, offer.CurrentPrice, conversion.Data, packages,
                offer.LeadTimeDays ?? 30, performance));
        }
        var result = new SupplierRecommendationDto
        {
            StoreId = storeId, IngredientId = ingredientId, RequiredBaseQuantity = requiredBaseQuantity,
            WeightVersion = _options.WeightVersion, CalculatedAtUtc = DateTime.UtcNow
        };
        if (raw.Count == 0) return result;
        var minPrice = raw.Min(x => x.BasePrice); var maxPrice = raw.Max(x => x.BasePrice);
        var minLead = raw.Min(x => x.Lead); var maxLead = raw.Max(x => x.Lead);
        foreach (var item in raw)
        {
            var p = item.Performance;
            var components = new SupplierScoreComponentDto
            {
                Price = Inverse(item.BasePrice, minPrice, maxPrice),
                LeadTime = Inverse(item.Lead, minLead, maxLead),
                OnTime = p?.OnTimeRate ?? 0, Fill = p?.FillRate ?? 0,
                Quality = p == null ? 0 : Math.Clamp(100 - ((p.RejectionRate + p.IssueRate) / 2), 0, 100)
            };
            var score = (components.Price * _options.PriceWeight + components.OnTime * _options.OnTimeWeight
                + components.Fill * _options.FillWeight + components.Quality * _options.QualityWeight
                + components.LeadTime * _options.LeadTimeWeight) / 100;
            var receipts = p?.ConfirmedReceiptCount ?? 0;
            var confidence = receipts >= _options.HighConfidenceReceipts ? "HIGH"
                : receipts >= _options.MediumConfidenceReceipts ? "MEDIUM" : "INSUFFICIENT_DATA";
            result.Candidates.Add(new SupplierRecommendationCandidateDto
            {
                SupplierId = item.SupplierId, IngredientSupplierId = item.OfferId, SupplierName = item.Name,
                Score = Math.Round(score, 2), Confidence = confidence, PackageCount = item.Packages,
                PackageBaseQuantity = item.PackageBase, EstimatedAmount = item.Packages * item.PackagePrice,
                ComponentScores = components,
                Warnings = confidence == "INSUFFICIENT_DATA" ? ["Chưa đủ 5 phiếu nhận để đánh giá độ tin cậy."] : []
            });
        }
        result.Candidates = result.Candidates.OrderByDescending(x => x.Score).ThenBy(x => x.EstimatedAmount).ToList();
        return result;
    }

    private static decimal Inverse(decimal value, decimal min, decimal max) => max == min ? 100 : Math.Clamp((max - value) / (max - min) * 100, 0, 100);
    private sealed record RawCandidate(int OfferId, int SupplierId, string Name, decimal BasePrice, decimal PackagePrice,
        decimal PackageBase, int Packages, int Lead, CafeChain.Application.DTOs.Admin.Procurement.SupplierPerformanceDto? Performance);
}
