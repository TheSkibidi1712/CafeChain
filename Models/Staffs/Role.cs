using System.Collections;

namespace CafeChain.Models.Staffs
{
    public class Role
    {
        public int RoleId { get; set; }
        public string Name { get; set; }
        public bool Active { get; set; }
        public DateTime CreatedAt { get; set; }

        public virtual ICollection<Staff> Staffs { get; set; }
        public virtual ICollection<StaffRole> StaffRoles { get; set; }
    }
}
