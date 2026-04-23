using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Admin.Dashboard;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace CafeChain.Infrastrusture.Repositories.Admin.Dashboard
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly AppDbContext _context;

        public DashboardRepository(AppDbContext context)
        {
            _context = context;
        }

        // ================= REVENUE =================

        public async Task<IEnumerable<RevenueDto>> GetRevenueAsync(DateTime from, DateTime to, int? storeId, int? provinceId, int? districtId)
        {
            string storeIds = storeId.HasValue ? storeId.Value.ToString() : null;

            var result = await _context.Set<RevenueDto>()
                .FromSqlRaw(
                    "EXEC sp_Revenue_Filtered @FromDate, @ToDate, @StoreIds, @ProvinceId, @DistrictId",
                    new SqlParameter("@FromDate", from),
                    new SqlParameter("@ToDate", to),
                    new SqlParameter("@StoreIds", (object?)storeIds ?? DBNull.Value),
                    new SqlParameter("@ProvinceId", provinceId ?? (object)DBNull.Value),
                    new SqlParameter("@DistrictId", districtId ?? (object)DBNull.Value)
                )
                .AsNoTracking()
                .ToListAsync();

            return result;
        }

        public async Task<IEnumerable<RevenueByStoreDto>> GetRevenueByStoreAsync(DateTime from, DateTime to, List<int> storeIds)
        {
            var storeIdsString = storeIds != null && storeIds.Any()
                ? string.Join(",", storeIds)
                : null;

            var result = await _context.Set<RevenueByStoreDto>()
                .FromSqlRaw(
                    "EXEC sp_Revenue_By_Store @FromDate, @ToDate, @StoreIds",
                    new SqlParameter("@FromDate", from),
                    new SqlParameter("@ToDate", to),
                    new SqlParameter("@StoreIds", (object?)storeIdsString ?? DBNull.Value)
                )
                .AsNoTracking()
                .ToListAsync();

            return result;
        }

        // ================= TOP PRODUCTS =================

        public async Task<IEnumerable<TopDrinkDto>> GetTopDrinksAsync(int top)
        {
            var result = await _context.Set<TopDrinkDto>()
                .FromSqlRaw(
                    "EXEC sp_Top_Selling_Drinks @Top",
                    new SqlParameter("@Top", top)
                )
                .AsNoTracking()
                .ToListAsync();

            return result;
        }

        public async Task<IEnumerable<TopToppingDto>> GetTopToppingsAsync()
        {
            var result = await _context.Set<TopToppingDto>()
                .FromSqlRaw("EXEC sp_Top_Toppings")
                .AsNoTracking()
                .ToListAsync();

            return result;
        }

        // ================= PAYMENT =================

        public async Task<IEnumerable<PaymentMethodDto>> GetPaymentMethodsAsync()
        {
            var result = await _context.Set<PaymentMethodDto>()
                .FromSqlRaw("EXEC sp_Revenue_By_PaymentMethod")
                .AsNoTracking()
                .ToListAsync();

            return result;
        }

        // ================= STAFF =================

        public async Task<IEnumerable<StaffPerformanceDto>> GetStaffPerformanceAsync()
        {
            var result = await _context.Set<StaffPerformanceDto>()
                .FromSqlRaw("EXEC sp_Staff_Performance")
                .AsNoTracking()
                .ToListAsync();

            return result;
        }

        // ================= INVENTORY =================

        public async Task<IEnumerable<InventoryDto>> GetInventoryAsync(int storeId)
        {
            var result = await _context.Set<InventoryDto>()
                .FromSqlRaw(
                    "EXEC sp_Inventory_Summary @StoreId",
                    new SqlParameter("@StoreId", storeId)
                )
                .AsNoTracking()
                .ToListAsync();

            return result;
        }

        public async Task<IEnumerable<WasteDto>> GetWasteAsync(DateTime from, DateTime to, int? storeId)
        {
            var result = await _context.Set<WasteDto>()
                .FromSqlRaw(
                    "EXEC sp_Waste_Report @FromDate, @ToDate, @StoreIds",
                    new SqlParameter("@FromDate", from),
                    new SqlParameter("@ToDate", to),
                    new SqlParameter("@StoreIds", storeId?.ToString() ?? (object)DBNull.Value)
                )
                .AsNoTracking()
                .ToListAsync();

            return result;
        }

        // ================= CASH FLOW =================

        public async Task<IEnumerable<CashFlowDto>> GetCashFlowAsync(int storeId)
        {
            var result = await _context.Set<CashFlowDto>()
                .FromSqlRaw(
                    "EXEC sp_Cash_Flow_Today @StoreIds",
                    new SqlParameter("@StoreIds", storeId.ToString())
                )
                .AsNoTracking()
                .ToListAsync();

            return result;
        }

        // ================= SUMMARY =================

        public async Task<DashboardSummaryDto> GetSummaryAsync()
        {
            var list = await _context.Set<DashboardSummaryDto>()
                .FromSqlRaw("EXEC sp_Dashboard_Summary")
                .AsNoTracking()
                .ToListAsync(); // 🔥 MUST

            return list.FirstOrDefault();
        }

        // ================= SCOPE =================
        public async Task<List<ScopeDto>> GetUserScopesAsync(int staffId)
        {
            return await _context.StaffScopes
                .Where(x => x.StaffId == staffId)
                .Select(x => new ScopeDto
                {
                    ScopeTypeId = x.ScopeTypeId,
                    ScopeRefId = x.ScopeRefId
                })
                .ToListAsync();
        }

        // ================= STORES BY SCOPE =================
        public async Task<List<StoreDropdownDto>> GetStoresByScopeAsync(List<int> provinceIds, List<int> districtIds, List<int> storeIds, bool isCountry)
        {
            var query = _context.Stores.AsQueryable();

            if (!isCountry)
            {
                if (storeIds.Any())
                {
                    query = query.Where(s => storeIds.Contains(s.StoreId));
                }
                else if (districtIds.Any())
                {
                    query = query.Where(s => s.DistrictId.HasValue && districtIds.Contains(s.DistrictId.Value));
                }
                else if (provinceIds.Any())
                {
                    query = query.Where(s => s.ProvinceId.HasValue && provinceIds.Contains(s.ProvinceId.Value));
                }
            }

            return await query
                .Select(s => new StoreDropdownDto
                {
                    StoreId = s.StoreId,
                    StoreName = s.Name,

                    ProvinceId = s.ProvinceId,
                    ProvinceName = s.Province.Name,

                    DistrictId = s.DistrictId,
                    DistrictName = s.District.Name
                })
                .ToListAsync();
        }
    }
}
