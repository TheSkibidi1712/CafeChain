using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Admin.Permissions;
using CafeChain.Models.Enums.Permissions;

namespace CafeChain.Application.Services.Admin.Permissions
{
    public class AdminPermissionService : IAdminPermissionService
    {
        private const int DefaultPageIndex = 1;
        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 100;

        private readonly IAdminPermissionRepository _repository;
        private readonly IScopeAuthorizationService _scopeAuthorizationService;

        public AdminPermissionService(
            IAdminPermissionRepository repository,
            IScopeAuthorizationService scopeAuthorizationService)
        {
            _repository = repository;
            _scopeAuthorizationService = scopeAuthorizationService;
        }

        public async Task<ServiceResult<AdminRolePagedResultDto>> GetRolesAsync(
            int pageIndex,
            int pageSize,
            string? search)
        {
            pageIndex = pageIndex < 1 ? DefaultPageIndex : pageIndex;
            pageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

            var (items, totalCount) = await _repository.GetPagedRolesAsync(pageIndex, pageSize, search);

            return ServiceResult<AdminRolePagedResultDto>.Success(new AdminRolePagedResultDto
            {
                Items = items,
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        public async Task<ServiceResult<List<PermissionCatalogGroupDto>>> GetPermissionCatalogAsync()
        {
            var catalog = await _repository.GetPermissionCatalogAsync();
            return ServiceResult<List<PermissionCatalogGroupDto>>.Success(catalog);
        }

        public async Task<ServiceResult<RolePermissionMatrixDto>> GetRolePermissionsAsync(int roleId)
        {
            var role = await _repository.GetRoleSummaryAsync(roleId);
            if (role == null)
            {
                return ServiceResult<RolePermissionMatrixDto>.Failure("Role not found.");
            }

            var groups = await _repository.GetPermissionCatalogAsync();
            var grantedIds = (await _repository.GetRolePermissionIdsAsync(roleId)).ToHashSet();

            var matrix = new RolePermissionMatrixDto
            {
                RoleId = role.RoleId,
                RoleName = role.Name,
                UserCount = role.UserCount,
                PermissionCount = grantedIds.Count,
                Groups = groups.Select(group => new RolePermissionGroupDto
                {
                    PermissionGroupId = group.PermissionGroupId,
                    Code = group.Code,
                    Name = group.Name,
                    DisplayOrder = group.DisplayOrder,
                    Permissions = group.Permissions.Select(permission => new RolePermissionItemDto
                    {
                        PermissionId = permission.PermissionId,
                        Code = permission.Code,
                        Name = permission.Name,
                        Description = permission.Description,
                        IsGranted = grantedIds.Contains(permission.PermissionId)
                    }).ToList()
                }).ToList()
            };

            return ServiceResult<RolePermissionMatrixDto>.Success(matrix);
        }

        public async Task<ServiceResult> UpdateRolePermissionsAsync(int roleId, SaveRolePermissionsRequest request)
        {
            if (!await _repository.RoleExistsAsync(roleId))
            {
                return ServiceResult.Failure("Role not found or inactive.");
            }

            var requestedIds = NormalizeIds(request.PermissionIds);
            var activeIds = await _repository.GetActivePermissionIdsAsync(requestedIds);
            var invalidIds = requestedIds.Except(activeIds).ToList();

            if (invalidIds.Any())
            {
                return ServiceResult.Failure(
                    "Some permissions are invalid or inactive.",
                    invalidIds.Select(x => x.ToString()).ToList(),
                    "INVALID_PERMISSION");
            }

            await _repository.ReplaceRolePermissionsAsync(roleId, requestedIds);

            return ServiceResult.Success("Role permissions updated.");
        }

        public async Task<ServiceResult<StaffRolesDto>> GetStaffRolesAsync(int staffId)
        {
            var staff = await _repository.GetStaffIdentityAsync(staffId);
            if (staff == null)
            {
                return ServiceResult<StaffRolesDto>.Failure("Staff not found.");
            }

            var assignedIds = (await _repository.GetAssignedRoleIdsAsync(staff.AccountId)).ToHashSet();
            var roles = await _repository.GetRoleOptionsAsync();

            foreach (var role in roles)
            {
                role.IsAssigned = assignedIds.Contains(role.RoleId);
            }

            return ServiceResult<StaffRolesDto>.Success(new StaffRolesDto
            {
                StaffId = staff.StaffId,
                AccountId = staff.AccountId,
                FullName = staff.FullName,
                Email = staff.Email,
                Roles = roles
            });
        }

        public async Task<ServiceResult> UpdateStaffRolesAsync(int staffId, SaveStaffRolesRequest request)
        {
            var staff = await _repository.GetStaffIdentityAsync(staffId);
            if (staff == null)
            {
                return ServiceResult.Failure("Staff not found.");
            }

            if (!staff.AccountActive)
            {
                return ServiceResult.Failure("Account is inactive.");
            }

            var requestedIds = NormalizeIds(request.RoleIds);
            if (!requestedIds.Any())
            {
                return ServiceResult.Failure("At least one role is required.", errorCode: "ROLE_REQUIRED");
            }

            var assignableIds = await _repository.GetAssignableRoleIdsAsync(requestedIds);
            var invalidIds = requestedIds.Except(assignableIds).ToList();

            if (invalidIds.Any())
            {
                return ServiceResult.Failure(
                    "Some roles are invalid, inactive, or not assignable to staff.",
                    invalidIds.Select(x => x.ToString()).ToList(),
                    "INVALID_ROLE");
            }

            await _repository.ReplaceAccountRolesAsync(staff.AccountId, requestedIds);

            return ServiceResult.Success("Staff roles updated.");
        }

        public async Task<ServiceResult<StaffScopesDto>> GetStaffScopesAsync(int staffId)
        {
            var staff = await _repository.GetStaffIdentityAsync(staffId);
            if (staff == null)
            {
                return ServiceResult<StaffScopesDto>.Failure("Staff not found.");
            }

            return ServiceResult<StaffScopesDto>.Success(new StaffScopesDto
            {
                StaffId = staff.StaffId,
                AccountId = staff.AccountId,
                FullName = staff.FullName,
                ScopeTypes = await _repository.GetScopeTypesAsync(),
                Scopes = await _repository.GetStaffScopesAsync(staffId)
            });
        }

        public async Task<ServiceResult> UpdateStaffScopesAsync(int staffId, SaveStaffScopesRequest request)
        {
            var staff = await _repository.GetStaffIdentityAsync(staffId);
            if (staff == null)
            {
                return ServiceResult.Failure("Staff not found.");
            }

            var scopes = NormalizeScopes(request.Scopes);
            var invalidScopes = await _repository.GetInvalidScopeRefsAsync(scopes);

            if (invalidScopes.Any())
            {
                return ServiceResult.Failure(
                    "Some scopes are invalid.",
                    invalidScopes.Select(x => $"{x.ScopeTypeId}:{x.ScopeRefId}").ToList(),
                    "INVALID_SCOPE");
            }

            await _repository.ReplaceStaffScopesAsync(staffId, scopes);

            return ServiceResult.Success("Staff scopes updated.");
        }

        public async Task<ServiceResult<AccountOverrideMatrixDto>> GetAccountOverridesAsync(int staffId)
        {
            var staff = await _repository.GetStaffIdentityAsync(staffId);
            if (staff == null)
            {
                return ServiceResult<AccountOverrideMatrixDto>.Failure("Staff not found.");
            }

            var groups = await _repository.GetPermissionCatalogAsync();
            var roleAllowedIds = (await _repository.GetRoleAllowedPermissionIdsForAccountAsync(staff.AccountId)).ToHashSet();
            var overrideEffects = await _repository.GetAccountOverrideEffectsAsync(staff.AccountId);
            var overrideReasons = await _repository.GetAccountOverrideReasonsAsync(staff.AccountId);

            var matrix = new AccountOverrideMatrixDto
            {
                StaffId = staff.StaffId,
                AccountId = staff.AccountId,
                FullName = staff.FullName,
                Email = staff.Email,
                PrimaryRoleName = staff.PrimaryRoleName,
                Groups = groups.Select(group => new AccountOverrideGroupDto
                {
                    PermissionGroupId = group.PermissionGroupId,
                    Code = group.Code,
                    Name = group.Name,
                    DisplayOrder = group.DisplayOrder,
                    Permissions = group.Permissions.Select(permission =>
                    {
                        var roleAllowed = roleAllowedIds.Contains(permission.PermissionId);
                        var hasOverride = overrideEffects.TryGetValue(permission.PermissionId, out var effect);

                        return new AccountOverrideItemDto
                        {
                            PermissionId = permission.PermissionId,
                            Code = permission.Code,
                            Name = permission.Name,
                            RoleAllowed = roleAllowed,
                            OverrideEffect = hasOverride ? effect : null,
                            Reason = overrideReasons.GetValueOrDefault(permission.PermissionId),
                            FinalAllowed = ResolveFinalAllowed(roleAllowed, hasOverride ? effect : null)
                        };
                    }).ToList()
                }).ToList()
            };

            return ServiceResult<AccountOverrideMatrixDto>.Success(matrix);
        }

        public async Task<ServiceResult> UpdateAccountOverridesAsync(
            int staffId,
            SaveAccountPermissionOverridesRequest request)
        {
            var staff = await _repository.GetStaffIdentityAsync(staffId);
            if (staff == null)
            {
                return ServiceResult.Failure("Staff not found.");
            }

            var normalized = NormalizeOverrides(request.Overrides);
            var permissionIds = normalized.Select(x => x.PermissionId).Distinct().ToList();
            var activePermissionIds = await _repository.GetActivePermissionIdsAsync(permissionIds);
            var invalidPermissionIds = permissionIds.Except(activePermissionIds).ToList();

            if (invalidPermissionIds.Any())
            {
                return ServiceResult.Failure(
                    "Some permissions are invalid or inactive.",
                    invalidPermissionIds.Select(x => x.ToString()).ToList(),
                    "INVALID_PERMISSION");
            }

            var invalidEffects = normalized
                .Where(x => x.Effect.HasValue &&
                    x.Effect.Value != PermissionEffect.Allow &&
                    x.Effect.Value != PermissionEffect.Deny)
                .ToList();

            if (invalidEffects.Any())
            {
                return ServiceResult.Failure("Invalid override effect.", errorCode: "INVALID_EFFECT");
            }

            await _repository.SaveAccountOverridesAsync(staff.AccountId, normalized);

            return ServiceResult.Success("Account permission overrides updated.");
        }

        public async Task<ServiceResult<PermissionDecisionDto>> HasPermissionAsync(
            int accountId,
            string permissionCode,
            int? targetStoreId = null)
        {
            var decision = new PermissionDecisionDto
            {
                AccountId = accountId,
                PermissionCode = permissionCode?.Trim() ?? string.Empty,
                TargetStoreId = targetStoreId
            };

            if (accountId <= 0)
            {
                decision.DenyReason = "Invalid account.";
                return ServiceResult<PermissionDecisionDto>.Success(decision);
            }

            if (string.IsNullOrWhiteSpace(permissionCode))
            {
                decision.DenyReason = "Permission code is required.";
                return ServiceResult<PermissionDecisionDto>.Success(decision);
            }

            var permission = await _repository.GetActivePermissionByCodeAsync(permissionCode);
            if (permission == null)
            {
                decision.DenyReason = "Permission not found or inactive.";
                return ServiceResult<PermissionDecisionDto>.Success(decision);
            }

            var facts = await _repository.GetAccountPermissionFactsAsync(accountId, permission.PermissionId);
            decision.StaffId = facts.StaffId;
            decision.RoleAllowed = facts.RoleAllowed;
            decision.OverrideEffect = facts.OverrideEffect;

            if (!facts.AccountExists)
            {
                decision.DenyReason = "Account not found.";
                return ServiceResult<PermissionDecisionDto>.Success(decision);
            }

            if (!facts.AccountActive)
            {
                decision.DenyReason = "Account is inactive.";
                return ServiceResult<PermissionDecisionDto>.Success(decision);
            }

            var permissionAllowed = ResolveFinalAllowed(facts.RoleAllowed, facts.OverrideEffect);
            if (!permissionAllowed)
            {
                decision.DenyReason = facts.OverrideEffect == PermissionEffect.Deny
                    ? "Denied by account override."
                    : "Permission is not granted by role.";

                return ServiceResult<PermissionDecisionDto>.Success(decision);
            }

            if (targetStoreId.HasValue)
            {
                if (!facts.StaffId.HasValue)
                {
                    decision.ScopeAllowed = false;
                    decision.DenyReason = "Account is not linked to staff scope.";
                    return ServiceResult<PermissionDecisionDto>.Success(decision);
                }

                decision.ScopeAllowed = await _scopeAuthorizationService
                    .CheckIfStoreIsWithinManagerScopeAsync(facts.StaffId.Value, targetStoreId.Value);

                if (!decision.ScopeAllowed)
                {
                    decision.DenyReason = "Store is outside staff scope.";
                    return ServiceResult<PermissionDecisionDto>.Success(decision);
                }
            }

            decision.Allowed = true;
            decision.ScopeAllowed = true;
            return ServiceResult<PermissionDecisionDto>.Success(decision);
        }

        private static bool ResolveFinalAllowed(bool roleAllowed, PermissionEffect? overrideEffect)
        {
            return overrideEffect switch
            {
                PermissionEffect.Allow => true,
                PermissionEffect.Deny => false,
                _ => roleAllowed
            };
        }

        private static List<int> NormalizeIds(IEnumerable<int>? ids)
        {
            return (ids ?? Enumerable.Empty<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToList();
        }

        private static List<StaffScopeInputDto> NormalizeScopes(IEnumerable<StaffScopeInputDto>? scopes)
        {
            return (scopes ?? Enumerable.Empty<StaffScopeInputDto>())
                .Where(x => x.ScopeTypeId > 0 && x.ScopeRefId > 0)
                .GroupBy(x => new { x.ScopeTypeId, x.ScopeRefId })
                .Select(x => new StaffScopeInputDto
                {
                    ScopeTypeId = x.Key.ScopeTypeId,
                    ScopeRefId = x.Key.ScopeRefId
                })
                .ToList();
        }

        private static List<AccountPermissionOverrideInputDto> NormalizeOverrides(
            IEnumerable<AccountPermissionOverrideInputDto>? overrides)
        {
            return (overrides ?? Enumerable.Empty<AccountPermissionOverrideInputDto>())
                .Where(x => x.PermissionId > 0)
                .GroupBy(x => x.PermissionId)
                .Select(x => x.Last())
                .ToList();
        }
    }
}
