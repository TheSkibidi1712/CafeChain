namespace CafeChain.Models.Staffs
{
    public class StaffShift
    {
        public int StaffShiftId { get; set; }
        public int StaffId { get; set; }
        public int ShiftId { get; set; }
        public DateTime WorkDate { get; set; }

        public virtual Staff Staff { get; set; }
        public virtual Shift Shift { get; set; }
    }
}
