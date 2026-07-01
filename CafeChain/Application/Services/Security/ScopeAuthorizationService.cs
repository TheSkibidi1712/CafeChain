using CafeChain.Application.Interfaces.Security;
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

        public async Task<List<Store>> GetAllowedStoresAsync(int currentStaffId)
        {
            var scopes = await GetStaffScopesAsync(currentStaffId);
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
            return CanAccessStoreAsync(currentStaffId, targetStoreId);
        }

        public async Task<bool> CanAccessStoreAsync(int currentStaffId, int targetStoreId)
        {
            if (currentStaffId <= 0 || targetStoreId <= 0)
            {
                return false;
            }

            var scopes = await GetStaffScopesAsync(currentStaffId);
            if (!scopes.Any())
            {
                return false;
            }

            var store = await _context.Stores
                .AsNoTracking()
                .Where(x => x.StoreId == targetStoreId && x.Active)
                .Select(x => new
                {
                    x.StoreId,
                    x.ProvinceId,
                    x.DistrictId,
                    x.WardId
                })
                .FirstOrDefaultAsync();

            if (store == null)
            {
                return false;
            }

            if (scopes.Any(x => x.ScopeTypeId == (int)ScopeLevel.Country))
            {
                return true;
            }

            return scopes.Any(scope => scope.ScopeTypeId switch
            {
                (int)ScopeLevel.Province => store.ProvinceId == scope.ScopeRefId,
                (int)ScopeLevel.District => store.DistrictId == scope.ScopeRefId,
                (int)ScopeLevel.Ward => store.WardId == scope.ScopeRefId,
                (int)ScopeLevel.Store => store.StoreId == scope.ScopeRefId,
                _ => false
            });
        }

        private Task<List<StaffScope>> GetStaffScopesAsync(int staffId)
        {
            return _context.StaffScopes
                .AsNoTracking()
                .Where(x => x.StaffId == staffId)
                .ToListAsync();
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
