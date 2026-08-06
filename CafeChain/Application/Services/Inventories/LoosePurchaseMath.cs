namespace CafeChain.Application.Services.Inventories;

public readonly record struct LoosePurchasePlan(
    decimal DemandCoveredQuantity,
    decimal OrderedQuantity,
    decimal RoundingSurplusQuantity);

public static class LoosePurchaseMath
{
    public static bool TryPlan(
        decimal requestedQuantity,
        decimal? minimumOrderQuantity,
        decimal? quantityStep,
        out LoosePurchasePlan plan)
    {
        plan = default;
        if (requestedQuantity <= 0m
            || minimumOrderQuantity < 0m
            || quantityStep <= 0m)
            return false;

        var ordered = Math.Max(requestedQuantity, minimumOrderQuantity.GetValueOrDefault());
        if (quantityStep.HasValue)
            ordered = decimal.Ceiling(ordered / quantityStep.Value) * quantityStep.Value;

        plan = new LoosePurchasePlan(
            requestedQuantity,
            ordered,
            ordered - requestedQuantity);
        return true;
    }
}
