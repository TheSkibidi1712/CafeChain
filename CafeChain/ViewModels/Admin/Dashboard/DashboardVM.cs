using CafeChain.Application.DTOs.Admin.Dashboard;

namespace CafeChain.ViewModels.Admin.Dashboard
{
    public class DashboardVM
    {
        public List<RevenueDto> Revenue { get; set; }
        public List<RevenueByStoreDto> RevenueByStore { get; set; }

        public List<TopDrinkDto> TopDrinks { get; set; }
        public List<TopToppingDto> TopToppings { get; set; }

        public List<PaymentMethodDto> PaymentMethods { get; set; }

        public List<StaffPerformanceDto> StaffPerformance { get; set; }

        public List<InventoryDto> Inventory { get; set; }
        public List<WasteDto> Waste { get; set; }

        public List<CashFlowDto> CashFlows { get; set; }

        public DashboardSummaryDto Summary { get; set; }
        public List<StoreDropdownDto> Stores { get; set; } = new();
        public List<int> Provinces { get; set; } = new();
        public List<int> Wards { get; set; } = new();
    }
}
