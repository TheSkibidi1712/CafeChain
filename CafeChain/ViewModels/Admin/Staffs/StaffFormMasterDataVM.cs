using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.ViewModels.Admin.Staffs
{
    /// <summary>
    /// Master data cho form Create/Edit Staff.
    /// Controller không được gọi _context — mọi data này do Service cung cấp.
    /// </summary>
    public class StaffFormMasterDataVM
    {
        public List<Role> Roles { get; set; } = new();
        public List<Store> Stores { get; set; }
        public List<ScopeType> ScopeTypes { get; set; } = new();
        public bool IsStoreManager { get; set; }
        public int CurrentStoreId { get; set; }
        public string CurrentStoreName { get; set; }
    }
}
