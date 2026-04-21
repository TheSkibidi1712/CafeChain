namespace CafeChain.Models.Staffs
{
    public class StaffShift
    {
        public int StaffShiftId { get; set; }
        public int StaffId { get; set; }
        public int? ShiftId { get; set; }
        public bool IsAdHoc { get; set; }
        public TimeSpan? CustomStartTime { get; set; }
        public TimeSpan? CustomEndTime { get; set; }
        public DateTime WorkDate { get; set; }
        public DateTime? ActualCheckIn { get; set; }
        public DateTime? ActualCheckOut { get; set; }
        public decimal? PayrollHours { get; set; } // Tổng giờ làm đã làm tròn 15 phút
        public int StatusId { get; set; }
        // PLANNED, CHECKED_IN, COMPLETED, ABSENT
        public virtual StaffShiftStatus Status { get; set; }
        public virtual Staff Staff { get; set; }
        public virtual Shift Shift { get; set; }
    }
}
