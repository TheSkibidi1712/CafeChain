namespace CafeChain.Models.Staffs
{
    public class StaffShiftStatus
    {
        public int StaffShiftStatusId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsSystem { get; set; }

        public virtual ICollection<StaffShift> StaffShifts { get; set; } = new List<StaffShift>();
    }
}
