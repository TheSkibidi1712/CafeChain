using CafeChain.Models.Customers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Infrastrusture.Interfaces.Admin.Staffs
{
    public interface IAdminStaffRepository
    {
        // === READ ===
        Task<(IEnumerable<Models.Staffs.Staff> Items, int TotalCount)> GetPaginatedStaffsAsync(
            int pageIndex, int pageSize, int? storeId, string search, int? roleFilter);

        Task<Models.Staffs.Staff> GetStaffByIdAsync(int staffId);

        Task<(int Total, int Active, int Inactive)> GetStaffCountsAsync(int? storeId);

        // === MASTER DATA (cho Thin Controller) ===
        Task<List<Role>> GetRolesForDropdownAsync(int? storeManagerStoreId);
        Task<List<Store>> GetActiveStoresAsync();
        Task<List<ScopeType>> GetScopeTypesAsync();
        Task<Store> GetStoreByIdAsync(int storeId);
        Task<List<CafeChain.Models.Locations.Province>> GetProvincesAsync();
        Task<List<CafeChain.Models.Locations.District>> GetDistrictsAsync(int provinceId);

        // === WRITE (TRANSACTION) ===
        Task CreateStaffTransactionAsync(
            Models.Staffs.Staff staff,
            Account account,
            List<AccountRole> accountRoles,
            List<StaffScope> staffScopes,
            List<StaffPhone> staffPhones,
            List<StaffAddress> staffAddresses,
            List<StaffBank> staffBanks);

        Task UpdateStaffTransactionAsync(
            Models.Staffs.Staff staff,
            Account account,
            List<AccountRole> accountRoles,
            List<StaffScope> staffScopes,
            List<StaffPhone> staffPhones,
            List<StaffAddress> staffAddresses,
            List<StaffBank> staffBanks);

        Task UpdateStaffAvatarAsync(int staffId, string avatarUrl);

        Task<bool> ToggleStatusAsync(int staffId);

        // === VALIDATION QUERIES ===
        Task<bool> EmailExistsAsync(string email, int? excludeAccountId = null);
        Task<bool> DefaultPhoneExistsAsync(string phone, int? excludeStaffId = null);
        Task<bool> TaxCodeExistsAsync(string taxCode, int? excludeStaffId = null);
        Task<bool> HasOpenCashSessionAsync(int staffId);
        Task<bool> HasActiveShiftAsync(int staffId);
        Task<bool> CCCDExistsAsync(string cccd, int? excludeStaffId = null);
    }
}
