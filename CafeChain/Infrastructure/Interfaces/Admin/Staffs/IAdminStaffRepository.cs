using CafeChain.Models.Customers;
using CafeChain.Models.Permissions;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Infrastrusture.Interfaces.Admin.Staffs;

public interface IAdminStaffRepository
{
    Task<(IEnumerable<Models.Staffs.Staff> Items, int TotalCount)> GetPaginatedStaffsAsync(
        int pageIndex, int pageSize, IReadOnlyCollection<int>? storeIds, string search, int? roleFilter);
    Task<Models.Staffs.Staff> GetStaffByIdAsync(int staffId);
    Task<Models.Staffs.Staff?> GetStaffByAccountIdAsync(int accountId);
    Task<(int Total, int Active, int Inactive)> GetStaffCountsAsync(IReadOnlyCollection<int>? storeIds);
    Task<List<Role>> GetRolesForDropdownAsync(int? storeManagerStoreId);
    Task<List<Store>> GetActiveStoresAsync();
    Task<List<ScopeType>> GetScopeTypesAsync();
    Task<Store> GetStoreByIdAsync(int storeId);
    Task<List<Models.Locations.Province>> GetProvincesAsync();
    Task<List<Models.Locations.District>> GetDistrictsAsync(int provinceId);
    Task<List<Models.Locations.Ward>> GetWardsAsync(int districtId);
    Task<bool> ScopeCoversStoreAsync(int scopeTypeId, int scopeRefId, int storeId);
    Task<bool> IsAddressHierarchyValidAsync(int provinceId, int districtId, int wardId);
    Task CreateStaffTransactionAsync(Models.Staffs.Staff staff, Account account,
        List<AccountRole> accountRoles, List<StaffScope> staffScopes,
        List<StaffPhone> staffPhones, List<StaffAddress> staffAddresses);
    Task UpdateStaffTransactionAsync(Models.Staffs.Staff staff, Account account,
        List<AccountRole> accountRoles, List<StaffScope> staffScopes,
        List<StaffPhone> staffPhones, List<StaffAddress> staffAddresses);
    Task UpdateStaffProfileTransactionAsync(Models.Staffs.Staff staff, Account account,
        List<StaffPhone> staffPhones, List<StaffAddress> staffAddresses);
    Task<bool> ResetPasswordAsync(int accountId, string passwordHash);
    Task<bool> ToggleStatusAsync(int staffId);
    Task<bool> EmailExistsAsync(string email, int? excludeAccountId = null);
    Task<bool> DefaultPhoneExistsAsync(string phone, int? excludeStaffId = null);
    Task<bool> HasOpenCashSessionAsync(int staffId);
    Task<bool> HasActiveShiftAsync(int staffId);
    Task<bool> CCCDExistsAsync(string cccd, int? excludeStaffId = null);
}
