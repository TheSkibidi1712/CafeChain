namespace CafeChain.Models.Staffs
{
    public class StaffScope
    {
        public int StaSId { get; set; }
        public int StaId { get; set; }

        public int ScopeTypeId { get; set; } // 🔥 thêm
        public int ScopeRefId { get; set; }  // 🔥 generic id

        public virtual Staff Staff { get; set; }
        public virtual ScopeType ScopeType { get; set; }
    }
}
