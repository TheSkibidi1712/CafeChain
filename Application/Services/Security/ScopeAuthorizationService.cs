using CafeChain.Application.Interfaces.Security;
using CafeChain.Data;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            var staff = await _context.Staffs
                .Include(s => s.StaffScopes)
                .FirstOrDefaultAsync(s => s.StaffId == currentStaffId);

            if (staff == null) return new List<Store>();

            var query = _context.Stores.Where(s => s.Active);

            // 1. Kiểm tra cấp độ vùng cao nhất (HQ)
            if (staff.StaffScopes.Any(s => s.ScopeTypeId == (int)ScopeLevel.HQ))
            {
                return await query.ToListAsync();
            }

            // 2. Chế độ Hỗ trợ ĐA Tỉnh (Multiple Provinces)
            var provinceIds = staff.StaffScopes
                .Where(s => s.ScopeTypeId == (int)ScopeLevel.Province)
                .Select(s => s.ScopeRefId)
                .ToList();
            if (provinceIds.Any())
            {
                return await query.Where(s => s.Ward.ProvinceId.HasValue && provinceIds.Contains(s.Ward.ProvinceId.Value)).ToListAsync();
            }

            // 3. Chế độ Hỗ trợ ĐA Phường/Xã (Multiple Wards)
            var wardIds = staff.StaffScopes
                .Where(s => s.ScopeTypeId == (int)ScopeLevel.Ward)
                .Select(s => s.ScopeRefId)
                .ToList();
            if (wardIds.Any())
            {
                return await query.Where(s => s.WardId.HasValue && wardIds.Contains(s.WardId.Value)).ToListAsync();
            }

            // 4. Mặc định Cửa Hàng (Store Manager) - Mức quyền thấp nhất
            return await query.Where(s => s.StoreId == staff.StoreId).ToListAsync();
        }

        public async Task<bool> CheckIfStoreIsWithinManagerScopeAsync(int currentStaffId, int targetStoreId)
        {
            var staff = await _context.Staffs
                .Include(s => s.StaffScopes)
                .FirstOrDefaultAsync(s => s.StaffId == currentStaffId);

            if (staff == null) return false;

            var storeQuery = _context.Stores.Where(s => s.StoreId == targetStoreId && s.Active);

            // 1. HQ System
            if (staff.StaffScopes.Any(s => s.ScopeTypeId == (int)ScopeLevel.HQ))
            {
                return await storeQuery.AnyAsync();
            }

            // 2. Province Check
            var provinceIds = staff.StaffScopes
                .Where(s => s.ScopeTypeId == (int)ScopeLevel.Province)
                .Select(s => s.ScopeRefId)
                .ToList();
            if (provinceIds.Any())
            {
                return await storeQuery.AnyAsync(s => s.Ward.ProvinceId.HasValue && provinceIds.Contains(s.Ward.ProvinceId.Value));
            }

            // 3. Ward Check
            var wardIds = staff.StaffScopes
                .Where(s => s.ScopeTypeId == (int)ScopeLevel.Ward)
                .Select(s => s.ScopeRefId)
                .ToList();
            if (wardIds.Any())
            {
                return await storeQuery.AnyAsync(s => s.WardId.HasValue && wardIds.Contains(s.WardId.Value));
            }

            // 4. Store Level Security Match
            return staff.StoreId == targetStoreId;
        }
    }
}
