using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Infrastructure.Interfaces.Admin.Permissions;
using CafeChain.Models.Enums.Permissions;
using System.Security.Claims;
using CafeChain.Application.Constants;

namespace CafeChain.Application.Services.Admin.Permissions
{
    public class AdminPermissionService : IAdminPermissionService
    {
        private const int DefaultPageIndex = 1;
        private const int DefaultPageSize = 10;
        private const int MaxPageSize = 100;

        private readonly IAdminPermissionRepository _repository;
        private readonly IScopeAuthorizationService _scopeAuthorizationService;
        private readonly Dictionary<int, HashSet<string>> _effectivePermissionCache = new();

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

        public async Task<ServiceResult<AdminPermissionStaffPagedResultDto>> GetStaffAsync(
            int pageIndex,
            int pageSize,
            string? search)
        {
            pageIndex = pageIndex < 1 ? DefaultPageIndex : pageIndex;
            pageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

            var (items, totalCount) = await _repository.GetPagedStaffAsync(pageIndex, pageSize, search);

            return ServiceResult<AdminPermissionStaffPagedResultDto>.Success(new AdminPermissionStaffPagedResultDto
            {
                Items = items,
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }

        public async Task<ServiceResult<List<ScopeReferenceDto>>> GetScopeReferencesAsync(
            int scopeTypeId,
            ClaimsPrincipal actor,
            int? parentId = null)
        {
            if (scopeTypeId <= 0)
            {
                return ServiceResult<List<ScopeReferenceDto>>.Failure("Scope type is required.");
            }

            var references = await _repository.GetScopeReferencesAsync(scopeTypeId, parentId);
            var isGlobalActor = actor.IsInRole(RoleConstants.BusinessOwner)
                || actor.IsInRole(RoleConstants.SystemAdmin);
            if (!isGlobalActor)
            {
                if (!int.TryParse(actor.FindFirstValue("StaffId"), out var actorStaffId)
                    || actorStaffId <= 0)
                    return ServiceResult<List<ScopeReferenceDto>>.Success(new List<ScopeReferenceDto>());

                var stores = await _scopeAuthorizationService.GetAllowedStoresAsync(actorStaffId);
                var allowedReferenceIds = scopeTypeId switch
                {
                    (int)ScopeLevel.Province => stores.Where(x => x.ProvinceId.HasValue)
                        .Select(x => x.ProvinceId!.Value).ToHashSet(),
                    (int)ScopeLevel.District => stores.Where(x => x.DistrictId.HasValue)
                        .Select(x => x.DistrictId!.Value).ToHashSet(),
                    (int)ScopeLevel.Ward => stores.Where(x => x.WardId.HasValue)
                        .Select(x => x.WardId!.Value).ToHashSet(),
                    (int)ScopeLevel.Store => stores.Select(x => x.StoreId).ToHashSet(),
                    _ => new HashSet<int>()
                };
                references = references.Where(x => allowedReferenceIds.Contains(x.Id)).ToList();
            }

            return ServiceResult<List<ScopeReferenceDto>>.Success(references);
        }

        public async Task<ServiceResult<List<PermissionCatalogGroupDto>>> GetPermissionCatalogAsync()
        {
            var catalog = await _repository.GetPermissionCatalogAsync();
            return ServiceResult<List<PermissionCatalogGroupDto>>.Success(catalog);
        }

        public async Task<ServiceResult<RolePermissionMatrixDto>> GetRolePermissionsAsync(int roleId, ClaimsPrincipal actor)
        {
            var role = await _repository.GetRoleSummaryAsync(roleId);
            if (role == null)
            {
                return ServiceResult<RolePermissionMatrixDto>.Failure("Role not found.");
            }

            var groups = await _repository.GetPermissionCatalogAsync();
            var grantedIds = (await _repository.GetRolePermissionIdsAsync(roleId)).ToHashSet();
            var actorCodes = await GetActorEffectiveCodesAsync(actor);
            var canManageTargetRole = (actor.IsInRole(RoleConstants.BusinessOwner)
                    || actor.IsInRole(RoleConstants.SystemAdmin))
                && !((actor.IsInRole(RoleConstants.BusinessOwner) && role.Name == RoleConstants.SystemAdmin)
                    || (actor.IsInRole(RoleConstants.SystemAdmin) && role.Name == RoleConstants.BusinessOwner));

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
                        IsGranted = grantedIds.Contains(permission.PermissionId),
                        CanChange = canManageTargetRole && actorCodes.Contains(permission.Code),
                        ReadOnlyReason = !canManageTargetRole
                            ? "Không được thay đổi vai trò cấp cao chéo."
                            : actorCodes.Contains(permission.Code) ? null : "Bạn không có quyền này nên không thể thay đổi."
                    }).ToList()
                }).ToList()
            };

            return ServiceResult<RolePermissionMatrixDto>.Success(matrix);
        }

        public async Task<ServiceResult> UpdateRolePermissionsAsync(int roleId, SaveRolePermissionsRequest request, ClaimsPrincipal actor)
        {
            var actorGuard = await EnsureActorCanManageAsync(actor);
            if (actorGuard != null) return actorGuard;
            var targetRole = await _repository.GetRoleSummaryAsync(roleId);
            if (targetRole == null || !targetRole.Active)
            {
                return ServiceResult.Failure("Role not found or inactive.");
            }
            if (!actor.IsInRole(RoleConstants.BusinessOwner)
                && !actor.IsInRole(RoleConstants.SystemAdmin))
                return ServiceResult.Failure(
                    "Chỉ Chủ doanh nghiệp hoặc Quản trị hệ thống được thay đổi quyền vai trò.",
                    errorCode: "PRIVILEGE_ESCALATION");
            if ((actor.IsInRole(RoleConstants.BusinessOwner) && targetRole.Name == RoleConstants.SystemAdmin)
                || (actor.IsInRole(RoleConstants.SystemAdmin) && targetRole.Name == RoleConstants.BusinessOwner))
                return ServiceResult.Failure(
                    "Không được thay đổi quyền của vai trò cấp cao chéo.",
                    errorCode: "PRIVILEGE_ESCALATION");

            var requestedIds = NormalizeIds(request.PermissionIds);
            var currentIds = (await _repository.GetRolePermissionIdsAsync(roleId)).ToHashSet();
            var changedIds = new HashSet<int>(currentIds);
            changedIds.SymmetricExceptWith(requestedIds);
            var grantGuard = await EnsureActorCanGrantPermissionsAsync(actor, changedIds);
            if (grantGuard != null) return grantGuard;
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

        public async Task<ServiceResult<StaffRolesDto>> GetStaffRolesAsync(int staffId, ClaimsPrincipal actor)
        {
            var staff = await _repository.GetStaffIdentityAsync(staffId);
            if (staff == null)
            {
                return ServiceResult<StaffRolesDto>.Failure("Staff not found.");
            }

            var assignedIds = (await _repository.GetAssignedRoleIdsAsync(staff.AccountId)).ToHashSet();
            var roles = await _repository.GetRoleOptionsAsync();
            var targetGuard = await EnsureActorCanManageTargetAsync(actor, staff.AccountId);
            var canManageTarget = targetGuard == null;
            var actorRank = GetActorRoleRank(actor);

            foreach (var role in roles)
            {
                role.IsAssigned = assignedIds.Contains(role.RoleId);
                role.CanChange = canManageTarget
                    && role.Name != RoleConstants.Customer
                    && GetRoleRank(role.Name) > actorRank;
                role.ReadOnlyReason = role.CanChange
                    ? null
                    : targetGuard?.Message ?? "Chỉ được gán vai trò thấp hơn vai trò của người thao tác.";
            }

            return ServiceResult<StaffRolesDto>.Success(new StaffRolesDto
            {
                StaffId = staff.StaffId,
                AccountId = staff.AccountId,
                FullName = staff.FullName,
                Email = staff.Email,
                CanChange = canManageTarget,
                ReadOnlyReason = targetGuard?.Message,
                Roles = roles
            });
        }

        public async Task<ServiceResult> UpdateStaffRolesAsync(int staffId, SaveStaffRolesRequest request, ClaimsPrincipal actor)
        {
            var actorGuard = await EnsureActorCanManageAsync(actor);
            if (actorGuard != null) return actorGuard;
            var staff = await _repository.GetStaffIdentityAsync(staffId);
            if (staff == null)
            {
                return ServiceResult.Failure("Staff not found.");
            }

            if (!staff.AccountActive)
            {
                return ServiceResult.Failure("Account is inactive.");
            }
            var targetGuard = await EnsureActorCanManageTargetAsync(actor, staff.AccountId);
            if (targetGuard != null) return targetGuard;

            var requestedIds = NormalizeIds(request.RoleIds);
            if (!requestedIds.Any())
            {
                return ServiceResult.Failure("At least one role is required.", errorCode: "ROLE_REQUIRED");
            }

            var roleOptions = await _repository.GetRoleOptionsAsync(includeCustomer: true);
            var requestedNames = roleOptions.Where(x => requestedIds.Contains(x.RoleId)).Select(x => x.Name).ToHashSet();
            var currentIds = await _repository.GetAssignedRoleIdsAsync(staff.AccountId);
            var currentNames = roleOptions.Where(x => currentIds.Contains(x.RoleId)).Select(x => x.Name).ToHashSet();
            if (requestedNames.Contains(RoleConstants.Customer)
                || (actor.IsInRole(RoleConstants.BusinessOwner) && requestedNames.Contains(RoleConstants.SystemAdmin))
                || (actor.IsInRole(RoleConstants.SystemAdmin) && requestedNames.Contains(RoleConstants.BusinessOwner))
                || (actor.IsInRole(RoleConstants.BusinessOwner) && currentNames.Contains(RoleConstants.SystemAdmin))
                || (actor.IsInRole(RoleConstants.SystemAdmin) && currentNames.Contains(RoleConstants.BusinessOwner))
                || !CanAssignRequestedRoles(actor, requestedNames))
                return ServiceResult.Failure(
                    "Không được gán vai trò Khách hàng hoặc vai trò cấp cao chéo.",
                    errorCode: "PRIVILEGE_ESCALATION");

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

        public async Task<ServiceResult<StaffScopesDto>> GetStaffScopesAsync(int staffId, ClaimsPrincipal actor)
        {
            var staff = await _repository.GetStaffIdentityAsync(staffId);
            if (staff == null)
            {
                return ServiceResult<StaffScopesDto>.Failure("Staff not found.");
            }

            var targetGuard = await EnsureActorCanManageTargetAsync(actor, staff.AccountId);
            return ServiceResult<StaffScopesDto>.Success(new StaffScopesDto
            {
                StaffId = staff.StaffId,
                AccountId = staff.AccountId,
                FullName = staff.FullName,
                CanChange = targetGuard == null,
                ReadOnlyReason = targetGuard?.Message,
                ScopeTypes = await _repository.GetScopeTypesAsync(),
                Scopes = await _repository.GetStaffScopesAsync(staffId)
            });
        }

        public async Task<ServiceResult> UpdateStaffScopesAsync(int staffId, SaveStaffScopesRequest request, ClaimsPrincipal actor)
        {
            var actorGuard = await EnsureActorCanManageAsync(actor);
            if (actorGuard != null) return actorGuard;
            var staff = await _repository.GetStaffIdentityAsync(staffId);
            if (staff == null)
            {
                return ServiceResult.Failure("Staff not found.");
            }
            var targetGuard = await EnsureActorCanManageTargetAsync(actor, staff.AccountId);
            if (targetGuard != null) return targetGuard;

            var scopes = NormalizeScopes(request.Scopes);
            if (!actor.IsInRole(RoleConstants.BusinessOwner) && !actor.IsInRole(RoleConstants.SystemAdmin))
            {
                var actorStaffId = int.TryParse(actor.FindFirstValue("StaffId"), out var parsedStaffId)
                    ? parsedStaffId : 0;
                var allowedStoreIds = (await _scopeAuthorizationService.GetAllowedStoresAsync(actorStaffId))
                    .Select(x => x.StoreId).ToHashSet();
                if (scopes.Any(x => x.ScopeTypeId != 5 || !allowedStoreIds.Contains(x.ScopeRefId)))
                    return ServiceResult.Failure(
                        "Không được cấp phạm vi vượt quá phạm vi của người thao tác.",
                        errorCode: "SCOPE_ESCALATION");
            }
            if (!await _repository.ScopesCoverStoreAsync(scopes, staff.PrimaryStoreId))
                return ServiceResult.Failure(
                    "Cửa hàng làm việc chính phải thuộc phạm vi được cấp.",
                    errorCode: "PRIMARY_STORE_OUT_OF_SCOPE");
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

        public async Task<ServiceResult<AccountOverrideMatrixDto>> GetAccountOverridesAsync(int staffId, ClaimsPrincipal actor)
        {
            var staff = await _repository.GetStaffIdentityAsync(staffId);
            if (staff == null)
            {
                return ServiceResult<AccountOverrideMatrixDto>.Failure("Staff not found.");
            }

            var targetGuard = await EnsureActorCanManageTargetAsync(actor, staff.AccountId);
            if (targetGuard != null)
                return ServiceResult<AccountOverrideMatrixDto>.Failure(
                    targetGuard.Message, targetGuard.Errors, targetGuard.ErrorCode);
            var actorCodes = await GetActorEffectiveCodesAsync(actor);

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
                            Description = permission.Description,
                            RoleAllowed = roleAllowed,
                            OverrideEffect = hasOverride ? effect : null,
                            Reason = overrideReasons.GetValueOrDefault(permission.PermissionId),
                            FinalAllowed = ResolveFinalAllowed(roleAllowed, hasOverride ? effect : null),
                            CanChange = actorCodes.Contains(permission.Code),
                            ReadOnlyReason = actorCodes.Contains(permission.Code)
                                ? null : "Bạn không có quyền này nên không thể thay đổi."
                        };
                    }).ToList()
                }).ToList()
            };

            return ServiceResult<AccountOverrideMatrixDto>.Success(matrix);
        }

        public async Task<ServiceResult> UpdateAccountOverridesAsync(
            int staffId,
            SaveAccountPermissionOverridesRequest request,
            ClaimsPrincipal actor)
        {
            var actorGuard = await EnsureActorCanManageAsync(actor);
            if (actorGuard != null) return actorGuard;
            var staff = await _repository.GetStaffIdentityAsync(staffId);
            if (staff == null)
            {
                return ServiceResult.Failure("Staff not found.");
            }
            var targetGuard = await EnsureActorCanManageTargetAsync(actor, staff.AccountId);
            if (targetGuard != null) return targetGuard;

            var normalized = NormalizeOverrides(request.Overrides);
            var permissionIds = normalized.Select(x => x.PermissionId).Distinct().ToList();
            var currentEffects = await _repository.GetAccountOverrideEffectsAsync(staff.AccountId);
            var requestedEffects = normalized.ToDictionary(x => x.PermissionId, x => x.Effect);
            var changedIds = currentEffects.Keys.Union(requestedEffects.Keys)
                .Where(id => currentEffects.GetValueOrDefault(id) != requestedEffects.GetValueOrDefault(id))
                .ToList();
            var grantGuard = await EnsureActorCanGrantPermissionsAsync(actor, changedIds);
            if (grantGuard != null) return grantGuard;
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

        public async Task<ServiceResult<HashSet<string>>> GetEffectivePermissionCodesAsync(int accountId)
        {
            if (accountId <= 0)
                return ServiceResult<HashSet<string>>.Success(new HashSet<string>(StringComparer.Ordinal));
            if (!_effectivePermissionCache.TryGetValue(accountId, out var permissions))
            {
                permissions = await _repository.GetEffectivePermissionCodesAsync(accountId);
                _effectivePermissionCache[accountId] = permissions;
            }
            return ServiceResult<HashSet<string>>.Success(permissions);
        }

        private async Task<ServiceResult?> EnsureActorCanManageAsync(ClaimsPrincipal actor)
        {
            var accountId = int.TryParse(actor.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed)
                ? parsed : 0;
            var permissions = (await GetEffectivePermissionCodesAsync(accountId)).Data;
            return permissions?.Contains(PermissionConstants.SystemPermissionManage) == true
                ? null
                : ServiceResult.Failure("Bạn không có quyền quản lý phân quyền.", errorCode: "PERMISSION_REQUIRED");
        }

        private static bool CanAssignRequestedRoles(
            ClaimsPrincipal actor,
            IReadOnlySet<string> requestedNames)
        {
            var actorRank = GetActorRoleRank(actor);
            return actorRank < int.MaxValue
                && requestedNames.Count > 0
                && requestedNames.All(name => name != RoleConstants.Customer
                    && GetRoleRank(name) > actorRank);
        }

        private async Task<ServiceResult?> EnsureActorCanManageTargetAsync(
            ClaimsPrincipal actor,
            int targetAccountId)
        {
            var assignedIds = await _repository.GetAssignedRoleIdsAsync(targetAccountId);
            var roles = await _repository.GetRoleOptionsAsync(includeCustomer: true);
            var names = roles.Where(x => assignedIds.Contains(x.RoleId)).Select(x => x.Name).ToHashSet();
            var crossHighRole = names.Select(GetRoleRank).DefaultIfEmpty(int.MaxValue).Min()
                <= GetActorRoleRank(actor);
            return crossHighRole
                ? ServiceResult.Failure(
                    "Không được thay đổi quyền hoặc phạm vi của tài khoản cấp cao hơn/ngang hàng.",
                    errorCode: "PRIVILEGE_ESCALATION")
                : null;
        }

        private static int GetActorRoleRank(ClaimsPrincipal actor) => actor.Claims
            .Where(claim => claim.Type == ClaimTypes.Role)
            .Select(claim => GetRoleRank(claim.Value))
            .DefaultIfEmpty(int.MaxValue)
            .Min();

        private static int GetRoleRank(string roleName) => roleName switch
        {
            RoleConstants.BusinessOwner or RoleConstants.SystemAdmin => 0,
            RoleConstants.AreaManager => 10,
            RoleConstants.StoreManager => 20,
            RoleConstants.AccountantWarehouse => 30,
            RoleConstants.ShiftSupervisor => 40,
            RoleConstants.SalesStaff => 50,
            RoleConstants.Customer => 100,
            _ => int.MaxValue
        };

        private async Task<ServiceResult?> EnsureActorCanGrantPermissionsAsync(
            ClaimsPrincipal actor,
            IEnumerable<int> requestedPermissionIds)
        {
            var requested = requestedPermissionIds.Distinct().ToHashSet();
            if (requested.Count == 0) return null;
            var accountId = int.TryParse(actor.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed)
                ? parsed : 0;
            var effectiveCodes = (await GetEffectivePermissionCodesAsync(accountId)).Data
                ?? new HashSet<string>(StringComparer.Ordinal);
            var catalog = await _repository.GetPermissionCatalogAsync();
            var forbidden = catalog.SelectMany(x => x.Permissions)
                .Where(x => requested.Contains(x.PermissionId) && !effectiveCodes.Contains(x.Code))
                .Select(x => x.Code).ToList();
            return forbidden.Count == 0
                ? null
                : ServiceResult.Failure(
                    "Không được cấp quyền mà chính người thao tác không có.",
                    forbidden,
                    "PRIVILEGE_ESCALATION");
        }

        private async Task<HashSet<string>> GetActorEffectiveCodesAsync(ClaimsPrincipal actor)
        {
            var accountId = int.TryParse(actor.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed)
                ? parsed : 0;
            return (await GetEffectivePermissionCodesAsync(accountId)).Data
                ?? new HashSet<string>(StringComparer.Ordinal);
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
