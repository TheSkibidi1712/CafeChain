using CafeChain.Application.Interfaces.Admin.Dashboard;
using CafeChain.Infrastrusture.Interfaces.Admin.Dashboard;
using CafeChain.ViewModels.Admin.Dashboard;
using CafeChain.Application.DTOs.Admin.Dashboard;
namespace CafeChain.Application.Services.Admin.Dashboard
{
    public class DashboardService : IDashboardService
    {
        private readonly IDashboardRepository _repo;
        private static readonly DateTime DashboardStartDate = new DateTime(2023, 1, 1);
        public DashboardService(IDashboardRepository repo)
        {
            _repo = repo;
        }

        public async Task<DashboardVM> GetDashboardAsync(DashboardRequest request)
        {
            if (!request.StaffId.HasValue)
            {
                throw new Exception("StaffId is required");
            }

            // ========================================
            // DOUBLE PROTECTION
            // tránh trường hợp API khác gọi service
            // mà quên set date ở controller
            // ========================================

            if (request.FromDate == default)
            {
                request.FromDate = DashboardStartDate;
            }

            if (request.ToDate == default)
            {
                request.ToDate = DateTime.Today;
            }


            var vm = new DashboardVM();

            // ================= SCOPE =================
            var scopes = await _repo.GetUserScopesAsync(request.StaffId.Value);

            bool isCountry = scopes.Any(x => x.ScopeTypeId == 1);

            var provinceIds = scopes
                .Where(x => x.ScopeTypeId == 2)
                .Select(x => x.ScopeRefId)
                .ToList();

            var districtIds = scopes
                .Where(x => x.ScopeTypeId == 3)
                .Select(x => x.ScopeRefId)
                .ToList();

            var storeIds = scopes
                .Where(x => x.ScopeTypeId == 5)
                .Select(x => x.ScopeRefId)
                .ToList();

            // ================= STORES BY SCOPE =================
            var stores = await _repo.GetStoresByScopeAsync(
                provinceIds,
                districtIds,
                storeIds,
                isCountry);

            if (!stores.Any())
                throw new UnauthorizedAccessException("No store access");

            vm.Stores = stores;

            // ================= DROPDOWN =================

            vm.Provinces = stores
                .Where(x => x.ProvinceId.HasValue)
                .Select(x => x.ProvinceId.Value)
                .Distinct()
                .ToList();

            // filter theo province
            var filteredByProvince = request.ProvinceId.HasValue
                ? stores.Where(x => x.ProvinceId == request.ProvinceId)
                : stores;

            vm.Districts = filteredByProvince
                .Where(x => x.DistrictId.HasValue)
                .Select(x => x.DistrictId.Value)
                .Distinct()
                .ToList();

            // filter theo district
            var filteredStores = request.DistrictId.HasValue
                ? filteredByProvince.Where(x => x.DistrictId == request.DistrictId)
                : filteredByProvince;

            // ================= FIX QUAN TRỌNG =================
            // 👉 Nếu KHÔNG có StoreId → lấy toàn bộ store trong scope (KHÔNG phải first)
            if (request.StoreId.HasValue)
            {
                if (!filteredStores.Any(s => s.StoreId == request.StoreId))
                    throw new UnauthorizedAccessException();
            }

            var finalStoreIds = request.StoreId.HasValue
                ? new List<int> { request.StoreId.Value }
                : filteredStores.Select(x => x.StoreId).ToList();

            // 🔥 FIX CỰC QUAN TRỌNG: tránh empty
            if (!finalStoreIds.Any())
                finalStoreIds = stores.Select(x => x.StoreId).ToList();

            // ================= DATA =================

            // 🔥 Revenue (trend)
            vm.Revenue = (await _repo.GetRevenueAsync(
                request.FromDate,
                request.ToDate,
                request.StoreId,   // dùng 1 store nếu có
                request.ProvinceId,
                request.DistrictId)).ToList();

            // 🔥 Revenue by store (scope)
            vm.RevenueByStore = (await _repo.GetRevenueByStoreAsync(
                request.FromDate,
                request.ToDate,
                finalStoreIds)).ToList();

            vm.TopDrinks = (await _repo.GetTopDrinksAsync(10,request.FromDate, request.ToDate, finalStoreIds)).ToList();
            vm.TopToppings = (await _repo.GetTopToppingsAsync()).ToList();

            vm.PaymentMethods = (await _repo.GetPaymentMethodsAsync(request.FromDate, request.ToDate, finalStoreIds)).ToList();
            vm.StaffPerformance = (await _repo.GetStaffPerformanceAsync(request.FromDate, request.ToDate, finalStoreIds)).ToList();

            vm.Summary = await _repo.GetSummaryAsync(request.FromDate, request.ToDate, finalStoreIds) ?? new DashboardSummaryDto();

            // ================= STORE DETAIL =================
            // 👉 chỉ load khi chọn 1 store
            if (request.StoreId.HasValue)
            {
                vm.Inventory = (await _repo.GetInventoryAsync(request.StoreId.Value)).ToList();

                vm.CashFlows = (await _repo.GetCashFlowAsync(request.StoreId.Value)).ToList();

                vm.Waste = (await _repo.GetWasteAsync(
                    request.FromDate,
                    request.ToDate,
                    request.StoreId)).ToList();
            }
            else
            {
                vm.Inventory = new List<InventoryDto>();
                vm.Waste = new List<WasteDto>();
                vm.CashFlows = new List<CashFlowDto>();
            }

            return vm;
        }
    }
}
