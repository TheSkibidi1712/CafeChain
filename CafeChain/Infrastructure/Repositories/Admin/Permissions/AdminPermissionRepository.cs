using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.Permissions;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Permissions;
using CafeChain.Models.Permissions;
using CafeChain.Models.Staffs;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastructure.Repositories.Admin.Permissions
{
    public class AdminPermissionRepository : IAdminPermissionRepository
    {
        private const int ScopeCountry = (int)ScopeLevel.Country;
        private const int ScopeProvince = (int)ScopeLevel.Province;
        private const int ScopeDistrict = (int)ScopeLevel.District;
        private const int ScopeWard = (int)ScopeLevel.Ward;
        private const int ScopeStore = (int)ScopeLevel.Store;

        private readonly AppDbContext _context;

        public AdminPermissionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<(List<AdminRoleListItemDto> Items, int TotalCount)> GetPagedRolesAsync(
            int pageIndex,
            int pageSize,
            string? search)
        {
            var query = _context.Roles.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim().ToLower();
                query = query.Where(x => x.Name.ToLower().Contains(keyword));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(x => x.RoleId)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AdminRoleListItemDto
                {
                    RoleId = x.RoleId,
                    Name = x.Name,
                    Active = x.Active,
                    IsStoreLevel = x.IsStoreLevel,
                    CreatedAt = x.CreatedAt,
                    UserCount = x.AccountRoles.Count,
                    PermissionCount = x.RolePermissions.Count(rp => rp.Permission.Active)
                })
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(List<AdminPermissionStaffListItemDto> Items, int TotalCount)> GetPagedStaffAsync(
            int pageIndex,
            int pageSize,
            string? search)
        {
            var query = _context.Staffs
                .AsNoTracking()
                .Where(x => x.Account != null)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var keyword = search.Trim().ToLower();
                query = query.Where(x =>
                    x.FullName.ToLower().Contains(keyword) ||
                    x.Account.Email.ToLower().Contains(keyword) ||
                    x.Store.Name.ToLower().Contains(keyword) ||
                    x.Account.AccountRoles.Any(ar => ar.Role.Name.ToLower().Contains(keyword)));
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderBy(x => x.FullName)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AdminPermissionStaffListItemDto
                {
                    StaffId = x.StaffId,
                    AccountId = x.AccountId,
                    FullName = x.FullName,
                    Email = x.Account.Email,
                    StoreName = x.Store.Name,
                    Active = x.Active && x.Account.Active,
                    RoleNames = x.Account.AccountRoles
                        .OrderBy(ar => ar.RoleId)
                        .Select(ar => ar.Role.Name)
                        .ToList()
                })
                .ToListAsync();

            return (items, totalCount);
        }

        public Task<List<ScopeReferenceDto>> GetScopeReferencesAsync(int scopeTypeId, int? parentId = null)
        {
            return scopeTypeId switch
            {
                ScopeCountry => _context.Countries
                    .AsNoTracking()
                    .OrderBy(x => x.Name)
                    .Select(x => new ScopeReferenceDto
                    {
                        Id = x.CountryId,
                        Name = x.Name
                    })
                    .ToListAsync(),

                ScopeProvince => _context.Provinces
                    .AsNoTracking()
                    .OrderBy(x => x.Name)
                    .Select(x => new ScopeReferenceDto
                    {
                        Id = x.ProvinceId,
                        Name = x.Name
                    })
                    .ToListAsync(),

                ScopeDistrict => _context.Districts
                    .AsNoTracking()
                    .Where(x => !parentId.HasValue || x.ProvinceId == parentId.Value)
                    .OrderBy(x => x.Name)
                    .Select(x => new ScopeReferenceDto
                    {
                        Id = x.DistrictId,
                        Name = x.Name
                    })
                    .ToListAsync(),

                ScopeWard => _context.Wards
                    .AsNoTracking()
                    .Where(x => !parentId.HasValue || x.DistrictId == parentId.Value)
                    .OrderBy(x => x.Name)
                    .Select(x => new ScopeReferenceDto
                    {
                        Id = x.WardId,
                        Name = x.Name
                    })
                    .ToListAsync(),

                ScopeStore => _context.Stores
                    .AsNoTracking()
                    .Where(x => x.Active)
                    .OrderBy(x => x.Name)
                    .Select(x => new ScopeReferenceDto
                    {
                        Id = x.StoreId,
                        Name = x.Name
                    })
                    .ToListAsync(),

                _ => Task.FromResult(new List<ScopeReferenceDto>())
            };
        }

        public Task<AdminRoleListItemDto?> GetRoleSummaryAsync(int roleId)
        {
            return _context.Roles
                .AsNoTracking()
                .Where(x => x.RoleId == roleId)
                .Select(x => new AdminRoleListItemDto
                {
                    RoleId = x.RoleId,
                    Name = x.Name,
                    Active = x.Active,
                    IsStoreLevel = x.IsStoreLevel,
                    CreatedAt = x.CreatedAt,
                    UserCount = x.AccountRoles.Count,
                    PermissionCount = x.RolePermissions.Count(rp => rp.Permission.Active)
                })
                .FirstOrDefaultAsync();
        }

        public async Task<List<PermissionCatalogGroupDto>> GetPermissionCatalogAsync(bool activeOnly = true)
        {
            var groups = await _context.PermissionGroups
                .AsNoTracking()
                .Where(x => !activeOnly || x.Active)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
                .Select(x => new PermissionCatalogGroupDto
                {
                    PermissionGroupId = x.PermissionGroupId,
                    Code = x.Code,
                    Name = x.Name,
                    DisplayOrder = x.DisplayOrder,
                    Permissions = x.Permissions
                        .Where(p => !activeOnly || p.Active)
                        .OrderBy(p => p.Code)
                        .Select(p => new PermissionCatalogItemDto
                        {
                            PermissionId = p.PermissionId,
                            PermissionGroupId = p.PermissionGroupId,
                            Code = p.Code,
                            Name = p.Name,
                            Description = p.Description,
                            Active = p.Active
                        })
                        .ToList()
                })
                .ToListAsync();

            return groups.Where(x => x.Permissions.Any()).ToList();
        }

        public Task<List<int>> GetRolePermissionIdsAsync(int roleId)
        {
            return _context.RolePermissions
                .AsNoTracking()
                .Where(x => x.RoleId == roleId && x.Permission.Active)
                .Select(x => x.PermissionId)
                .ToListAsync();
        }

        public async Task<List<int>> GetActivePermissionIdsAsync(IEnumerable<int> permissionIds)
        {
            var ids = permissionIds.Distinct().ToList();

            return await _context.Permissions
                .AsNoTracking()
                .Where(x => ids.Contains(x.PermissionId) && x.Active)
                .Select(x => x.PermissionId)
                .ToListAsync();
        }

        public Task<bool> RoleExistsAsync(int roleId)
        {
            return _context.Roles.AnyAsync(x => x.RoleId == roleId && x.Active);
        }

        public async Task ReplaceRolePermissionsAsync(int roleId, IEnumerable<int> permissionIds)
        {
            var distinctPermissionIds = permissionIds.Distinct().ToList();

            var ownsTransaction = _context.Database.CurrentTransaction == null;
            await using var transaction = ownsTransaction
                ? await _context.Database.BeginTransactionAsync()
                : null;
            try
            {
                var oldPermissions = await _context.RolePermissions
                    .Where(x => x.RoleId == roleId)
                    .ToListAsync();

                _context.RolePermissions.RemoveRange(oldPermissions);

                var newPermissions = distinctPermissionIds.Select(permissionId => new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permissionId
                });

                await _context.RolePermissions.AddRangeAsync(newPermissions);
                await _context.SaveChangesAsync();
                if (ownsTransaction)
                    await transaction!.CommitAsync();
            }
            catch
            {
                if (ownsTransaction)
                    await transaction!.RollbackAsync();
                throw;
            }
        }

        public Task<StaffPermissionIdentityDto?> GetStaffIdentityAsync(int staffId)
        {
            return _context.Staffs
                .AsNoTracking()
                .Where(x => x.StaffId == staffId)
                .Select(x => new StaffPermissionIdentityDto
                {
                    StaffId = x.StaffId,
                    AccountId = x.AccountId,
                    PrimaryStoreId = x.StoreId,
                    FullName = x.FullName,
                    Email = x.Account.Email,
                    StaffActive = x.Active,
                    AccountActive = x.Account.Active,
                    PrimaryRoleName = x.Account.AccountRoles
                        .OrderBy(ar => ar.RoleId)
                        .Select(ar => ar.Role.Name)
                        .FirstOrDefault() ?? string.Empty
                })
                .FirstOrDefaultAsync();
        }

        public Task<List<StaffRoleOptionDto>> GetRoleOptionsAsync(bool includeCustomer = false)
        {
            return _context.Roles
                .AsNoTracking()
                .Where(x => x.Active && (includeCustomer || x.Name != RoleConstants.Customer))
                .OrderBy(x => x.RoleId)
                .Select(x => new StaffRoleOptionDto
                {
                    RoleId = x.RoleId,
                    Name = x.Name,
                    Active = x.Active,
                    IsStoreLevel = x.IsStoreLevel
                })
                .ToListAsync();
        }

        public Task<List<int>> GetAssignedRoleIdsAsync(int accountId)
        {
            return _context.AccountRoles
                .AsNoTracking()
                .Where(x => x.AccountId == accountId && x.Role.Active)
                .Select(x => x.RoleId)
                .ToListAsync();
        }

        public async Task<List<int>> GetAssignableRoleIdsAsync(IEnumerable<int> roleIds)
        {
            var ids = roleIds.Distinct().ToList();

            return await _context.Roles
                .AsNoTracking()
                .Where(x => ids.Contains(x.RoleId) && x.Active && x.Name != RoleConstants.Customer)
                .Select(x => x.RoleId)
                .ToListAsync();
        }

        public async Task ReplaceAccountRolesAsync(int accountId, IEnumerable<int> roleIds)
        {
            var distinctRoleIds = roleIds.Distinct().ToList();

            var ownsTransaction = _context.Database.CurrentTransaction == null;
            await using var transaction = ownsTransaction
                ? await _context.Database.BeginTransactionAsync()
                : null;
            try
            {
                var oldRoles = await _context.AccountRoles
                    .Where(x => x.AccountId == accountId)
                    .ToListAsync();

                _context.AccountRoles.RemoveRange(oldRoles);

                var newRoles = distinctRoleIds.Select(roleId => new AccountRole
                {
                    AccountId = accountId,
                    RoleId = roleId
                });

                await _context.AccountRoles.AddRangeAsync(newRoles);
                await _context.SaveChangesAsync();
                if (ownsTransaction)
                    await transaction!.CommitAsync();
            }
            catch
            {
                if (ownsTransaction)
                    await transaction!.RollbackAsync();
                throw;
            }
        }

        public async Task<List<ScopeTypeOptionDto>> GetScopeTypesAsync()
        {
            var scopeTypes = await _context.ScopeTypes
                .AsNoTracking()
                .OrderBy(x => x.ScopeTypeId)
                .Select(x => new ScopeTypeOptionDto
                {
                    ScopeTypeId = x.ScopeTypeId,
                    Code = x.Code,
                    Name = x.Name
                })
                .ToListAsync();

            foreach (var scopeType in scopeTypes)
                scopeType.Name = ScopeTypeDisplayNames.FromCode(scopeType.Code);

            return scopeTypes;
        }

        public async Task<List<StaffScopeItemDto>> GetStaffScopesAsync(int staffId)
        {
            var scopes = await _context.StaffScopes
                .AsNoTracking()
                .Where(x => x.StaffId == staffId)
                .OrderBy(x => x.ScopeTypeId)
                .ThenBy(x => x.ScopeRefId)
                .Select(x => new StaffScopeItemDto
                {
                    StaffScopeId = x.StaffScopeId,
                    ScopeTypeId = x.ScopeTypeId,
                    ScopeTypeCode = x.ScopeType.Code,
                    ScopeTypeName = x.ScopeType.Name,
                    ScopeRefId = x.ScopeRefId
                })
                .ToListAsync();

            await FillScopeRefNamesAsync(scopes);

            foreach (var scope in scopes)
                scope.ScopeTypeName = ScopeTypeDisplayNames.FromCode(scope.ScopeTypeCode);

            return scopes;
        }

        public async Task<List<StaffScopeInputDto>> GetInvalidScopeRefsAsync(IEnumerable<StaffScopeInputDto> scopes)
        {
            var normalized = NormalizeScopes(scopes);
            var invalid = new List<StaffScopeInputDto>();

            foreach (var group in normalized.GroupBy(x => x.ScopeTypeId))
            {
                var refs = group.Select(x => x.ScopeRefId).ToList();
                List<int> existingRefs = group.Key switch
                {
                    ScopeCountry => await _context.Countries
                        .AsNoTracking()
                        .Where(x => refs.Contains(x.CountryId))
                        .Select(x => x.CountryId)
                        .ToListAsync(),

                    ScopeProvince => await _context.Provinces
                        .AsNoTracking()
                        .Where(x => refs.Contains(x.ProvinceId))
                        .Select(x => x.ProvinceId)
                        .ToListAsync(),

                    ScopeDistrict => await _context.Districts
                        .AsNoTracking()
                        .Where(x => refs.Contains(x.DistrictId))
                        .Select(x => x.DistrictId)
                        .ToListAsync(),

                    ScopeWard => await _context.Wards
                        .AsNoTracking()
                        .Where(x => refs.Contains(x.WardId))
                        .Select(x => x.WardId)
                        .ToListAsync(),

                    ScopeStore => await _context.Stores
                        .AsNoTracking()
                        .Where(x => refs.Contains(x.StoreId) && x.Active)
                        .Select(x => x.StoreId)
                        .ToListAsync(),

                    _ => new List<int>()
                };

                invalid.AddRange(group.Where(x => !existingRefs.Contains(x.ScopeRefId)));
            }

            return invalid;
        }

        public async Task<bool> ScopesCoverStoreAsync(
            IEnumerable<StaffScopeInputDto> scopes,
            int storeId)
        {
            var normalized = NormalizeScopes(scopes);
            var store = await _context.Stores
                .AsNoTracking()
                .Include(x => x.Province)
                .Include(x => x.District).ThenInclude(x => x!.Province)
                .Include(x => x.Ward).ThenInclude(x => x!.District).ThenInclude(x => x!.Province)
                .FirstOrDefaultAsync(x => x.StoreId == storeId);
            if (store == null) return false;

            var wardId = store.WardId;
            var districtId = store.DistrictId ?? store.Ward?.DistrictId;
            var provinceId = store.ProvinceId
                ?? store.District?.ProvinceId
                ?? store.Ward?.District?.ProvinceId;
            var countryId = store.Province?.CountryId
                ?? store.District?.Province?.CountryId
                ?? store.Ward?.District?.Province?.CountryId;

            return normalized.Any(x =>
                (x.ScopeTypeId == ScopeCountry && x.ScopeRefId == countryId)
                || (x.ScopeTypeId == ScopeProvince && x.ScopeRefId == provinceId)
                || (x.ScopeTypeId == ScopeDistrict && x.ScopeRefId == districtId)
                || (x.ScopeTypeId == ScopeWard && x.ScopeRefId == wardId)
                || (x.ScopeTypeId == ScopeStore && x.ScopeRefId == storeId));
        }

        public async Task ReplaceStaffScopesAsync(int staffId, IEnumerable<StaffScopeInputDto> scopes)
        {
            var normalized = NormalizeScopes(scopes);

            var ownsTransaction = _context.Database.CurrentTransaction == null;
            await using var transaction = ownsTransaction
                ? await _context.Database.BeginTransactionAsync()
                : null;
            try
            {
                var oldScopes = await _context.StaffScopes
                    .Where(x => x.StaffId == staffId)
                    .ToListAsync();

                _context.StaffScopes.RemoveRange(oldScopes);

                var newScopes = normalized.Select(x => new StaffScope
                {
                    StaffId = staffId,
                    ScopeTypeId = x.ScopeTypeId,
                    ScopeRefId = x.ScopeRefId
                });

                await _context.StaffScopes.AddRangeAsync(newScopes);
                await _context.SaveChangesAsync();
                if (ownsTransaction)
                    await transaction!.CommitAsync();
            }
            catch
            {
                if (ownsTransaction)
                    await transaction!.RollbackAsync();
                throw;
            }
        }

        public Task<Dictionary<int, PermissionEffect>> GetAccountOverrideEffectsAsync(int accountId)
        {
            return _context.AccountPermissionOverrides
                .AsNoTracking()
                .Where(x => x.AccountId == accountId)
                .ToDictionaryAsync(x => x.PermissionId, x => x.Effect);
        }

        public Task<Dictionary<int, string?>> GetAccountOverrideReasonsAsync(int accountId)
        {
            return _context.AccountPermissionOverrides
                .AsNoTracking()
                .Where(x => x.AccountId == accountId)
                .ToDictionaryAsync(x => x.PermissionId, x => x.Reason);
        }

        public Task<List<int>> GetRoleAllowedPermissionIdsForAccountAsync(int accountId)
        {
            return _context.RolePermissions
                .AsNoTracking()
                .Where(x =>
                    x.Permission.Active &&
                    x.Role.Active &&
                    x.Role.AccountRoles.Any(ar => ar.AccountId == accountId))
                .Select(x => x.PermissionId)
                .Distinct()
                .ToListAsync();
        }

        public async Task SaveAccountOverridesAsync(
            int accountId,
            IEnumerable<AccountPermissionOverrideInputDto> overrides)
        {
            var normalized = overrides
                .GroupBy(x => x.PermissionId)
                .Select(x => x.Last())
                .ToList();

            var permissionIds = normalized.Select(x => x.PermissionId).Distinct().ToList();

            var ownsTransaction = _context.Database.CurrentTransaction == null;
            await using var transaction = ownsTransaction
                ? await _context.Database.BeginTransactionAsync()
                : null;
            try
            {
                var oldOverrides = await _context.AccountPermissionOverrides
                    .Where(x => x.AccountId == accountId && permissionIds.Contains(x.PermissionId))
                    .ToListAsync();

                foreach (var input in normalized)
                {
                    var existing = oldOverrides.FirstOrDefault(x => x.PermissionId == input.PermissionId);

                    if (!input.Effect.HasValue)
                    {
                        if (existing != null)
                        {
                            _context.AccountPermissionOverrides.Remove(existing);
                        }

                        continue;
                    }

                    if (existing == null)
                    {
                        await _context.AccountPermissionOverrides.AddAsync(new AccountPermissionOverride
                        {
                            AccountId = accountId,
                            PermissionId = input.PermissionId,
                            Effect = input.Effect.Value,
                            Reason = input.Reason
                        });
                    }
                    else
                    {
                        existing.Effect = input.Effect.Value;
                        existing.Reason = input.Reason;
                    }
                }

                await _context.SaveChangesAsync();
                if (ownsTransaction)
                    await transaction!.CommitAsync();
            }
            catch
            {
                if (ownsTransaction)
                    await transaction!.RollbackAsync();
                throw;
            }
        }

        public Task<Permission?> GetActivePermissionByCodeAsync(string permissionCode)
        {
            var normalizedCode = permissionCode.Trim();

            return _context.Permissions
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == normalizedCode && x.Active);
        }

        public async Task<AccountPermissionFactsDto> GetAccountPermissionFactsAsync(int accountId, int permissionId)
        {
            var account = await _context.Accounts
                .AsNoTracking()
                .Where(x => x.AccountId == accountId)
                .Select(x => new
                {
                    x.AccountId,
                    x.Active,
                    StaffId = x.Staff != null ? (int?)x.Staff.StaffId : null
                })
                .FirstOrDefaultAsync();

            if (account == null)
            {
                return new AccountPermissionFactsDto();
            }

            var roleAllowed = await _context.RolePermissions
                .AsNoTracking()
                .AnyAsync(x =>
                    x.PermissionId == permissionId &&
                    x.Permission.Active &&
                    x.Role.Active &&
                    x.Role.AccountRoles.Any(ar => ar.AccountId == accountId));

            var overrideEffect = await _context.AccountPermissionOverrides
                .AsNoTracking()
                .Where(x => x.AccountId == accountId && x.PermissionId == permissionId)
                .Select(x => (PermissionEffect?)x.Effect)
                .FirstOrDefaultAsync();

            return new AccountPermissionFactsDto
            {
                AccountExists = true,
                AccountActive = account.Active,
                StaffId = account.StaffId,
                RoleAllowed = roleAllowed,
                OverrideEffect = overrideEffect
            };
        }

        public async Task<HashSet<string>> GetEffectivePermissionCodesAsync(int accountId)
        {
            var accountActive = await _context.Accounts.AsNoTracking()
                .AnyAsync(x => x.AccountId == accountId && x.Active);
            if (!accountActive) return new HashSet<string>(StringComparer.Ordinal);

            var roleCodes = await _context.RolePermissions.AsNoTracking()
                .Where(x => x.Permission.Active && x.Role.Active
                    && x.Role.AccountRoles.Any(ar => ar.AccountId == accountId))
                .Select(x => x.Permission.Code)
                .Distinct()
                .ToListAsync();
            var overrides = await _context.AccountPermissionOverrides.AsNoTracking()
                .Where(x => x.AccountId == accountId && x.Permission.Active)
                .Select(x => new { x.Permission.Code, x.Effect })
                .ToListAsync();

            var result = new HashSet<string>(roleCodes, StringComparer.Ordinal);
            foreach (var item in overrides)
            {
                if (item.Effect == PermissionEffect.Allow) result.Add(item.Code);
                else if (item.Effect == PermissionEffect.Deny) result.Remove(item.Code);
            }
            return result;
        }

        private async Task FillScopeRefNamesAsync(List<StaffScopeItemDto> scopes)
        {
            if (!scopes.Any())
            {
                return;
            }

            var countryIds = scopes.Where(x => x.ScopeTypeId == ScopeCountry).Select(x => x.ScopeRefId).Distinct().ToList();
            var provinceIds = scopes.Where(x => x.ScopeTypeId == ScopeProvince).Select(x => x.ScopeRefId).Distinct().ToList();
            var districtIds = scopes.Where(x => x.ScopeTypeId == ScopeDistrict).Select(x => x.ScopeRefId).Distinct().ToList();
            var wardIds = scopes.Where(x => x.ScopeTypeId == ScopeWard).Select(x => x.ScopeRefId).Distinct().ToList();
            var storeIds = scopes.Where(x => x.ScopeTypeId == ScopeStore).Select(x => x.ScopeRefId).Distinct().ToList();

            var countries = await _context.Countries.AsNoTracking()
                .Where(x => countryIds.Contains(x.CountryId))
                .ToDictionaryAsync(x => x.CountryId, x => x.Name);

            var provinces = await _context.Provinces.AsNoTracking()
                .Where(x => provinceIds.Contains(x.ProvinceId))
                .ToDictionaryAsync(x => x.ProvinceId, x => x.Name);

            var districts = await _context.Districts.AsNoTracking()
                .Where(x => districtIds.Contains(x.DistrictId))
                .ToDictionaryAsync(x => x.DistrictId, x => x.Name);

            var wards = await _context.Wards.AsNoTracking()
                .Where(x => wardIds.Contains(x.WardId))
                .ToDictionaryAsync(x => x.WardId, x => x.Name);

            var stores = await _context.Stores.AsNoTracking()
                .Where(x => storeIds.Contains(x.StoreId))
                .ToDictionaryAsync(x => x.StoreId, x => x.Name);

            foreach (var scope in scopes)
            {
                scope.ScopeRefName = scope.ScopeTypeId switch
                {
                    ScopeCountry => countries.GetValueOrDefault(scope.ScopeRefId) ?? string.Empty,
                    ScopeProvince => provinces.GetValueOrDefault(scope.ScopeRefId) ?? string.Empty,
                    ScopeDistrict => districts.GetValueOrDefault(scope.ScopeRefId) ?? string.Empty,
                    ScopeWard => wards.GetValueOrDefault(scope.ScopeRefId) ?? string.Empty,
                    ScopeStore => stores.GetValueOrDefault(scope.ScopeRefId) ?? string.Empty,
                    _ => string.Empty
                };
            }
        }

        private static List<StaffScopeInputDto> NormalizeScopes(IEnumerable<StaffScopeInputDto> scopes)
        {
            return scopes
                .Where(x => x.ScopeTypeId > 0 && x.ScopeRefId > 0)
                .GroupBy(x => new { x.ScopeTypeId, x.ScopeRefId })
                .Select(x => new StaffScopeInputDto
                {
                    ScopeTypeId = x.Key.ScopeTypeId,
                    ScopeRefId = x.Key.ScopeRefId
                })
                .ToList();
        }
    }
}
