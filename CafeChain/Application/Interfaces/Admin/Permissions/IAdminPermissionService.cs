using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.Permissions
{
    public interface IAdminPermissionService
    {
        Task<ServiceResult<AdminRolePagedResultDto>> GetRolesAsync(int pageIndex, int pageSize, string? search);
        Task<ServiceResult<AdminPermissionStaffPagedResultDto>> GetStaffAsync(int pageIndex, int pageSize, string? search);
        Task<ServiceResult<List<ScopeReferenceDto>>> GetScopeReferencesAsync(int scopeTypeId, int? parentId = null);
        Task<ServiceResult<List<PermissionCatalogGroupDto>>> GetPermissionCatalogAsync();
        Task<ServiceResult<RolePermissionMatrixDto>> GetRolePermissionsAsync(int roleId);
        Task<ServiceResult> UpdateRolePermissionsAsync(int roleId, SaveRolePermissionsRequest request);
        Task<ServiceResult<StaffRolesDto>> GetStaffRolesAsync(int staffId);
        Task<ServiceResult> UpdateStaffRolesAsync(int staffId, SaveStaffRolesRequest request);
        Task<ServiceResult<StaffScopesDto>> GetStaffScopesAsync(int staffId);
        Task<ServiceResult> UpdateStaffScopesAsync(int staffId, SaveStaffScopesRequest request);
        Task<ServiceResult<AccountOverrideMatrixDto>> GetAccountOverridesAsync(int staffId);
        Task<ServiceResult> UpdateAccountOverridesAsync(int staffId, SaveAccountPermissionOverridesRequest request);
        Task<ServiceResult<PermissionDecisionDto>> HasPermissionAsync(int accountId, string permissionCode, int? targetStoreId = null);
    }
}
