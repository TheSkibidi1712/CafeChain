namespace CafeChain.Application.DTOs.POS;

public static class POSIceLevels
{
    public const int None = 0;
    public const int Half = 50;
    public const int Full = 100;

    public static bool IsAllowed(int value) => value is None or Half or Full;
}

public sealed class POSIceEligibilityDto
{
    public bool SupportsIceCustomization { get; init; }
    public int? IceIngredientId { get; init; }
    public decimal? BaseIceQuantityBaseUnit { get; init; }
}

public sealed class POSIceOrderSnapshotDto
{
    public int IceLevelPercent { get; init; }
    public int IceIngredientId { get; init; }
    public decimal BaseIceQuantityBaseUnit { get; init; }
    public decimal AppliedIceQuantityBaseUnit { get; init; }
}
