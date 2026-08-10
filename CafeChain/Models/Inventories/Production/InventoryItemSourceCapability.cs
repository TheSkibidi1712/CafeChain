using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;

namespace CafeChain.Models.Inventories.Production;

/// <summary>
/// Explicit global source capabilities for one inventory identity.
/// This is business configuration, not inventory writer capability.
/// </summary>
public class InventoryItemSourceCapability
{
    public int InventoryItemSourceCapabilityId { get; set; }
    public int? IngredientId { get; set; }
    public int? PreparedItemId { get; set; }
    public bool CanProduce { get; set; }
    public bool CanPurchase { get; set; }
    public bool CanTransfer { get; set; }
    public bool Active { get; set; } = true;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public int CreatedByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int? UpdatedByStaffId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual Ingredient? Ingredient { get; set; }
    public virtual PreparedItem? PreparedItem { get; set; }
}
