using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Models.Inventories.Ice;

public class IcePolicy
{
    public int IcePolicyId { get; set; }
    public int StoreId { get; set; }
    public int IngredientId { get; set; }
    public int DisplayUnitId { get; set; }
    public decimal SuggestedDailyQuantity { get; set; }
    public decimal SuggestedShiftQuantity { get; set; }
    public bool AllowSupplementalIssue { get; set; }
    public bool AllowSameDayCarryOver { get; set; }
    public bool RequireVarianceApproval { get; set; }
    public decimal VarianceApprovalQuantityThreshold { get; set; }
    public decimal VarianceApprovalPercentThreshold { get; set; }
    public bool Active { get; set; }
    public int UpdatedByStaffId { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public virtual Store Store { get; set; } = null!;
    public virtual Ingredient Ingredient { get; set; } = null!;
    public virtual Unit DisplayUnit { get; set; } = null!;
    public virtual Staff UpdatedByStaff { get; set; } = null!;
    public virtual ICollection<IceAllocation> Allocations { get; set; } = [];
}
