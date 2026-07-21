namespace CafeChain.Models.Staffs
{
    public class StaffShift
    {
        public int StaffShiftId { get; set; }
        public int StaffId { get; set; }
        public int ShiftId { get; set; }
        public TimeSpan? CustomStartTime { get; set; }
        public TimeSpan? CustomEndTime { get; set; }
        public DateTime WorkDate { get; set; }
        public int StatusId { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        public virtual StaffShiftStatus Status { get; set; } = null!;
        public virtual Staff Staff { get; set; } = null!;
        public virtual Shift Shift { get; set; } = null!;
    }
}
