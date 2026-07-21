using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Models.Enums.Permissions;
using CafeChain.Models.Permissions;

namespace CafeChain.Infrastructure.Interfaces.Admin.Permissions
{
    public interface IAdminPermissionRepository
    {
        Task<(List<AdminRoleListItemDto> Items, int TotalCount)> GetPagedRolesAsync(int pageIndex, int pageSize, string? search);
        Task<(List<AdminPermissionStaffListItemDto> Items, int TotalCount)> GetPagedStaffAsync(int pageIndex, int pageSize, string? search);
        Task<List<ScopeReferenceDto>> GetScopeReferencesAsync(int scopeTypeId, int? parentId = null);
        Task<AdminRoleListItemDto?> GetRoleSummaryAsync(int roleId);
        Task<List<PermissionCatalogGroupDto>> GetPermissionCatalogAsync(bool activeOnly = true);
        Task<List<int>> GetRolePermissionIdsAsync(int roleId);
        Task<List<int>> GetActivePermissionIdsAsync(IEnumerable<int> permissionIds);
        Task<bool> RoleExistsAsync(int roleId);
        Task ReplaceRolePermissionsAsync(int roleId, IEnumerable<int> permissionIds);

        Task<StaffPermissionIdentityDto?> GetStaffIdentityAsync(int staffId);
        Task<List<StaffRoleOptionDto>> GetRoleOptionsAsync(bool includeCustomer = false);
        Task<List<int>> GetAssignedRoleIdsAsync(int accountId);
        Task<List<int>> GetAssignableRoleIdsAsync(IEnumerable<int> roleIds);
        Task ReplaceAccountRolesAsync(int accountId, IEnumerable<int> roleIds);

        Task<List<ScopeTypeOptionDto>> GetScopeTypesAsync();
        Task<List<StaffScopeItemDto>> GetStaffScopesAsync(int staffId);
        Task<List<StaffScopeInputDto>> GetInvalidScopeRefsAsync(IEnumerable<StaffScopeInputDto> scopes);
        Task<bool> ScopesCoverStoreAsync(IEnumerable<StaffScopeInputDto> scopes, int storeId);
        Task ReplaceStaffScopesAsync(int staffId, IEnumerable<StaffScopeInputDto> scopes);

        Task<Dictionary<int, PermissionEffect>> GetAccountOverrideEffectsAsync(int accountId);
        Task<Dictionary<int, string?>> GetAccountOverrideReasonsAsync(int accountId);
        Task<List<int>> GetRoleAllowedPermissionIdsForAccountAsync(int accountId);
        Task SaveAccountOverridesAsync(int accountId, IEnumerable<AccountPermissionOverrideInputDto> overrides);

        Task<Permission?> GetActivePermissionByCodeAsync(string permissionCode);
        Task<AccountPermissionFactsDto> GetAccountPermissionFactsAsync(int accountId, int permissionId);
        Task<HashSet<string>> GetEffectivePermissionCodesAsync(int accountId);
    }
}
