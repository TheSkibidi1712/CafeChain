using System.Text.RegularExpressions;

namespace CafeChain.Tests;

public sealed class DashboardAnalyticsScriptTests
{
    private static readonly string[] CanonicalProcedures =
    [
        "usp_Dashboard_NetSalesTrend", "usp_Dashboard_StoreRanking", "usp_Dashboard_PaymentMethodMix",
        "usp_Dashboard_OrderHeatmap", "usp_Dashboard_OperationalAlerts",
        "usp_Operations_WorkShiftCashDiscrepancy", "usp_Operations_WorkShiftSales", "usp_Operations_WorkShiftPaymentMix",
        "usp_Operations_OfflineReconciliationExceptions", "usp_Operations_HourlyOrders",
        "usp_Operations_WorkShiftTopDiscrepancies", "usp_Operations_WorkShiftKpis",
        "usp_Inventory_ShortageRisk", "usp_Inventory_MovementByType", "usp_Inventory_ThresholdRisk",
        "usp_Inventory_ReorderSuggestions", "usp_Inventory_WasteByStoreIngredient", "usp_Inventory_FifoLayerAge",
        "usp_Procurement_PurchaseOrderPipeline", "usp_Procurement_OverduePurchaseOrders", "usp_Procurement_SupplierQuality",
        "usp_Procurement_PurchasePriceTrend", "usp_Procurement_SpendBreakdown", "usp_Procurement_SupplierIssueMix",
        "usp_Product_TopProducts", "usp_Product_VolumeMarginMatrix", "usp_Product_SizeMargin",
        "usp_Product_TopToppings", "usp_Product_BomHealth", "usp_Product_HighConsumptionLowEfficiency",
        "usp_Workforce_ShiftStatus", "usp_Workforce_HourlyDemand", "usp_Workforce_StaffPerformance"
    ];

    private static readonly string[] LegacyProcedures =
    [
        "sp_Revenue_By_Store", "sp_Revenue_Filtered", "sp_Inventory_Summary", "sp_Waste_Report",
        "sp_Cash_Flow_Today", "sp_Top_Selling_Drinks_Filtered", "sp_Top_Toppings_Filtered",
        "sp_Top_Customers", "sp_Revenue_By_PaymentMethod_Filtered", "sp_Order_Status_Stats",
        "sp_Revenue_By_Hour", "sp_Staff_Performance_Filtered", "sp_Dashboard_Summary_Filtered"
    ];

    [Fact]
    public void Script_contains_all_canonical_and_legacy_contracts()
    {
        var sql = ReadScript();
        Assert.All(CanonicalProcedures.Concat(LegacyProcedures), name =>
            Assert.Contains($"PROCEDURE dbo.{name}", sql, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(46, Regex.Matches(sql, @"CREATE\s+OR\s+ALTER\s+PROCEDURE", RegexOptions.IgnoreCase).Count);
    }

    [Fact]
    public void Script_uses_guardrails_and_does_not_create_attendance_quality_metric()
    {
        var sql = ReadScript();
        Assert.DoesNotMatch(new Regex(@"SELECT\s+\*", RegexOptions.IgnoreCase), sql);
        Assert.DoesNotContain("NOLOCK", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sp_executesql", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AttendanceQuality", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OrderStatusId=5", sql.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ShippingFee", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ufn_AnalyticsBucketStart", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ufn_AnalyticsNextBucket", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHEN 'HOUR'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHEN 'WEEK'", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHEN 'MONTH'", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Repository_uses_all_canonical_procedures_with_typed_fixed_contract()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "CafeChain", "Infrastructure", "Repositories", "Admin", "Dashboard", "DashboardRepository.cs"));
        var procedureCalls = Regex.Matches(source, "dbo\\.(usp_[A-Za-z0-9_]+)")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(CanonicalProcedures.OrderBy(x => x), procedureCalls.OrderBy(x => x));
        Assert.DoesNotContain("dbo.sp_", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Dictionary<string, object>", source, StringComparison.Ordinal);
        Assert.Contains("CommandType.StoredProcedure", source, StringComparison.Ordinal);
        Assert.All(new[] { "@FromDate", "@ToDate", "@StoreIds", "@Granularity", "@Top" },
            parameter => Assert.Contains(parameter, source, StringComparison.Ordinal));
        Assert.Contains("catch (OperationCanceledException) { throw; }", source, StringComparison.Ordinal);
    }

    private static string ReadScript() => File.ReadAllText(Path.Combine(
        FindRepoRoot(), "CafeChain", "Scripts", "20260717_DashboardAnalyticsStoredProcedures.idempotent.sql"));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "CafeChain")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("CafeChain repository root not found.");
    }
}
