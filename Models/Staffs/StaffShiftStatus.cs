namespace CafeChain.Models.Staffs
{
    public class StaffShiftStatus
    {
        public int StaffShiftStatusId { get; set; }
        public string Code { get; set; } // PLANNED
        public string Name { get; set; } // Đã lên lịch
        public bool IsSystem { get; set; }

        public virtual ICollection<StaffShift> StaffShifts { get; set; }
    }
}
