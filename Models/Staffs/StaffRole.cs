namespace CafeChain.Models.Staffs
{
    public class StaffRole
    {
        public int StaffRoleId { get; set; }
        public int StaffId { get; set; }
        public int RoleId { get; set; }
        public DateTime AssignedAt { get; set; }

        public virtual Staff Staff { get; set; }
        public virtual Role Role { get; set; }
    }
}
