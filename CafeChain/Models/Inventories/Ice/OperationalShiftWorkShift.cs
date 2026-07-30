using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Models.Inventories.Ice;

public class OperationalShiftWorkShift
{
    public int OperationalShiftId { get; set; }
    public int WorkShiftId { get; set; }
    public int LinkedByStaffId { get; set; }
    public DateTime LinkedAtUtc { get; set; }

    public virtual OperationalShift OperationalShift { get; set; } = null!;
    public virtual WorkShift WorkShift { get; set; } = null!;
    public virtual Staff LinkedByStaff { get; set; } = null!;
}
