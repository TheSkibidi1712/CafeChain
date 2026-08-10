using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories.Production;

/// <summary>
/// Explicit permission for one store to produce one inventory identity.
/// Global item capability must also allow production.
/// </summary>
public class StoreProductionCapability
{
    public int StoreProductionCapabilityId { get; set; }
    public int StoreId { get; set; }
    public int? IngredientId { get; set; }
    public int? PreparedItemId { get; set; }
    public bool Active { get; set; } = true;
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public int CreatedByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int? UpdatedByStaffId { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual Store Store { get; set; } = null!;
    public virtual Ingredient? Ingredient { get; set; }
    public virtual PreparedItem? PreparedItem { get; set; }
}
