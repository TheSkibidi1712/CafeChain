using System.ComponentModel.DataAnnotations;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Staffs;

namespace CafeChain.Models.Inventories.Production;

public class ProductionRunInputActual
{
    public int ProductionRunInputActualId { get; set; }
    public int ProductionRunId { get; set; }
    public int? IngredientId { get; set; }
    public int? PreparedItemId { get; set; }
    public int BaseUnitId { get; set; }
    public decimal PlannedBaseQuantity { get; set; }
    public decimal ActualBaseQuantity { get; set; }
    public int ConfirmedByStaffId { get; set; }
    public DateTime ConfirmedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual ProductionRun ProductionRun { get; set; } = null!;
    public virtual Ingredient? Ingredient { get; set; }
    public virtual PreparedItem? PreparedItem { get; set; }
    public virtual Unit BaseUnit { get; set; } = null!;
    public virtual Staff ConfirmedByStaff { get; set; } = null!;
}
