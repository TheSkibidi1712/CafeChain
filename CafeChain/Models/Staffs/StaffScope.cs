namespace CafeChain.Models.Staffs
{
    public class StaffScope
    {
        public int StaffScopeId { get; set; }
        public int StaffId { get; set; }

        public int ScopeTypeId { get; set; } // 🔥 thêm
        public int ScopeRefId { get; set; }  // 🔥 generic id

        public virtual Staff Staff { get; set; }
        public virtual ScopeType ScopeType { get; set; }
    }
}
