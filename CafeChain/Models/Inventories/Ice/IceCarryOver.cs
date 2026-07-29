using CafeChain.Application.Constants;
using CafeChain.Models.Staffs;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Models.Inventories.Ice;

public class IceCarryOver
{
    public int IceCarryOverId { get; set; }
    public Guid PublicId { get; set; }
    public int FromOperationalShiftId { get; set; }
    public int ToOperationalShiftId { get; set; }
    public int FromIceAllocationId { get; set; }
    public int ToIceAllocationId { get; set; }
    public decimal Quantity { get; set; }
    public string Status { get; set; } = IceCarryOverStatuses.Pending;
    public int HandedOverByStaffId { get; set; }
    public int? ReceivedByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ConfirmedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public virtual OperationalShift FromOperationalShift { get; set; } = null!;
    public virtual OperationalShift ToOperationalShift { get; set; } = null!;
    public virtual IceAllocation FromIceAllocation { get; set; } = null!;
    public virtual IceAllocation ToIceAllocation { get; set; } = null!;
    public virtual Staff HandedOverByStaff { get; set; } = null!;
    public virtual Staff? ReceivedByStaff { get; set; }
}
