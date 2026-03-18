namespace CafeChain.Models.Staffs
{
    public class StaffShift
    {
        public int StaSId { get; set; }
        public int StaId { get; set; }
        public int ShiId { get; set; }
        public DateTime WorkDate { get; set; }

        public virtual Staff Staff { get; set; }
        public virtual Shift Shift { get; set; }
    }
}
