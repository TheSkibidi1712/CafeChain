using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.Permissions
{
    public interface IAdminPermissionService
    {
        Task<ServiceResult<AdminRolePagedResultDto>> GetRolesAsync(int pageIndex, int pageSize, string? search);
        Task<ServiceResult<AdminPermissionStaffPagedResultDto>> GetStaffAsync(int pageIndex, int pageSize, string? search);
        Task<ServiceResult<List<ScopeReferenceDto>>> GetScopeReferencesAsync(
            int scopeTypeId,
            System.Security.Claims.ClaimsPrincipal actor,
            int? parentId = null);
        Task<ServiceResult<List<PermissionCatalogGroupDto>>> GetPermissionCatalogAsync();
        Task<ServiceResult<RolePermissionMatrixDto>> GetRolePermissionsAsync(int roleId, System.Security.Claims.ClaimsPrincipal actor);
        Task<ServiceResult> UpdateRolePermissionsAsync(int roleId, SaveRolePermissionsRequest request, System.Security.Claims.ClaimsPrincipal actor);
        Task<ServiceResult<StaffRolesDto>> GetStaffRolesAsync(int staffId, System.Security.Claims.ClaimsPrincipal actor);
        Task<ServiceResult> UpdateStaffRolesAsync(int staffId, SaveStaffRolesRequest request, System.Security.Claims.ClaimsPrincipal actor);
        Task<ServiceResult<StaffScopesDto>> GetStaffScopesAsync(int staffId, System.Security.Claims.ClaimsPrincipal actor);
        Task<ServiceResult> UpdateStaffScopesAsync(int staffId, SaveStaffScopesRequest request, System.Security.Claims.ClaimsPrincipal actor);
        Task<ServiceResult<AccountOverrideMatrixDto>> GetAccountOverridesAsync(int staffId, System.Security.Claims.ClaimsPrincipal actor);
        Task<ServiceResult> UpdateAccountOverridesAsync(int staffId, SaveAccountPermissionOverridesRequest request, System.Security.Claims.ClaimsPrincipal actor);
        Task<ServiceResult<PermissionDecisionDto>> HasPermissionAsync(int accountId, string permissionCode, int? targetStoreId = null);
        Task<ServiceResult<HashSet<string>>> GetEffectivePermissionCodesAsync(int accountId);
    }
}
