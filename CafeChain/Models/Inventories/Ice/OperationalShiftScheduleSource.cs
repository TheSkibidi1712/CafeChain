using CafeChain.Models.Staffs;

namespace CafeChain.Models.Inventories.Ice;

public class OperationalShiftScheduleSource
{
    public int OperationalShiftId { get; set; }
    public int StaffShiftId { get; set; }

    public virtual OperationalShift OperationalShift { get; set; } = null!;
    public virtual StaffShift StaffShift { get; set; } = null!;
}
