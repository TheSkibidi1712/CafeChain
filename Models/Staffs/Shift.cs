using CafeChain.Models.Stores;

namespace CafeChain.Models.Staffs
{
    public class Shift
    {
        public int ShiftId { get; set; }

        public string Name { get; set; }

        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public bool IsOvernight { get; set; } // 🔥 ca qua đêm

        public bool Active { get; set; }

        public int StoreId { get; set; } // 🔥 thuộc store nào

        public string? Notes { get; set; } // Ghi chú của quản lý cho ca này

        public virtual Store Store { get; set; }

        public virtual ICollection<StaffShift> StaffShifts { get; set; }
    }
}
