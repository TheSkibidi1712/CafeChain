using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Constants;
using CafeChain.Data;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Security
{
    public class ScopeAuthorizationService : IScopeAuthorizationService
    {
        private readonly AppDbContext _context;

        public ScopeAuthorizationService(AppDbContext context)
        {
            _context = context;
        }

        public Task<List<Store>> GetAllowedStoresAsync(int currentStaffId) =>
            GetAllowedStoresAsync(currentStaffId, StoreScopePurpose.Default);

        public async Task<List<Store>> GetAllowedStoresAsync(
            int currentStaffId,
            StoreScopePurpose purpose)
        {
            if (currentStaffId <= 0)
            {
                return new List<Store>();
            }

            var isActiveSystemAdmin = await IsActiveSystemAdminAsync(currentStaffId);
            if (purpose == StoreScopePurpose.ReorderSuggestion
                && isActiveSystemAdmin)
            {
                return await _context.Stores
                    .AsNoTracking()
                    .Where(x => x.Active)
                    .OrderBy(x => x.Name)
                    .ToListAsync();
            }

            var scopes = await GetStaffScopesAsync(currentStaffId);
            if (purpose == StoreScopePurpose.Default
                && isActiveSystemAdmin)
            {
                // The seeded Country scope is an administrative configuration
                // scope, not a global business-data bypass. Narrow scopes remain
                // usable when they have been configured explicitly.
                scopes = scopes
                    .Where(x => x.ScopeTypeId != (int)ScopeLevel.Country)
                    .ToList();
            }

            if (!scopes.Any())
            {
                return new List<Store>();
            }

            var query = _context.Stores
                .AsNoTracking()
                .Where(x => x.Active);

            if (scopes.Any(x => x.ScopeTypeId == (int)ScopeLevel.Country))
            {
                return await query.OrderBy(x => x.Name).ToListAsync();
            }

            var provinceIds = GetScopeRefs(scopes, ScopeLevel.Province);
            var districtIds = GetScopeRefs(scopes, ScopeLevel.District);
            var wardIds = GetScopeRefs(scopes, ScopeLevel.Ward);
            var storeIds = GetScopeRefs(scopes, ScopeLevel.Store);

            return await query
                .Where(x =>
                    (x.ProvinceId.HasValue && provinceIds.Contains(x.ProvinceId.Value)) ||
                    (x.DistrictId.HasValue && districtIds.Contains(x.DistrictId.Value)) ||
                    (x.WardId.HasValue && wardIds.Contains(x.WardId.Value)) ||
                    storeIds.Contains(x.StoreId))
                .OrderBy(x => x.Name)
                .ToListAsync();
        }

        public Task<bool> CheckIfStoreIsWithinManagerScopeAsync(int currentStaffId, int targetStoreId)
        {
            return CanAccessStoreAsync(
                currentStaffId,
                targetStoreId,
                StoreScopePurpose.Default);
        }

        public Task<bool> CanAccessStoreAsync(int currentStaffId, int targetStoreId) =>
            CanAccessStoreAsync(
                currentStaffId,
                targetStoreId,
                StoreScopePurpose.Default);

        public async Task<bool> CanAccessStoreAsync(
            int currentStaffId,
            int targetStoreId,
            StoreScopePurpose purpose)
        {
            if (currentStaffId <= 0 || targetStoreId <= 0)
            {
                return false;
            }

            var allowedStores = await GetAllowedStoresAsync(currentStaffId, purpose);
            return allowedStores.Any(x => x.StoreId == targetStoreId);
        }

        private Task<List<StaffScope>> GetStaffScopesAsync(int staffId)
        {
            return _context.StaffScopes
                .AsNoTracking()
                .Where(x =>
                    x.StaffId == staffId
                    && x.Staff.Active
                    && x.Staff.Account.Active)
                .ToListAsync();
        }

        private Task<bool> IsActiveSystemAdminAsync(int staffId)
        {
            return _context.Staffs
                .AsNoTracking()
                .AnyAsync(x =>
                    x.StaffId == staffId
                    && x.Active
                    && x.Account.Active
                    && x.Account.AccountRoles.Any(ar =>
                        ar.Role.Active
                        && ar.Role.Name == RoleConstants.SystemAdmin));
        }

        private static List<int> GetScopeRefs(IEnumerable<StaffScope> scopes, ScopeLevel scopeLevel)
        {
            return scopes
                .Where(x => x.ScopeTypeId == (int)scopeLevel)
                .Select(x => x.ScopeRefId)
                .Distinct()
                .ToList();
        }
    }
}
