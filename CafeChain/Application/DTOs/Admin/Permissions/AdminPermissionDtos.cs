using CafeChain.Models.Enums.Permissions;

namespace CafeChain.Application.DTOs.Admin.Permissions
{
    public class AdminRolePagedResultDto
    {
        public List<AdminRoleListItemDto> Items { get; set; } = new();
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }

    public class AdminRoleListItemDto
    {
        public int RoleId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Active { get; set; }
        public bool IsStoreLevel { get; set; }
        public int UserCount { get; set; }
        public int PermissionCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AdminPermissionStaffPagedResultDto
    {
        public List<AdminPermissionStaffListItemDto> Items { get; set; } = new();
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }

    public class AdminPermissionStaffListItemDto
    {
        public int StaffId { get; set; }
        public int AccountId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public bool Active { get; set; }
        public List<string> RoleNames { get; set; } = new();
    }

    public class ScopeReferenceDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class PermissionCatalogGroupDto
    {
        public int PermissionGroupId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public List<PermissionCatalogItemDto> Permissions { get; set; } = new();
    }

    public class PermissionCatalogItemDto
    {
        public int PermissionId { get; set; }
        public int PermissionGroupId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool Active { get; set; }
    }

    public class RolePermissionMatrixDto
    {
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty;
        public int UserCount { get; set; }
        public int PermissionCount { get; set; }
        public List<RolePermissionGroupDto> Groups { get; set; } = new();
    }

    public class RolePermissionGroupDto
    {
        public int PermissionGroupId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public List<RolePermissionItemDto> Permissions { get; set; } = new();
    }

    public class RolePermissionItemDto
    {
        public int PermissionId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsGranted { get; set; }
    }

    public class SaveRolePermissionsRequest
    {
        public List<int> PermissionIds { get; set; } = new();
    }

    public class StaffRolesDto
    {
        public int StaffId { get; set; }
        public int AccountId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public List<StaffRoleOptionDto> Roles { get; set; } = new();
    }

    public class StaffRoleOptionDto
    {
        public int RoleId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Active { get; set; }
        public bool IsStoreLevel { get; set; }
        public bool IsAssigned { get; set; }
    }

    public class SaveStaffRolesRequest
    {
        public List<int> RoleIds { get; set; } = new();
    }

    public class StaffScopesDto
    {
        public int StaffId { get; set; }
        public int AccountId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public List<ScopeTypeOptionDto> ScopeTypes { get; set; } = new();
        public List<StaffScopeItemDto> Scopes { get; set; } = new();
    }

    public class ScopeTypeOptionDto
    {
        public int ScopeTypeId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class StaffScopeItemDto
    {
        public int StaffScopeId { get; set; }
        public int ScopeTypeId { get; set; }
        public string ScopeTypeCode { get; set; } = string.Empty;
        public string ScopeTypeName { get; set; } = string.Empty;
        public int ScopeRefId { get; set; }
        public string ScopeRefName { get; set; } = string.Empty;
    }

    public class SaveStaffScopesRequest
    {
        public List<StaffScopeInputDto> Scopes { get; set; } = new();
    }

    public class StaffScopeInputDto
    {
        public int ScopeTypeId { get; set; }
        public int ScopeRefId { get; set; }
    }

    public class AccountOverrideMatrixDto
    {
        public int StaffId { get; set; }
        public int AccountId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PrimaryRoleName { get; set; } = string.Empty;
        public List<AccountOverrideGroupDto> Groups { get; set; } = new();
    }

    public class AccountOverrideGroupDto
    {
        public int PermissionGroupId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public List<AccountOverrideItemDto> Permissions { get; set; } = new();
    }

    public class AccountOverrideItemDto
    {
        public int PermissionId { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool RoleAllowed { get; set; }
        public PermissionEffect? OverrideEffect { get; set; }
        public bool FinalAllowed { get; set; }
        public string? Reason { get; set; }
    }

    public class SaveAccountPermissionOverridesRequest
    {
        public List<AccountPermissionOverrideInputDto> Overrides { get; set; } = new();
    }

    public class AccountPermissionOverrideInputDto
    {
        public int PermissionId { get; set; }
        public PermissionEffect? Effect { get; set; }
        public string? Reason { get; set; }
    }

    public class PermissionDecisionDto
    {
        public int AccountId { get; set; }
        public int? StaffId { get; set; }
        public string PermissionCode { get; set; } = string.Empty;
        public int? TargetStoreId { get; set; }
        public bool Allowed { get; set; }
        public bool RoleAllowed { get; set; }
        public PermissionEffect? OverrideEffect { get; set; }
        public bool ScopeAllowed { get; set; } = true;
        public string DenyReason { get; set; } = string.Empty;
    }

    public class StaffPermissionIdentityDto
    {
        public int StaffId { get; set; }
        public int AccountId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool StaffActive { get; set; }
        public bool AccountActive { get; set; }
        public string PrimaryRoleName { get; set; } = string.Empty;
    }

    public class AccountPermissionFactsDto
    {
        public bool AccountExists { get; set; }
        public bool AccountActive { get; set; }
        public int? StaffId { get; set; }
        public bool RoleAllowed { get; set; }
        public PermissionEffect? OverrideEffect { get; set; }
    }
}
