using CafeChain.Models.Enums.Unit;
using System.Text.Json.Serialization;

namespace CafeChain.Application.DTOs.Inventories;

/// <summary>
/// A trusted unit option expressed against an ingredient's inventory base unit.
/// The conversion factor is produced by the server and must never be accepted from a client.
/// </summary>
public sealed class InventoryUnitOptionDTO
{
    public int UnitId { get; init; }
    public string UnitCode { get; init; } = string.Empty;
    public string UnitName { get; init; } = string.Empty;
    [JsonIgnore]
    public UnitType UnitType { get; init; }
    public decimal ConversionFactorToBase { get; init; }
    public bool IsBaseUnit { get; init; }
}
