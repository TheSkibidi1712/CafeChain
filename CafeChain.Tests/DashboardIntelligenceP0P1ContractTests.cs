using System.Reflection;
using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.Application.Services.Admin.Dashboard;

namespace CafeChain.Tests;

public sealed class DashboardIntelligenceP0P1ContractTests
{
    [Fact]
    public void CancellationRate_IsWeightedByTotalOrders()
    {
        var rows = new[]
        {
            new OrderStatusSummaryRow { StoreId = 1, TotalOrders = 100, CancelledOrders = 10, CancellationRate = 0.10m },
            new OrderStatusSummaryRow { StoreId = 2, TotalOrders = 1, CancelledOrders = 1, CancellationRate = 1m }
        };

        var (value, sample) = InvokeMetric(DashboardAnalyticsWidget.OrderStatusSummary, rows);

        Assert.Equal(11m / 101m, value, 8);
        Assert.Equal(101, sample);
    }

    [Fact]
    public void SupplierRejectionRate_IsWeightedByReceivedQuantity()
    {
        var rows = new[]
        {
            new SupplierQualityRow
            {
                SupplierId = 1, SupplierName = "A",
                AcceptedBaseQuantity = 90, RejectedBaseQuantity = 10, RejectionRate = 0.10m, ReceiptCount = 1
            },
            new SupplierQualityRow
            {
                SupplierId = 2, SupplierName = "B",
                AcceptedBaseQuantity = 1, RejectedBaseQuantity = 9, RejectionRate = 0.90m, ReceiptCount = 1
            }
        };

        var (value, sample) = InvokeMetric(DashboardAnalyticsWidget.SupplierQuality, rows);

        Assert.Equal(19m / 110m, value, 8);
        Assert.Equal(2, sample);
    }

    [Theory]
    [InlineData("NO_DATA", "NO_DATA")]
    [InlineData("PARTIAL_COGS", "PARTIAL_COGS")]
    [InlineData("THRESHOLD_NOT_CONFIGURED", "MISSING_CONFIG")]
    [InlineData("ERROR", "ERROR")]
    [InlineData("AVAILABLE", "OK")]
    public void DataStatus_UsesRowQuality_NotOnlyRowCount(string rowStatus, string expected)
    {
        var rows = new[]
        {
            new TopProductRow { DrinkId = 1, DrinkName = "Test", ProductRevenue = 100, DataStatus = rowStatus }
        };
        var method = typeof(DashboardIntelligenceService).GetMethod(
            "EvaluateRowsStatus",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        var actual = Assert.IsType<string>(method.Invoke(null, [rows, "AVAILABLE"]));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void AiFrontend_ContainsRealChartsFallbackUnitsAbortAndFingerprintGuard()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "CafeChain", "wwwroot", "js", "Admin", "Dashboard", "dashboard-intelligence.js"));

        Assert.Contains("\"HorizontalBar\"", script, StringComparison.Ordinal);
        Assert.Contains("\"Donut\"", script, StringComparison.Ordinal);
        Assert.Contains("\"Heatmap\"", script, StringComparison.Ordinal);
        Assert.Contains("\"Scatter\"", script, StringComparison.Ordinal);
        Assert.Contains("window.echarts.init", script, StringComparison.Ordinal);
        Assert.Contains("renderTable(chart, rows", script, StringComparison.Ordinal);
        Assert.Contains("Intl.NumberFormat(\"vi-VN\"", script, StringComparison.Ordinal);
        Assert.Contains("AbortController", script, StringComparison.Ordinal);
        Assert.Contains("filterFingerprint", script, StringComparison.Ordinal);
        Assert.Contains("sequence !== requestSequence", script, StringComparison.Ordinal);
        Assert.Contains("horizontalBarVisibleRows = 12", script, StringComparison.Ordinal);
        Assert.Contains("yAxisIndex: 0", script, StringComparison.Ordinal);
        Assert.Contains("orient: \"vertical\"", script, StringComparison.Ordinal);
        Assert.Contains("inverse: true", script, StringComparison.Ordinal);
        Assert.Contains("overflow: \"truncate\"", script, StringComparison.Ordinal);
        Assert.Contains("ellipsis: \"…\"", script, StringComparison.Ordinal);
        Assert.Contains("axisValueLabel", script, StringComparison.Ordinal);
        Assert.Contains("instance.resize()", script, StringComparison.Ordinal);
        Assert.DoesNotContain("question.length < 0", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlContracts_ExposeEntityFieldsAndRatioScale()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "CafeChain", "Scripts", "20260717_DashboardAnalyticsStoredProcedures.idempotent.sql"));

        Assert.Contains("si.AvailableQty-si.ReservedQty AS AvailableQuantity", script, StringComparison.Ordinal);
        Assert.Contains("i.Code AS IngredientCode", script, StringComparison.Ordinal);
        Assert.Contains("issue.SupplierId,s.Name AS SupplierName", script, StringComparison.Ordinal);
        Assert.Contains("d.CategoryId,c.Name AS CategoryName", script, StringComparison.Ordinal);
        Assert.Contains("SUM(CASE WHEN o.OrderStatusId = 6 THEN 1 ELSE 0 END) * 1.0", script, StringComparison.Ordinal);
        Assert.DoesNotContain("SUM(CASE WHEN o.OrderStatusId = 6 THEN 1 ELSE 0 END) * 100.0", script, StringComparison.Ordinal);
    }

    [Fact]
    public void EntityEvidence_PreservesStoreProductIngredientAndSupplierNames()
    {
        var store = BuildEvidence(DashboardAnalyticsWidget.StoreRanking, new[]
        {
            new StoreRankingRow { StoreId = 1, StoreName = "CafeChain Dĩ An", TotalOrders = 2, NetSales = 200000 }
        });
        var product = BuildEvidence(DashboardAnalyticsWidget.TopProducts, new[]
        {
            new TopProductRow { DrinkId = 2, DrinkName = "Cà phê sữa", TotalSold = 3, ProductRevenue = 150000 }
        });
        var ingredient = BuildEvidence(DashboardAnalyticsWidget.InventoryShortageRisk, new[]
        {
            new InventoryShortageRiskRow
            {
                StoreId = 1, StoreName = "CafeChain Dĩ An", IngredientId = 3,
                IngredientCode = "MILK", IngredientName = "Sữa tươi", Unit = "LITER",
                AvailableQuantity = 12, MinimumStock = 20, ShortageQuantity = 8, RiskLevel = "HIGH"
            }
        });
        var supplier = BuildEvidence(DashboardAnalyticsWidget.SupplierQuality, new[]
        {
            new SupplierQualityRow
            {
                SupplierId = 4, SupplierName = "NCC Bình Minh",
                AcceptedBaseQuantity = 90, RejectedBaseQuantity = 10, RejectionRate = .1m, ReceiptCount = 2
            }
        });

        Assert.Contains(store, item => item.EntityName == "CafeChain Dĩ An");
        Assert.Contains(product, item => item.EntityName == "Cà phê sữa");
        Assert.Contains(ingredient, item =>
            item.EntityName == "Sữa tươi" && item.StoreName == "CafeChain Dĩ An" && item.Unit == "LITER");
        Assert.Contains(supplier, item => item.EntityName == "NCC Bình Minh");
    }

    [Fact]
    public void OperationalAlert_AlwaysBecomesBackendAnomaly()
    {
        var rows = new[]
        {
            new OperationalAlertRow
            {
                AlertType = "LOW_STOCK", StoreId = 1, StoreName = "Dĩ An",
                EntityType = "INGREDIENT", EntityId = 3, EntityCode = "MILK",
                EntityName = "Sữa tươi", Severity = "CRITICAL", AlertValue = -2,
                Unit = "LITER", Message = "Tồn dưới ngưỡng: Sữa tươi"
            }
        };
        var bundle = InvokeEvidenceBundle(DashboardAnalyticsWidget.OperationalAlerts, rows);
        var anomalies = new List<DashboardAnomalyResultDto>();
        var method = typeof(DashboardIntelligenceService).GetMethod(
            "AddBackendAnomalies",
            BindingFlags.Static | BindingFlags.NonPublic)!;

        method.Invoke(null, [DashboardAnalyticsWidget.OperationalAlerts, bundle, anomalies]);

        var anomaly = Assert.Single(anomalies);
        Assert.Equal("LOW_STOCK", anomaly.Code);
        Assert.NotEmpty(anomaly.EvidenceIds);
    }

    [Fact]
    public void InvalidEvidenceId_IsRejectedByNarrativeValidator()
    {
        var method = typeof(DashboardIntelligenceService).GetMethod(
            "ValidateNarratives",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var input = new[]
        {
            new DashboardNarrativeItemDto { Text = "Hợp lệ", EvidenceIds = ["E-VALID"] },
            new DashboardNarrativeItemDto { Text = "Bịa evidence", EvidenceIds = ["E-NOT-FOUND"] }
        };
        var allowed = new HashSet<string>(["E-VALID"], StringComparer.Ordinal);

        var result = Assert.IsAssignableFrom<IReadOnlyCollection<DashboardNarrativeItemDto>>(
            method.Invoke(null, [input, allowed]));

        Assert.Single(result);
        Assert.Equal("Hợp lệ", result.Single().Text);
    }

    private static (decimal Value, long Sample) InvokeMetric(
        DashboardAnalyticsWidget widget,
        object rows)
    {
        var method = typeof(DashboardIntelligenceService).GetMethod(
            "Metric",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var metric = method.Invoke(null, [widget, rows])!;
        var type = metric.GetType();
        return (
            (decimal)type.GetProperty("Value")!.GetValue(metric)!,
            (long)type.GetProperty("Sample")!.GetValue(metric)!);
    }

    private static IReadOnlyList<DashboardEvidenceDto> BuildEvidence(
        DashboardAnalyticsWidget widget,
        object rows)
    {
        var bundle = InvokeEvidenceBundle(widget, rows);
        var type = bundle.GetType();
        var facts = (IEnumerable<DashboardEvidenceDto>)type.GetProperty("Facts")!.GetValue(bundle)!;
        var statistics = (IEnumerable<DashboardEvidenceDto>)type.GetProperty("Statistics")!.GetValue(bundle)!;
        return facts.Concat(statistics).ToList();
    }

    private static object InvokeEvidenceBundle(DashboardAnalyticsWidget widget, object rows)
    {
        var method = typeof(DashboardIntelligenceService).GetMethod(
            "BuildWidgetEvidence",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        return method.Invoke(null, [
            widget,
            rows,
            new DashboardComparisonResultDto { CurrentValue = InvokeMetric(widget, rows).Value, CurrentSampleSize = 1 },
            "OK",
            10
        ])!;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.csproj")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
