using System.Reflection;
using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.Application.Services.Admin.Dashboard;

namespace CafeChain.Tests;

public sealed class DashboardMetricContractTests
{
    [Fact]
    public void EveryCatalogWidgetHasExplicitMetricContractAndChartField()
    {
        var catalog = typeof(DashboardIntelligenceService).Assembly
            .GetType("CafeChain.Application.Services.Admin.Dashboard.DashboardWidgetCatalog")!;
        var get = catalog.GetMethod("Get", BindingFlags.Static | BindingFlags.Public)!;
        var definitions = Enum.GetValues<DashboardAnalyticsWidget>()
            .Select(widget => get.Invoke(null, [widget])!)
            .ToList();

        Assert.Equal(Enum.GetValues<DashboardAnalyticsWidget>().Length, definitions.Count);
        foreach (var definition in definitions)
        {
            var metric = definition.GetType().GetProperty("Metric")!.GetValue(definition);
            Assert.NotNull(metric);
            Assert.False(string.IsNullOrWhiteSpace((string)metric!.GetType().GetProperty("Name")!.GetValue(metric)!));
            var chartValue = (string)definition.GetType().GetProperty("ValueField")!.GetValue(definition)!;
            var metricValue = (string)metric.GetType().GetProperty("ValueField")!.GetValue(metric)!;
            Assert.Equal(chartValue, metricValue);
        }
    }

    [Fact]
    public void EveryDataPlanWidgetHasMetricContract()
    {
        var service = typeof(DashboardIntelligenceService);
        var dataPlan = service.GetMethod("DataPlan", BindingFlags.Static | BindingFlags.NonPublic)!;
        var catalog = service.Assembly.GetType(
            "CafeChain.Application.Services.Admin.Dashboard.DashboardWidgetCatalog")!;
        var get = catalog.GetMethod("Get", BindingFlags.Static | BindingFlags.Public)!;

        var widgets = new HashSet<DashboardAnalyticsWidget>();
        foreach (var intent in Enum.GetValues<DashboardBusinessIntent>())
        {
            var result = (IEnumerable<DashboardAnalyticsWidget>)dataPlan.Invoke(
                null, [intent, Array.Empty<string>()])!;
            widgets.UnionWith(result);
        }

        Assert.Equal(31, widgets.Count);
        Assert.All(widgets, widget =>
        {
            var definition = get.Invoke(null, [widget])!;
            Assert.NotNull(definition.GetType().GetProperty("Metric")!.GetValue(definition));
        });
    }

    public static IEnumerable<object[]> ExplicitMetricCases()
    {
        yield return [DashboardAnalyticsWidget.OrderHeatmap, new[] { new OrderHeatmapRow { TotalOrders = 3 }, new OrderHeatmapRow { TotalOrders = 7 } }, 10m];
        yield return [DashboardAnalyticsWidget.WorkShiftCashDiscrepancy, new[] { new WorkShiftCashDiscrepancyRow { CashDiscrepancy = 5 }, new WorkShiftCashDiscrepancyRow { CashDiscrepancy = -2 } }, 3m];
        yield return [DashboardAnalyticsWidget.WorkShiftSales, new[] { new WorkShiftSalesRow { NetSales = 5_000_000 }, new WorkShiftSalesRow { NetSales = 7_000_000 } }, 12_000_000m];
        yield return [DashboardAnalyticsWidget.InventoryMovementByType, new[] { new InventoryMovementRow { Quantity = 4 }, new InventoryMovementRow { Quantity = -1 } }, 3m];
        yield return [DashboardAnalyticsWidget.InventoryThresholdRisk, new[] { new InventoryThresholdRiskRow { RiskIngredientCount = 1 }, new InventoryThresholdRiskRow { RiskIngredientCount = 0 } }, 1m];
        yield return [DashboardAnalyticsWidget.PurchaseOrderPipeline, new[] { new PurchaseOrderPipelineRow { PurchaseOrderCount = 4 }, new PurchaseOrderPipelineRow { PurchaseOrderCount = 6 } }, 10m];
        yield return [DashboardAnalyticsWidget.SizeMargin, new[] { new SizeMarginRow { ConfirmedGrossProfit = 100 }, new SizeMarginRow { ConfirmedGrossProfit = -25 } }, 75m];
        yield return [DashboardAnalyticsWidget.TopToppings, new[] { new TopToppingAnalyticsRow { Revenue = 120 }, new TopToppingAnalyticsRow { Revenue = 80 } }, 200m];
        yield return [DashboardAnalyticsWidget.BomHealth, new[] { new BomHealthRow { BomIssueCount = 2 }, new BomHealthRow { BomIssueCount = 1 } }, 3m];
        yield return [DashboardAnalyticsWidget.WorkforceStaffPerformance, new[] { new WorkforceStaffPerformanceRow { TotalOrders = 9, WorkShiftCount = 2 }, new WorkforceStaffPerformanceRow { TotalOrders = 6, WorkShiftCount = 1 } }, 5m];
        yield return [DashboardAnalyticsWidget.OperationalAlerts, new[] { new OperationalAlertRow { AlertCount = 1 }, new OperationalAlertRow { AlertCount = 1 } }, 2m];
        yield return [DashboardAnalyticsWidget.WorkforceShiftStatus, new[] { new WorkforceShiftStatusRow { ShiftCount = 1 }, new WorkforceShiftStatusRow { ShiftCount = 1 } }, 2m];
        yield return [DashboardAnalyticsWidget.IngredientConsumptionTrend, new[] { new IngredientConsumptionTrendRow { ConsumedQuantity = 2 }, new IngredientConsumptionTrendRow { ConsumedQuantity = 3 } }, 5m];
    }

    [Theory]
    [MemberData(nameof(ExplicitMetricCases))]
    public void ExplicitMetricCasesDoNotUseRowCount(
        DashboardAnalyticsWidget widget,
        object rows,
        decimal expected)
    {
        var method = typeof(DashboardIntelligenceService).GetMethod(
            "Metric", BindingFlags.Static | BindingFlags.NonPublic)!;
        var metric = method.Invoke(null, [widget, rows])!;
        Assert.Equal(expected, (decimal)metric.GetType().GetProperty("Value")!.GetValue(metric)!);
    }

    [Fact]
    public void MissingMetricFieldFailsClosed()
    {
        var method = typeof(DashboardIntelligenceService).GetMethod(
            "Metric", BindingFlags.Static | BindingFlags.NonPublic)!;
        var error = Assert.Throws<TargetInvocationException>(() =>
            method.Invoke(null, [DashboardAnalyticsWidget.WorkShiftSales, new[] { new { netSales = 1m } }]));
        Assert.IsType<InvalidOperationException>(error.InnerException);
    }
}
