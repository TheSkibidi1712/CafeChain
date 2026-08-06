using CafeChain.Application.DTOs.Inventories;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;

namespace CafeChain.Application.Services.Inventories;

/// <summary>
/// Limits demand and loose-purchase inputs to practical procurement units while
/// leaving the broader recipe/inventory conversion registry unchanged.
/// </summary>
public static class ProcurementUnitPolicy
{
    private static readonly HashSet<string> MassCodes =
        new(StringComparer.OrdinalIgnoreCase) { "g", "kg" };

    private static readonly HashSet<string> VolumeCodes =
        new(StringComparer.OrdinalIgnoreCase) { "ml", "l" };

    public static IReadOnlyList<InventoryUnitOptionDTO> Filter(
        IReadOnlyList<InventoryUnitOptionDTO> options)
    {
        var baseOption = options.FirstOrDefault(x => x.IsBaseUnit);
        if (baseOption == null)
            return Array.Empty<InventoryUnitOptionDTO>();

        return options
            .Where(x => IsAllowed(baseOption.UnitType, x.UnitType, x.UnitCode))
            .ToList();
    }

    public static bool IsAllowed(Unit baseUnit, Unit candidate) =>
        IsAllowed(baseUnit.Type, candidate.Type, candidate.UnitCode);

    private static bool IsAllowed(
        UnitType baseType,
        UnitType candidateType,
        string candidateCode)
    {
        if (candidateType != baseType)
            return false;

        return baseType switch
        {
            UnitType.KhoiLuong => MassCodes.Contains(candidateCode),
            UnitType.TheTich => VolumeCodes.Contains(candidateCode),
            UnitType.Dem => true,
            _ => false
        };
    }
}
