using System.Security.Cryptography;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.Admin.StoreMenu;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Options;
using CafeChain.Infrastructure.Interfaces.Analytics;
using CafeChain.Models.Analytics;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.AI;

public sealed class PosRecommendationService : IPosRecommendationService
{
    private readonly IPosRecommendationRepository _repository;
    private readonly IStoreMenuAvailabilityEvaluator _availability;
    private readonly PosRecommendationOptions _options;
    public PosRecommendationService(IPosRecommendationRepository repository,
        IStoreMenuAvailabilityEvaluator availability,
        IOptions<PosRecommendationOptions> options)
    { _repository = repository; _availability = availability; _options = options.Value; }

    public async Task RebuildStoreAsync(int storeId, CancellationToken ct = default)
    {
        if (!_options.Enabled) return;
        await _repository.ReconcileConversionsAsync(storeId, ct);
        var now = DateTime.UtcNow; var baskets = await _repository.GetBasketsAsync(storeId, now.AddDays(-_options.AnalysisWindowDays), now, ct);
        if (baskets.Count < _options.MinimumBasketCount) { await _repository.ReplaceCatalogAsync(storeId, _options.ModelVersion, [], ct); return; }
        var itemCounts = baskets.SelectMany(x => x.DrinkIds).GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());
        var pairCounts = new Dictionary<(int Trigger, int Recommended), int>();
        foreach (var basket in baskets)
            foreach (var trigger in basket.DrinkIds)
                foreach (var recommended in basket.DrinkIds.Where(x => x != trigger))
                    pairCounts[(trigger, recommended)] = pairCounts.GetValueOrDefault((trigger, recommended)) + 1;

        var candidateIds = pairCounts.Keys.SelectMany(x => new[] { x.Trigger, x.Recommended }).Distinct().ToArray();
        var candidates = await _repository.GetCandidatesAsync(storeId, candidateIds, now, ct);
        var qualified = pairCounts.Select(x =>
        {
            var support = (decimal)x.Value / baskets.Count;
            var confidence = (decimal)x.Value / itemCounts[x.Key.Trigger];
            var recommendedProbability = (decimal)itemCounts[x.Key.Recommended] / baskets.Count;
            var lift = recommendedProbability == 0 ? 0 : confidence / recommendedProbability;
            return new { x.Key.Trigger, x.Key.Recommended, Support = support, Confidence = confidence, Lift = lift };
        }).Where(x => x.Support >= _options.MinimumSupport && x.Confidence >= _options.MinimumConfidence && x.Lift >= _options.MinimumLift
            && candidates.TryGetValue(x.Trigger, out var trigger) && trigger.IsAvailable
            && candidates.TryGetValue(x.Recommended, out var recommended) && recommended.IsAvailable && recommended.Margin >= 0)
          .GroupBy(x => x.Trigger)
          .SelectMany(g => g.OrderByDescending(x => x.Lift).ThenByDescending(x => x.Confidence).ThenBy(x => x.Recommended)
              .Take(_options.MaximumResults).Select((x, rank) => new PosRecommendationCatalog
              {
                  StoreId = storeId, TriggerDrinkId = x.Trigger, RecommendedDrinkId = x.Recommended,
                  Support = x.Support, Confidence = x.Confidence, Lift = x.Lift,
                  Margin = candidates[x.Recommended].Margin, Rank = rank + 1, ModelVersion = _options.ModelVersion,
                  GeneratedAtUtc = now, ExpiresAtUtc = now.AddHours(Math.Max(2, _options.IntervalHours * 2))
              })).ToList();
        await _repository.ReplaceCatalogAsync(storeId, _options.ModelVersion, qualified, ct);
    }

    public async Task<PosRecommendationResultDto> GetAsync(int storeId, Guid sessionId, IReadOnlyCollection<int> triggerDrinkIds, CancellationToken ct = default)
    {
        if (!_options.Enabled || sessionId == Guid.Empty || triggerDrinkIds.Count == 0) return new() { RecommendationSessionId = sessionId };
        var exposure = await _repository.GetExposureAsync(sessionId, ct);
        var variant = exposure?.Variant ?? StableVariant(sessionId);
        if (exposure == null)
        {
            exposure = new PosRecommendationExposure { RecommendationSessionId = sessionId, StoreId = storeId, Variant = variant, ModelVersion = _options.ModelVersion, CreatedAtUtc = DateTime.UtcNow };
            await _repository.AddExposureAsync(exposure, ct);
        }
        if (variant == "CONTROL") { await _repository.SaveChangesAsync(ct); return new() { RecommendationSessionId = sessionId, Variant = variant }; }

        var rows = await _repository.GetCatalogAsync(storeId, triggerDrinkIds, DateTime.UtcNow, _options.MaximumResults * 3, ct);
        var ids = rows.Select(x => x.RecommendedDrinkId).Distinct().ToArray();
        var current = await _repository.GetCandidatesAsync(storeId, ids, DateTime.UtcNow, ct);
        var operational = await _availability.EvaluateStoreAsync(storeId, DateTime.UtcNow, ct);
        var chosen = rows.Where(x => current.TryGetValue(x.RecommendedDrinkId, out var c) && c.IsAvailable
                && c.DrinkSizeIds.Any(sizeId => operational.TryGetValue(sizeId, out var state) && state.IsSellable)
                && !triggerDrinkIds.Contains(x.RecommendedDrinkId)).GroupBy(x => x.RecommendedDrinkId).Select(g => g.First())
            .OrderBy(x => x.Rank).ThenByDescending(x => x.Lift).Take(_options.MaximumResults).ToList();
        foreach (var row in chosen.Where(row => exposure.Items.All(x => x.RecommendedDrinkId != row.RecommendedDrinkId)))
            exposure.Items.Add(new PosRecommendationExposureItem { TriggerDrinkId = row.TriggerDrinkId, RecommendedDrinkId = row.RecommendedDrinkId, Rank = row.Rank, WasDisplayed = true });
        await _repository.SaveChangesAsync(ct);
        return new PosRecommendationResultDto
        {
            RecommendationSessionId = sessionId, Variant = variant,
            Items = chosen.Select(x => { var c = current[x.RecommendedDrinkId]; return new PosRecommendationDto { TriggerDrinkId = x.TriggerDrinkId, RecommendedDrinkId = x.RecommendedDrinkId, DrinkName = c.Name, ImageUrl = c.ImageUrl, Price = c.Price, Support = x.Support, Confidence = x.Confidence, Lift = x.Lift, Rank = x.Rank }; }).ToList()
        };
    }

    public async Task TrackAsync(int storeId, PosRecommendationInteractionDto input, CancellationToken ct = default)
    {
        if (!_options.Enabled || input.RecommendationSessionId == Guid.Empty) return;
        var exposure = await _repository.GetExposureAsync(input.RecommendationSessionId, ct);
        if (exposure == null || exposure.StoreId != storeId) return;
        var item = exposure.Items.FirstOrDefault(x => x.TriggerDrinkId == input.TriggerDrinkId && x.RecommendedDrinkId == input.RecommendedDrinkId);
        if (item == null) return;
        if (input.Action.Equals("CLICKED", StringComparison.OrdinalIgnoreCase)) item.WasClicked = true;
        if (input.Action.Equals("ADDED", StringComparison.OrdinalIgnoreCase)) { item.WasClicked = true; item.WasAdded = true; }
        await _repository.SaveChangesAsync(ct);
    }

    public async Task LinkOrderAsync(Guid sessionId, int orderId, CancellationToken ct = default)
    {
        var exposure = await _repository.GetExposureAsync(sessionId, ct); if (exposure == null || exposure.OrderId.HasValue) return;
        exposure.OrderId = orderId; await _repository.SaveChangesAsync(ct);
    }

    private static string StableVariant(Guid sessionId)
    {
        var hash = SHA256.HashData(sessionId.ToByteArray()); return (hash[0] & 1) == 0 ? "CONTROL" : "TREATMENT";
    }
}
