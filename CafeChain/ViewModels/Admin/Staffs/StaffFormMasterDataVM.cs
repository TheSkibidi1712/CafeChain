using CafeChain.Models.Permissions;
using CafeChain.Models.Stores;
using CafeChain.Models.Locations;

namespace CafeChain.ViewModels.Admin.Staffs
{
    /// <summary>
    /// Master data cho form Create/Edit Staff.
    /// Controller không được gọi _context — mọi data này do Service cung cấp.
    /// </summary>
    public class StaffFormMasterDataVM
    {
        public List<Role> Roles { get; set; } = new();
        public List<Store> Stores { get; set; } = new();
        public List<StaffScopeTypeOptionVM> ScopeTypes { get; set; } = new();
        public List<Province> Provinces { get; set; } = new();
        public bool IsStoreManager { get; set; }
        public int CurrentStoreId { get; set; }
        public string CurrentStoreName { get; set; } = string.Empty;
    }

    public class StaffScopeTypeOptionVM
    {
        public int ScopeTypeId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}
