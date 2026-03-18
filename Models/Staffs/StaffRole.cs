namespace CafeChain.Models.Staffs
{
    public class StaffRole
    {
        public int StaRId { get; set; }
        public int StaId { get; set; }
        public int RoleId { get; set; }
        public DateTime AssignedAt { get; set; }

        public virtual Staff Staff { get; set; }
        public virtual Role Role { get; set; }
    }
}
