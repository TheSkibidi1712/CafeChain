using CafeChain.Application.Constants;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Models.Inventories.Ice;

public class IceAllocation
{
    public int IceAllocationId { get; set; }
    public Guid PublicId { get; set; }
    public int OperationalShiftId { get; set; }
    public int IcePolicyId { get; set; }
    public int StoreInventoryId { get; set; }
    public int IngredientId { get; set; }
    public decimal OpeningCarryQuantity { get; set; }
    public decimal InitialIssuedQuantity { get; set; }
    public decimal SupplementalIssuedQuantity { get; set; }
    public decimal ReturnedQuantity { get; set; }
    public decimal ClosingCarryQuantity { get; set; }
    public decimal TheoreticalUsageQuantity { get; set; }
    public decimal? ActualUsageQuantity { get; set; }
    public decimal? VarianceQuantity { get; set; }
    public decimal ReservedOutstandingQuantity { get; set; }
    public decimal? UnitCostSnapshot { get; set; }
    public string CostSnapshotStatus { get; set; } = IceCostSnapshotStatuses.Missing;
    public string ReservationReference { get; set; } = string.Empty;
    public string Status { get; set; } = OperationalIceStatuses.Draft;
    public string? ReconciliationReason { get; set; }
    public string? CloseReason { get; set; }
    public string? ReturnCondition { get; set; }
    public int? ReturnedByStaffId { get; set; }
    public int? ReturnReceivedByStaffId { get; set; }
    public DateTime? ReturnedAtUtc { get; set; }
    public int CreatedByStaffId { get; set; }
    public int? OpenedByStaffId { get; set; }
    public int? ClosedByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? OpenedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public int Revision { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public virtual OperationalShift OperationalShift { get; set; } = null!;
    public virtual IcePolicy IcePolicy { get; set; } = null!;
    public virtual StoreInventory StoreInventory { get; set; } = null!;
    public virtual Ingredient Ingredient { get; set; } = null!;
    public virtual Staff CreatedByStaff { get; set; } = null!;
    public virtual Staff? OpenedByStaff { get; set; }
    public virtual Staff? ClosedByStaff { get; set; }
    public virtual Staff? ReturnedByStaff { get; set; }
    public virtual Staff? ReturnReceivedByStaff { get; set; }
    public virtual ICollection<IceSupplementalIssue> SupplementalIssues { get; set; } = [];
    public virtual ICollection<IceCarryOver> OutgoingCarryOvers { get; set; } = [];
    public virtual ICollection<IceCarryOver> IncomingCarryOvers { get; set; } = [];
    public virtual ICollection<IceInventoryPosting> InventoryPostings { get; set; } = [];
}
