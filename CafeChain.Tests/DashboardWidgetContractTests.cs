using CafeChain.Application.DTOs.Admin.Dashboard;

namespace CafeChain.Tests;

public sealed class DashboardWidgetContractTests
{
    private static readonly IReadOnlyDictionary<string, (Type RowType, string[] Fields)> Contracts =
        new Dictionary<string, (Type, string[])>(StringComparer.Ordinal)
        {
            ["netSalesTrend"] = (typeof(NetSalesTrendRow), ["BucketDate", "NetSales"]),
            ["storeRanking"] = (typeof(StoreRankingRow), ["StoreId", "StoreName", "NetSales"]),
            ["paymentMethodMix"] = (typeof(PaymentMethodMixRow), ["PaymentMethodName", "Amount"]),
            ["orderHeatmap"] = (typeof(OrderHeatmapRow), ["IsoWeekday", "HourOfDay", "TotalOrders"]),
            ["operationalAlerts"] = (typeof(OperationalAlertRow), ["Severity", "AlertType", "StoreId", "AlertValue", "Message"]),

            ["kpis"] = (typeof(WorkShiftKpiRow), ["TotalWorkShifts", "OpenWorkShifts", "ExceptionClosedCount", "ReconciliationCount", "AbsoluteCashDiscrepancy"]),
            ["cashDiscrepancy"] = (typeof(WorkShiftCashDiscrepancyRow), ["WorkShiftId", "StoreId", "StoreName", "FullName", "CashDiscrepancy"]),
            ["shiftSales"] = (typeof(WorkShiftSalesRow), ["WorkShiftId", "NetSales"]),
            ["paymentMix"] = (typeof(WorkShiftPaymentMixRow), ["WorkShiftId", "PaymentMethodName", "Amount"]),
            ["hourlyOrders"] = (typeof(HourlyOrdersRow), ["HourOfDay", "TotalOrders"]),
            ["offlineReconciliation"] = (typeof(OfflineReconciliationRow), ["WorkShiftId", "StoreId", "OfflineOrderCountAtClose", "OfflineEstimatedTotalAtClose", "RequiresReconciliation", "HasLateOfflineSync"]),
            ["topDiscrepancies"] = (typeof(WorkShiftTopDiscrepancyRow), ["WorkShiftId", "StoreId", "StaffId", "CashDiscrepancy", "DiscrepancyReason", "EndTime"]),

            ["shortageRisk"] = (typeof(InventoryShortageRiskRow), ["StoreId", "StoreName", "IngredientId", "IngredientCode", "IngredientName", "Unit", "OnHandQuantity", "ReservedQuantity", "AvailableQuantity", "MinimumStock", "ShortageQuantity"]),
            ["movement"] = (typeof(InventoryMovementRow), ["MovementDate", "TransactionType", "Quantity"]),
            ["thresholdRisk"] = (typeof(InventoryThresholdRiskRow), ["StoreId", "IngredientName", "AvailableQty", "MinStockLevel", "MaxNegativeQty"]),
            ["reorderSuggestions"] = (typeof(InventoryReorderRow), ["StoreId", "IngredientName", "RequestedQuantity", "SuggestedQuantity", "Priority", "Status"]),
            ["waste"] = (typeof(InventoryWasteRow), ["StoreId", "StoreName", "IngredientId", "IngredientName", "WasteValue"]),
            ["fifoAge"] = (typeof(InventoryFifoAgeRow), ["StoreId", "IngredientId", "PreparedItemId", "RemainingQuantity", "AgeDays", "RemainingValue"]),

            ["purchaseOrderPipeline"] = (typeof(PurchaseOrderPipelineRow), ["Status", "OrderedValue"]),
            ["overduePurchaseOrders"] = (typeof(OverduePurchaseOrderRow), ["Code", "StoreId", "SupplierName", "Status", "ExpectedDeliveryAtUtc", "OverdueDays"]),
            ["supplierQuality"] = (typeof(SupplierQualityRow), ["SupplierId", "SupplierName", "RejectionRate"]),
            ["purchasePriceTrend"] = (typeof(PurchasePriceTrendRow), ["ReceiptDate", "IngredientId", "IngredientName", "AverageBaseUnitCost"]),
            ["spendBreakdown"] = (typeof(ProcurementSpendRow), ["SupplierId", "SupplierName", "StoreId", "Spend"]),
            ["supplierIssueMix"] = (typeof(SupplierIssueMixRow), ["SupplierId", "SupplierName", "StoreId", "StoreName", "IssueType", "Status", "IssueCount"]),

            ["topProducts"] = (typeof(TopProductRow), ["DrinkId", "DrinkName", "CategoryName", "ProductRevenue", "ConfirmedMarginRate"]),
            ["volumeMargin"] = (typeof(VolumeMarginRow), ["DrinkId", "DrinkName", "Volume", "Revenue", "ConfirmedCogs", "ConfirmedMarginRate"]),
            ["sizeMargin"] = (typeof(SizeMarginRow), ["SizeId", "SizeName", "ConfirmedGrossProfit"]),
            ["topToppings"] = (typeof(TopToppingAnalyticsRow), ["ToppingId", "ToppingName", "Revenue"]),
            ["bomHealth"] = (typeof(BomHealthRow), ["DrinkCode", "DrinkName", "RecipeCount", "RecipeLineCount", "InvalidLineCount"]),
            ["lowEfficiency"] = (typeof(LowEfficiencyProductRow), ["DrinkName", "TotalSold", "ConfirmedCogs", "ConfirmedGrossProfit"]),

            ["shiftStatus"] = (typeof(WorkforceShiftStatusRow), ["StaffShiftId", "StaffId", "StoreId", "ShiftId", "ShiftName", "PlannedStartAt", "PlannedEndAt", "StatusCode", "IsOvernight"]),
            ["hourlyDemand"] = (typeof(WorkforceHourlyDemandRow), ["HourOfDay", "TotalOrders", "ScheduledStaffCount"]),
            ["staffPerformance"] = (typeof(WorkforceStaffPerformanceRow), ["FullName", "StoreId", "WorkShiftCount", "TotalOrders", "NetSales", "AverageOrderValue", "OrdersPerWorkShift"])
        };

    [Fact]
    public void Dashboard_defines_all_33_widget_contracts_and_every_field_exists_on_its_row_dto()
    {
        var script = ReadDashboardScript();

        Assert.Equal(33, Contracts.Count);
        foreach (var (widget, contract) in Contracts)
        {
            Assert.Contains($"\"{widget}\"", script, StringComparison.Ordinal);
            var properties = contract.RowType.GetProperties()
                .Select(property => property.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.All(contract.Fields, field => Assert.Contains(field, properties));
        }
    }

    [Fact]
    public void Workforce_contract_contains_schedule_and_pos_activity_only()
    {
        var dto = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "CafeChain", "Application", "DTOs", "Admin", "Dashboard", "DashboardAnalyticsDtos.cs"));
        var repository = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "CafeChain", "Infrastructure", "Repositories", "Admin", "Dashboard", "DashboardRepository.cs"));
        var script = ReadDashboardScript();
        var combined = dto + repository + script;

        Assert.DoesNotContain("ActualCheckIn", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("ActualCheckOut", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("PayrollHours", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("SalesPerPayrollHour", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("OrdersPerStaff", combined, StringComparison.Ordinal);
        Assert.Contains("Lịch nhân sự là kế hoạch dự kiến", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Dashboard_uses_semantic_multi_series_and_non_empty_entity_labels()
    {
        var script = ReadDashboardScript();

        Assert.Contains("seriesBy: \"paymentMethodName\"", script, StringComparison.Ordinal);
        Assert.Contains("seriesBy: \"transactionType\"", script, StringComparison.Ordinal);
        Assert.Contains("seriesBy: \"ingredientName\"", script, StringComparison.Ordinal);
        Assert.Contains("aggregate: \"count\"", script, StringComparison.Ordinal);
        Assert.Contains("entityLabel(row, \"drink\")", script, StringComparison.Ordinal);
        Assert.Contains("[\"ingredientName\", \"ingredientId\", \"Nguyên liệu\"]", script, StringComparison.Ordinal);
        Assert.Contains("[\"supplierName\", \"supplierId\", \"Nhà cung cấp\"]", script, StringComparison.Ordinal);
        Assert.Contains("Ca #", script, StringComparison.Ordinal);
        Assert.Contains("Không xác định", script, StringComparison.Ordinal);
    }

    private static string ReadDashboardScript() => File.ReadAllText(Path.Combine(
        FindRepoRoot(), "CafeChain", "wwwroot", "js", "Admin", "Dashboard", "dashboard.js"));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.csproj")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
