namespace CafeChain.Application.Services.Inventories;

public readonly record struct PurchasePackPlan(
    int PackageCount,
    decimal DemandCoveredBaseQuantity,
    decimal OrderedBaseQuantity,
    decimal RoundingSurplusBaseQuantity);

public static class PurchasePackMath
{
    public static bool IsWholePackageCount(decimal packageCount) =>
        packageCount > 0m && decimal.Truncate(packageCount) == packageCount;

    public static bool TryPlan(
        decimal remainingDemandBaseQuantity,
        decimal packageBaseQuantity,
        out PurchasePackPlan plan)
    {
        plan = default;
        if (remainingDemandBaseQuantity <= 0m || packageBaseQuantity <= 0m)
            return false;

        var rawPackageCount = decimal.Ceiling(remainingDemandBaseQuantity / packageBaseQuantity);
        if (rawPackageCount > int.MaxValue)
            return false;

        var packageCount = decimal.ToInt32(rawPackageCount);
        var orderedBaseQuantity = packageCount * packageBaseQuantity;
        plan = new PurchasePackPlan(
            packageCount,
            remainingDemandBaseQuantity,
            orderedBaseQuantity,
            orderedBaseQuantity - remainingDemandBaseQuantity);
        return true;
    }
}
