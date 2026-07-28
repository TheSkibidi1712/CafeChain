using CafeChain.Application.Constants;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using System.ComponentModel.DataAnnotations;

namespace CafeChain.Models.Inventories.Ice;

public class OperationalShift
{
    public int OperationalShiftId { get; set; }
    public int StoreId { get; set; }
    public DateTime BusinessDate { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public int? ShiftLeadId { get; set; }
    public string Status { get; set; } = OperationalIceStatuses.Draft;
    public int CreatedByStaffId { get; set; }
    public int? OpenedByStaffId { get; set; }
    public int? ClosedByStaffId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? OpenedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public virtual Store Store { get; set; } = null!;
    public virtual Staff? ShiftLead { get; set; }
    public virtual Staff CreatedByStaff { get; set; } = null!;
    public virtual Staff? OpenedByStaff { get; set; }
    public virtual Staff? ClosedByStaff { get; set; }
    public virtual ICollection<OperationalShiftWorkShift> WorkShiftLinks { get; set; } = [];
    public virtual ICollection<IceAllocation> IceAllocations { get; set; } = [];
    public virtual ICollection<IceCarryOver> OutgoingCarryOvers { get; set; } = [];
    public virtual ICollection<IceCarryOver> IncomingCarryOvers { get; set; } = [];
}
