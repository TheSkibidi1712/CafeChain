using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.Admin.Dashboard;

public enum DashboardSection
{
    Executive,
    Operations,
    Inventory,
    Procurement,
    Product,
    Workforce
}

public enum DashboardAnalyticsWidget
{
    NetSalesTrend, StoreRanking, PaymentMethodMix, OrderHeatmap, OperationalAlerts,
    WorkShiftCashDiscrepancy, WorkShiftSales, WorkShiftPaymentMix,
    OfflineReconciliationExceptions, HourlyOrders, WorkShiftTopDiscrepancies, WorkShiftKpis,
    InventoryShortageRisk, InventoryMovementByType, InventoryThresholdRisk,
    InventoryReorderSuggestions, InventoryWasteByStoreIngredient, InventoryFifoLayerAge,
    PurchaseOrderPipeline, OverduePurchaseOrders, SupplierQuality, PurchasePriceTrend,
    ProcurementSpendBreakdown, SupplierIssueMix,
    TopProducts, VolumeMarginMatrix, SizeMargin, TopToppings, BomHealth,
    HighConsumptionLowEfficiency,
    WorkforceShiftStatus, WorkforceHourlyDemand, WorkforceStaffPerformance
}

public class DashboardFilterDto
{
    [Required] public DateTime FromDate { get; set; } = DateTime.Today.AddDays(-7);
    [Required] public DateTime ToDate { get; set; } = DateTime.Today;
    public int? StoreId { get; set; }
    public int? ProvinceId { get; set; }
    public int? DistrictId { get; set; }
    [RegularExpression("^(Hour|Day|Week|Month)$")]
    public string Granularity { get; set; } = "Day";
    [Range(1, 100)] public int Top { get; set; } = 10;
}

// Compatibility query contract retained for the existing GetAnalytics route.
public sealed class DashboardAnalyticsFilter : DashboardFilterDto
{
    internal int? StaffId { get; set; }
}

public sealed class DashboardStoreOptionDto
{
    public int StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public int? ProvinceId { get; set; }
    public string ProvinceName { get; set; } = string.Empty;
    public int? DistrictId { get; set; }
    public string DistrictName { get; set; } = string.Empty;
}

public sealed class DashboardPageDto
{
    public DashboardFilterDto Filter { get; set; } = new();
    public IReadOnlyList<DashboardStoreOptionDto> Stores { get; set; } = [];
    public string RoleName { get; set; } = string.Empty;
}

public sealed class DashboardWidgetResult<T>
{
    public string Status { get; set; } = "AVAILABLE";
    public IReadOnlyList<T> Data { get; set; } = [];
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
    public List<string> Warnings { get; set; } = [];

    public static DashboardWidgetResult<T> Success(IReadOnlyList<T> data) => new()
    {
        Data = data,
        Status = data.Count == 0 ? "NO_DATA" : "AVAILABLE"
    };

    public static DashboardWidgetResult<T> Failure(string message) => new()
    {
        Status = "ERROR",
        ErrorCode = "DASHBOARD_QUERY_FAILED",
        Message = message
    };
}

public sealed class DashboardSectionResponse<T>
{
    public DashboardSection Section { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToExclusive { get; set; }
    public string Granularity { get; set; } = string.Empty;
    public IReadOnlyList<int> StoreIds { get; set; } = [];
    public T Data { get; set; } = default!;
}

public sealed class ExecutiveDashboardData
{
    public DashboardWidgetResult<NetSalesTrendRow> NetSalesTrend { get; set; } = new();
    public DashboardWidgetResult<StoreRankingRow> StoreRanking { get; set; } = new();
    public DashboardWidgetResult<PaymentMethodMixRow> PaymentMethodMix { get; set; } = new();
    public DashboardWidgetResult<OrderHeatmapRow> OrderHeatmap { get; set; } = new();
    public DashboardWidgetResult<OperationalAlertRow> OperationalAlerts { get; set; } = new();
}

public sealed class OperationsDashboardData
{
    public DashboardWidgetResult<WorkShiftCashDiscrepancyRow> CashDiscrepancy { get; set; } = new();
    public DashboardWidgetResult<WorkShiftSalesRow> ShiftSales { get; set; } = new();
    public DashboardWidgetResult<WorkShiftPaymentMixRow> PaymentMix { get; set; } = new();
    public DashboardWidgetResult<OfflineReconciliationRow> OfflineReconciliation { get; set; } = new();
    public DashboardWidgetResult<HourlyOrdersRow> HourlyOrders { get; set; } = new();
    public DashboardWidgetResult<WorkShiftTopDiscrepancyRow> TopDiscrepancies { get; set; } = new();
    public DashboardWidgetResult<WorkShiftKpiRow> Kpis { get; set; } = new();
}

public sealed class InventoryDashboardData
{
    public DashboardWidgetResult<InventoryShortageRiskRow> ShortageRisk { get; set; } = new();
    public DashboardWidgetResult<InventoryMovementRow> Movement { get; set; } = new();
    public DashboardWidgetResult<InventoryThresholdRiskRow> ThresholdRisk { get; set; } = new();
    public DashboardWidgetResult<InventoryReorderRow> ReorderSuggestions { get; set; } = new();
    public DashboardWidgetResult<InventoryWasteRow> Waste { get; set; } = new();
    public DashboardWidgetResult<InventoryFifoAgeRow> FifoAge { get; set; } = new();
}

public sealed class ProcurementDashboardData
{
    public DashboardWidgetResult<PurchaseOrderPipelineRow> PurchaseOrderPipeline { get; set; } = new();
    public DashboardWidgetResult<OverduePurchaseOrderRow> OverduePurchaseOrders { get; set; } = new();
    public DashboardWidgetResult<SupplierQualityRow> SupplierQuality { get; set; } = new();
    public DashboardWidgetResult<PurchasePriceTrendRow> PurchasePriceTrend { get; set; } = new();
    public DashboardWidgetResult<ProcurementSpendRow> SpendBreakdown { get; set; } = new();
    public DashboardWidgetResult<SupplierIssueMixRow> SupplierIssueMix { get; set; } = new();
}

public sealed class ProductDashboardData
{
    public DashboardWidgetResult<TopProductRow> TopProducts { get; set; } = new();
    public DashboardWidgetResult<VolumeMarginRow> VolumeMargin { get; set; } = new();
    public DashboardWidgetResult<SizeMarginRow> SizeMargin { get; set; } = new();
    public DashboardWidgetResult<TopToppingAnalyticsRow> TopToppings { get; set; } = new();
    public DashboardWidgetResult<BomHealthRow> BomHealth { get; set; } = new();
    public DashboardWidgetResult<LowEfficiencyProductRow> LowEfficiency { get; set; } = new();
}

public sealed class WorkforceDashboardData
{
    public DashboardWidgetResult<WorkforceShiftStatusRow> ShiftStatus { get; set; } = new();
    public DashboardWidgetResult<WorkforceHourlyDemandRow> HourlyDemand { get; set; } = new();
    public DashboardWidgetResult<WorkforceStaffPerformanceRow> StaffPerformance { get; set; } = new();
}

public sealed class DashboardAnalyticsResponse
{
    public DashboardAnalyticsWidget Widget { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToExclusive { get; set; }
    public string Granularity { get; set; } = string.Empty;
    public IReadOnlyList<int> StoreIds { get; set; } = [];
    public object Rows { get; set; } = Array.Empty<object>();
    public string DataStatus { get; set; } = "AVAILABLE";
    public List<string> Warnings { get; set; } = [];
}

public abstract class DashboardRow
{
    public string DataStatus { get; set; } = "AVAILABLE";
}

public sealed class NetSalesTrendRow : DashboardRow { public DateTime BucketDate { get; set; } public long TotalOrders { get; set; } public decimal NetSales { get; set; } }
public sealed class StoreRankingRow : DashboardRow { public int StoreId { get; set; } public string StoreName { get; set; } = string.Empty; public long TotalOrders { get; set; } public decimal NetSales { get; set; } public decimal AverageOrderValue { get; set; } }
public sealed class PaymentMethodMixRow : DashboardRow { public int PaymentMethodId { get; set; } public string PaymentMethodCode { get; set; } = string.Empty; public string PaymentMethodName { get; set; } = string.Empty; public long TotalTransactions { get; set; } public decimal Amount { get; set; } public decimal Share { get; set; } }
public sealed class OrderHeatmapRow : DashboardRow { public int IsoWeekday { get; set; } public int HourOfDay { get; set; } public long TotalOrders { get; set; } public decimal NetSales { get; set; } }
public sealed class OperationalAlertRow : DashboardRow { public string AlertType { get; set; } = string.Empty; public int StoreId { get; set; } public int EntityId { get; set; } public string Severity { get; set; } = string.Empty; public decimal AlertValue { get; set; } public string Message { get; set; } = string.Empty; }

public sealed class WorkShiftCashDiscrepancyRow : DashboardRow { public int WorkShiftId { get; set; } public int StoreId { get; set; } public string StoreName { get; set; } = string.Empty; public int StaffId { get; set; } public string FullName { get; set; } = string.Empty; public DateTime StartTime { get; set; } public DateTime? EndTime { get; set; } public decimal StartingCash { get; set; } public decimal? ExpectedEndingCash { get; set; } public decimal? ActualEndingCash { get; set; } public decimal? CashDiscrepancy { get; set; } public string? DiscrepancyReason { get; set; } public bool IsExceptionClosed { get; set; } public bool RequiresReconciliation { get; set; } }
public sealed class WorkShiftSalesRow : DashboardRow { public int WorkShiftId { get; set; } public int StoreId { get; set; } public long TotalOrders { get; set; } public decimal NetSales { get; set; } public decimal AverageOrderValue { get; set; } }
public sealed class WorkShiftPaymentMixRow : DashboardRow { public int WorkShiftId { get; set; } public int StoreId { get; set; } public int PaymentMethodId { get; set; } public string PaymentMethodCode { get; set; } = string.Empty; public string PaymentMethodName { get; set; } = string.Empty; public long TotalTransactions { get; set; } public decimal Amount { get; set; } }
public sealed class OfflineReconciliationRow : DashboardRow { public int WorkShiftId { get; set; } public int StoreId { get; set; } public bool IsExceptionClosed { get; set; } public int OfflineOrderCountAtClose { get; set; } public decimal OfflineEstimatedTotalAtClose { get; set; } public decimal OfflineCashTotalAtClose { get; set; } public bool RequiresReconciliation { get; set; } public bool HasLateOfflineSync { get; set; } public int LateOfflineSyncCount { get; set; } public DateTime? LastLateOfflineSyncedAt { get; set; } }
public sealed class HourlyOrdersRow : DashboardRow { public int HourOfDay { get; set; } public long TotalOrders { get; set; } public decimal NetSales { get; set; } }
public sealed class WorkShiftTopDiscrepancyRow : DashboardRow { public int WorkShiftId { get; set; } public int StoreId { get; set; } public int StaffId { get; set; } public decimal? CashDiscrepancy { get; set; } public decimal AbsoluteDiscrepancy { get; set; } public string? DiscrepancyReason { get; set; } public DateTime EndTime { get; set; } }
public sealed class WorkShiftKpiRow : DashboardRow { public long TotalWorkShifts { get; set; } public int OpenWorkShifts { get; set; } public int ExceptionClosedCount { get; set; } public int ReconciliationCount { get; set; } public decimal AbsoluteCashDiscrepancy { get; set; } }

public sealed class InventoryShortageRiskRow : DashboardRow { public int StoreInventoryId { get; set; } public int StoreId { get; set; } public int IngredientId { get; set; } public string IngredientName { get; set; } = string.Empty; public decimal AvailableQty { get; set; } public decimal ReservedQty { get; set; } public decimal? MinStockLevel { get; set; } public string RiskLevel { get; set; } = string.Empty; }
public sealed class InventoryMovementRow : DashboardRow { public DateTime MovementDate { get; set; } public int TransactionType { get; set; } public long TransactionCount { get; set; } public decimal Quantity { get; set; } public decimal TotalCost { get; set; } }
public sealed class InventoryThresholdRiskRow : DashboardRow { public int StoreInventoryId { get; set; } public int StoreId { get; set; } public int IngredientId { get; set; } public string IngredientName { get; set; } = string.Empty; public decimal AvailableQty { get; set; } public decimal ReservedQty { get; set; } public decimal? MinStockLevel { get; set; } public decimal MaxNegativeQty { get; set; } public decimal QuantityAboveMinimum { get; set; } }
public sealed class InventoryReorderRow : DashboardRow { public int RestockRequestId { get; set; } public int StoreId { get; set; } public int IngredientId { get; set; } public string IngredientName { get; set; } = string.Empty; public decimal RequestedQuantity { get; set; } public decimal? SuggestedQuantity { get; set; } public decimal? SuggestionAverageDailyUsageSnapshot { get; set; } public int? SuggestionLeadTimeDaysSnapshot { get; set; } public decimal? SuggestionIncomingQuantitySnapshot { get; set; } public string? SuggestionReason { get; set; } public string Status { get; set; } = string.Empty; public string Priority { get; set; } = string.Empty; public DateTime CreatedAt { get; set; } }
public sealed class InventoryWasteRow : DashboardRow { public int StoreId { get; set; } public string StoreName { get; set; } = string.Empty; public int IngredientId { get; set; } public string IngredientName { get; set; } = string.Empty; public decimal WasteQuantity { get; set; } public decimal WasteValue { get; set; } public long TransactionCount { get; set; } }
public sealed class InventoryFifoAgeRow : DashboardRow { public long InventoryCostLayerId { get; set; } public int StoreId { get; set; } public int? IngredientId { get; set; } public int? PreparedItemId { get; set; } public decimal RemainingQuantity { get; set; } public decimal UnitCost { get; set; } public DateTime CreatedAt { get; set; } public int AgeDays { get; set; } public decimal RemainingValue { get; set; } }

public sealed class PurchaseOrderPipelineRow : DashboardRow { public string Status { get; set; } = string.Empty; public long PurchaseOrderCount { get; set; } public decimal OrderedValue { get; set; } }
public sealed class OverduePurchaseOrderRow : DashboardRow { public int PurchaseOrderId { get; set; } public string Code { get; set; } = string.Empty; public int StoreId { get; set; } public int SupplierId { get; set; } public string SupplierName { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; public DateTime OrderDate { get; set; } public DateTime ExpectedDeliveryAtUtc { get; set; } public int OverdueDays { get; set; } }
public sealed class SupplierQualityRow : DashboardRow { public int SupplierId { get; set; } public string SupplierName { get; set; } = string.Empty; public decimal AcceptedBaseQuantity { get; set; } public decimal RejectedBaseQuantity { get; set; } public decimal RejectionRate { get; set; } public long ReceiptCount { get; set; } }
public sealed class PurchasePriceTrendRow : DashboardRow { public DateTime ReceiptDate { get; set; } public int IngredientId { get; set; } public string IngredientName { get; set; } = string.Empty; public decimal AverageBaseUnitCost { get; set; } public decimal MinimumBaseUnitCost { get; set; } public decimal MaximumBaseUnitCost { get; set; } public decimal ReceivedBaseQuantity { get; set; } }
public sealed class ProcurementSpendRow : DashboardRow { public int SupplierId { get; set; } public string SupplierName { get; set; } = string.Empty; public int StoreId { get; set; } public decimal Spend { get; set; } public long ReceiptCount { get; set; } }
public sealed class SupplierIssueMixRow : DashboardRow { public string IssueType { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; public long IssueCount { get; set; } public decimal AffectedBaseQuantity { get; set; } }

public sealed class TopProductRow : DashboardRow { public int DrinkId { get; set; } public string DrinkName { get; set; } = string.Empty; public int TotalSold { get; set; } public decimal ProductRevenue { get; set; } public decimal ConfirmedCogs { get; set; } public decimal ConfirmedGrossProfit { get; set; } }
public sealed class VolumeMarginRow : DashboardRow { public int DrinkId { get; set; } public string DrinkName { get; set; } = string.Empty; public int Volume { get; set; } public decimal Revenue { get; set; } public decimal ConfirmedCogs { get; set; } public decimal ConfirmedMarginRate { get; set; } }
public sealed class SizeMarginRow : DashboardRow { public int? SizeId { get; set; } public string SizeName { get; set; } = string.Empty; public int TotalSold { get; set; } public decimal Revenue { get; set; } public decimal ConfirmedCogs { get; set; } public decimal ConfirmedGrossProfit { get; set; } }
public sealed class TopToppingAnalyticsRow : DashboardRow { public int ToppingId { get; set; } public string ToppingName { get; set; } = string.Empty; public int TotalUsed { get; set; } public decimal Revenue { get; set; } public decimal ConfirmedCogs { get; set; } }
public sealed class BomHealthRow : DashboardRow { public int DrinkId { get; set; } public string DrinkCode { get; set; } = string.Empty; public string DrinkName { get; set; } = string.Empty; public int RecipeCount { get; set; } public int RecipeLineCount { get; set; } public int InvalidLineCount { get; set; } }
public sealed class LowEfficiencyProductRow : DashboardRow { public int DrinkId { get; set; } public string DrinkName { get; set; } = string.Empty; public int TotalSold { get; set; } public decimal ConfirmedCogs { get; set; } public decimal ConfirmedGrossProfit { get; set; } }

public sealed class WorkforceShiftStatusRow : DashboardRow { public int StaffShiftId { get; set; } public int StaffId { get; set; } public string FullName { get; set; } = string.Empty; public int StoreId { get; set; } public DateTime WorkDate { get; set; } public DateTime? ActualCheckIn { get; set; } public DateTime? ActualCheckOut { get; set; } public decimal? PayrollHours { get; set; } public int StatusId { get; set; } public string StatusCode { get; set; } = string.Empty; public bool IsAdHoc { get; set; } }
public sealed class WorkforceHourlyDemandRow : DashboardRow { public int HourOfDay { get; set; } public long TotalOrders { get; set; } public decimal NetSales { get; set; } public long StaffShiftCount { get; set; } public decimal OrdersPerStaff { get; set; } }
public sealed class WorkforceStaffPerformanceRow : DashboardRow { public int StaffId { get; set; } public string FullName { get; set; } = string.Empty; public int StoreId { get; set; } public long TotalOrders { get; set; } public decimal NetSales { get; set; } public decimal PayrollHours { get; set; } public decimal SalesPerPayrollHour { get; set; } }
