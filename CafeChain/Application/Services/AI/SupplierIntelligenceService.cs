using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Options;
using CafeChain.Infrastructure.Interfaces.Analytics;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Analytics;
using CafeChain.Data;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CafeChain.Application.Services.AI;

public sealed class SupplierIntelligenceService : ISupplierIntelligenceService
{
    private readonly ISupplierIntelligenceRepository _repository;
    private readonly ISupplierQualityService _quality;
    private readonly IUnitConversionService _conversion;
    private readonly IScopeAuthorizationService _scope;
    private readonly IAdminPermissionService _permissions;
    private readonly SupplierIntelligenceOptions _options;
    private readonly ISupplierIntelligenceFeatureGate _featureGate;
    private readonly AppDbContext? _context;

    public SupplierIntelligenceService(
        ISupplierIntelligenceRepository repository,
        ISupplierQualityService quality,
        IUnitConversionService conversion,
        IScopeAuthorizationService scope,
        IAdminPermissionService permissions,
        IOptions<SupplierIntelligenceOptions> options,
        ISupplierIntelligenceFeatureGate featureGate,
        AppDbContext? context = null)
    {
        _repository = repository;
        _quality = quality;
        _conversion = conversion;
        _scope = scope;
        _permissions = permissions;
        _options = options.Value;
        _featureGate = featureGate;
        _context = context;
    }

    public async Task<SupplierRecommendationDto> CompareAsync(
        AdminActorContext actor, int storeId, int ingredientId,
        decimal requiredBaseQuantity, CancellationToken ct = default)
    {
        var feature = await _featureGate.GetStateAsync(ct);
        if (!feature.IsEnabledForStore(storeId))
            throw new InvalidOperationException("Supplier Intelligence chưa được bật cho cửa hàng này.");
        if (requiredBaseQuantity <= 0)
            throw new ArgumentException("Số lượng cần mua phải lớn hơn 0.");
        if (actor.AccountId <= 0 || actor.StaffId <= 0)
            throw new UnauthorizedAccessException("Thiếu account/staff context.");
        await RequirePermissionAsync(actor.AccountId, storeId, "PurchaseAdvice.View");
        await RequirePermissionAsync(actor.AccountId, storeId, "SupplierQuality.View");
        if (!await _scope.CanAccessStoreAsync(actor.StaffId, storeId))
            throw new UnauthorizedAccessException("Cửa hàng nằm ngoài StaffScope.");

        var offers = await _repository.GetOffersAsync(storeId, ingredientId, ct);
        var raw = new List<RawCandidate>();
        var performanceFrom = DateTime.UtcNow.AddDays(-_options.PerformanceWindowDays);
        foreach (var offer in offers)
        {
            ct.ThrowIfCancellationRequested();
            var dashboard = await _quality.GetDashboardAsync(
                storeId, offer.SupplierId, performanceFrom, DateTime.UtcNow,
                actor.StaffId, actor.RoleNames);
            var performance = dashboard.Data?.Performance;

            if (offer.CurrentPrice > 0 && offer.PackageQuantity is > 0)
            {
                var converted = await _conversion.ConvertAsync(
                    ingredientId, offer.PackageQuantity.Value, offer.UnitId);
                if (converted.IsSuccess && converted.Data > 0)
                {
                    var packages = Math.Max(
                        (int)Math.Ceiling(requiredBaseQuantity / converted.Data),
                        Math.Max(1, offer.MinimumOrderPackageCount ?? 1));
                    raw.Add(CreateRaw(offer, "PACKAGED", offer.CurrentPrice,
                        converted.Data, packages, packages, performance));
                }
            }

            if (offer.AllowsLoosePurchase
                && offer.CurrentProcurementUnitPrice is > 0
                && offer.LooseProcurementUnitId.HasValue)
            {
                var unit = await _conversion.ConvertAsync(
                    ingredientId, 1m, offer.LooseProcurementUnitId.Value);
                if (unit.IsSuccess && unit.Data > 0)
                {
                    var requiredUnits = requiredBaseQuantity / unit.Data;
                    var minimum = Math.Max(0, offer.LooseMinimumOrderQuantity ?? 0);
                    var step = offer.LooseQuantityStep is > 0 ? offer.LooseQuantityStep.Value : 1m;
                    var purchaseUnits = Math.Max(minimum,
                        Math.Ceiling(requiredUnits / step) * step);
                    raw.Add(CreateRaw(offer, "LOOSE",
                        offer.CurrentProcurementUnitPrice.Value, unit.Data,
                        purchaseUnits, 0, performance));
                }
            }
        }

        var result = new SupplierRecommendationDto
        {
            StoreId = storeId,
            IngredientId = ingredientId,
            RequiredBaseQuantity = requiredBaseQuantity,
            WeightVersion = _options.WeightVersion,
            CalculatedAtUtc = DateTime.UtcNow,
            ShadowMode = feature.ShadowMode,
            FeatureMode = feature.Mode,
            FeatureSource = feature.Source
        };
        if (raw.Count == 0)
        {
            result.RankingMessage = "Không có nhà cung cấp và quy cách mua hợp lệ cho điều kiện hiện tại.";
            await RecordPilotAsync(result, ct);
            return result;
        }

        var minPrice = raw.Min(x => x.BasePrice);
        var maxPrice = raw.Max(x => x.BasePrice);
        var confirmedLeads = raw.Where(x => x.LeadSource == "CONFIRMED").Select(x => x.Lead).ToArray();
        var minLead = confirmedLeads.Length == 0 ? 0 : confirmedLeads.Min();
        var maxLead = confirmedLeads.Length == 0 ? 0 : confirmedLeads.Max();
        foreach (var item in raw)
        {
            var receipts = item.Performance?.ConfirmedReceiptCount ?? 0;
            var hasPerformance = receipts > 0 && item.Performance != null;
            var components = new SupplierScoreComponentDto
            {
                Price = Inverse(item.BasePrice, minPrice, maxPrice),
                LeadTime = item.LeadSource == "CONFIRMED" ? Inverse(item.Lead, minLead, maxLead) : null,
                OnTime = hasPerformance ? item.Performance!.OnTimeRate : null,
                Fill = hasPerformance ? item.Performance!.FillRate : null,
                Quality = hasPerformance
                    ? Math.Clamp(100 - ((item.Performance!.RejectionRate + item.Performance.IssueRate) / 2), 0, 100)
                    : null
            };
            var complete = components.Price.HasValue && components.OnTime.HasValue
                && components.Fill.HasValue && components.Quality.HasValue && components.LeadTime.HasValue;
            decimal? score = complete
                ? Math.Round((components.Price!.Value * _options.PriceWeight
                    + components.OnTime!.Value * _options.OnTimeWeight
                    + components.Fill!.Value * _options.FillWeight
                    + components.Quality!.Value * _options.QualityWeight
                    + components.LeadTime!.Value * _options.LeadTimeWeight) / 100, 2)
                : null;
            var confidence = receipts >= _options.HighConfidenceReceipts ? "HIGH"
                : receipts >= _options.MediumConfidenceReceipts ? "MEDIUM" : "INSUFFICIENT_DATA";
            var purchasedBase = item.PurchaseQuantity * item.UnitBaseQuantity;
            var excess = Math.Max(0, purchasedBase - requiredBaseQuantity);
            var warnings = new List<string>();
            if (confidence == "INSUFFICIENT_DATA") warnings.Add("Dữ liệu xác nhận nhận hàng dưới 5 phiếu.");
            if (!hasPerformance) warnings.Add("Chưa có dữ liệu hiệu suất nhà cung cấp; metric được giữ ở trạng thái unknown.");
            if (item.LeadSource == "FALLBACK") warnings.Add("Lead time 30 ngày là fallback, không phải dữ liệu đã xác nhận.");
            if (item.Performance != null
                && item.Performance.ExpectedDateSampleCount < item.Performance.ConfirmedReceiptCount)
                warnings.Add("Một số phiếu nhận thiếu ExpectedDelivery.");
            result.Candidates.Add(new SupplierRecommendationCandidateDto
            {
                SupplierId = item.SupplierId,
                IngredientSupplierId = item.OfferId,
                SupplierName = item.Name,
                PurchaseMode = item.Mode,
                Score = score,
                Confidence = confidence,
                Rankable = complete && confidence is ("HIGH" or "MEDIUM"),
                ReceiptCount = receipts,
                PackageCount = item.PackageCount,
                PackageBaseQuantity = item.UnitBaseQuantity,
                UnitPrice = item.UnitPrice,
                RequiredPurchaseQuantity = item.PurchaseQuantity,
                PurchasedBaseQuantity = purchasedBase,
                ExcessBaseQuantity = excess,
                ExcessRatio = requiredBaseQuantity == 0 ? 0 : Math.Round(excess / requiredBaseQuantity, 4),
                EstimatedAmount = item.PurchaseQuantity * item.UnitPrice,
                LeadTimeDays = item.Lead,
                LeadTimeSource = item.LeadSource,
                ComponentScores = components,
                Warnings = warnings
            });
        }

        var ranked = result.Candidates.Where(x => x.Rankable)
            .OrderByDescending(x => x.Score).ThenBy(x => x.EstimatedAmount).ToList();
        for (var index = 0; index < ranked.Count; index++) ranked[index].Rank = index + 1;
        result.Candidates = ranked.Concat(result.Candidates.Where(x => !x.Rankable)
                .OrderBy(x => x.EstimatedAmount)).ToList();
        result.HasCompetitiveRanking = ranked.Select(x => x.SupplierId).Distinct().Count() >= 2;
        result.RankingMessage = result.HasCompetitiveRanking
            ? "Ranking chỉ áp dụng cho candidate đủ dữ liệu và confidence."
            : ranked.Count == 1
                ? "Chỉ có một nhà cung cấp đủ điều kiện tham gia ranking; đây không phải so sánh cạnh tranh."
                : "Chưa có nhà cung cấp đủ dữ liệu để ranking.";
        await RecordPilotAsync(result, ct);
        return result;
    }

    private async Task RecordPilotAsync(SupplierRecommendationDto result, CancellationToken ct)
    {
        if (_context == null) return;
        try
        {
            _context.IntelligencePilotRuns.Add(new IntelligencePilotRun
            {
                FeatureCode = "SUPPLIER_INTELLIGENCE",
                StoreId = result.StoreId,
                RunMode = result.ShadowMode ? "SHADOW" : "ACTIVE",
                StartedAtUtc = result.CalculatedAtUtc,
                CompletedAtUtc = DateTime.UtcNow,
                Success = true,
                MetricsJson = JsonSerializer.Serialize(new
                {
                    CandidateCount = result.Candidates.Count,
                    RankableCount = result.Candidates.Count(x => x.Rankable),
                    InsufficientDataCount = result.Candidates.Count(x => x.Confidence == "INSUFFICIENT_DATA"),
                    OneCandidateCase = result.Candidates.Select(x => x.SupplierId).Distinct().Count() == 1,
                    LeadTimeFallbackCount = result.Candidates.Count(x => x.LeadTimeSource == "FALLBACK")
                })
            });
            await _context.SaveChangesAsync(ct);
        }
        catch
        {
            // Pilot telemetry is non-authoritative and must not fail comparison.
        }
    }

    private async Task RequirePermissionAsync(int accountId, int storeId, string code)
    {
        var permission = await _permissions.HasPermissionAsync(accountId, code, storeId);
        if (!permission.IsSuccess || permission.Data?.Allowed != true)
            throw new UnauthorizedAccessException($"Thiếu permission {code} tại cửa hàng.");
    }

    private static RawCandidate CreateRaw(
        IngredientSupplier offer, string mode, decimal unitPrice,
        decimal unitBase, decimal purchaseQuantity, int packageCount,
        CafeChain.Application.DTOs.Admin.Procurement.SupplierPerformanceDto? performance)
    {
        var lead = offer.LeadTimeDays ?? 30;
        return new RawCandidate(
            offer.IngredientSupplierId, offer.SupplierId,
            offer.Supplier.Name ?? $"NCC #{offer.SupplierId}", mode,
            unitPrice / unitBase, unitPrice, unitBase, purchaseQuantity,
            packageCount, lead, offer.LeadTimeDays.HasValue ? "CONFIRMED" : "FALLBACK",
            performance);
    }

    private static decimal Inverse(decimal value, decimal min, decimal max) =>
        max == min ? 100 : Math.Clamp((max - value) / (max - min) * 100, 0, 100);

    private sealed record RawCandidate(
        int OfferId, int SupplierId, string Name, string Mode,
        decimal BasePrice, decimal UnitPrice, decimal UnitBaseQuantity,
        decimal PurchaseQuantity, int PackageCount, int Lead, string LeadSource,
        CafeChain.Application.DTOs.Admin.Procurement.SupplierPerformanceDto? Performance);
}
