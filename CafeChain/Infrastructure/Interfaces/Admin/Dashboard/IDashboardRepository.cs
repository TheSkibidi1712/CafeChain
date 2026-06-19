using CafeChain.Application.DTOs.Admin.Dashboard;

namespace CafeChain.Infrastrusture.Interfaces.Admin.Dashboard
{
    public interface IDashboardRepository
    {
        Task<IEnumerable<RevenueDto>> GetRevenueAsync(DateTime from, DateTime to, int? storeId, int? provinceId, int? districtId);

        Task<IEnumerable<RevenueByStoreDto>> GetRevenueByStoreAsync(DateTime from, DateTime to, List<int> storeIds);

        Task<IEnumerable<TopToppingDto>> GetTopToppingsAsync(DateTime from, DateTime to, List<int> storeIds);

        Task<IEnumerable<TopDrinkDto>> GetTopDrinksAsync(int top, DateTime from, DateTime to, List<int> storeIds);

        Task<IEnumerable<PaymentMethodDto>> GetPaymentMethodsAsync(DateTime from, DateTime to, List<int> storeIds);

        Task<IEnumerable<StaffPerformanceDto>> GetStaffPerformanceAsync(DateTime from, DateTime to, List<int> storeIds);

        Task<IEnumerable<InventoryDto>> GetInventoryAsync(int storeId);
        Task<IEnumerable<WasteDto>> GetWasteAsync(DateTime from, DateTime to, int? storeId);

        Task<IEnumerable<CashFlowDto>> GetCashFlowAsync(int storeId);

        Task<DashboardSummaryDto> GetSummaryAsync(DateTime from, DateTime to, List<int> storeIds);
        Task<List<ScopeDto>> GetUserScopesAsync(int staffId);
        Task<List<StoreDropdownDto>> GetStoresByScopeAsync(List<int> provinceIds, List<int> districtIds, List<int> storeIds, bool isCountry);
    }
}
