using CafeChain.Application.DTOs.Admin.Dashboard;

namespace CafeChain.Application.Services.Admin.Dashboard;

internal sealed record DashboardWidgetDefinition(
    DashboardAnalyticsWidget Widget,
    DashboardSection Section,
    string Title,
    DashboardChartType ChartType,
    string LabelField,
    string ValueField,
    string Unit,
    bool SupportsComparison = true,
    string XField = "",
    string YField = "",
    string SeriesField = "",
    string XUnit = "",
    string YUnit = "",
    int MinimumRows = 1);

internal static class DashboardWidgetCatalog
{
    private static readonly IReadOnlyDictionary<DashboardAnalyticsWidget, DashboardWidgetDefinition> Definitions =
        Enum.GetValues<DashboardAnalyticsWidget>().ToDictionary(widget => widget, Create);

    private static readonly IReadOnlyDictionary<string, string> Labels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bucketDate"] = "Ngày", ["storeId"] = "Mã cửa hàng", ["storeName"] = "Cửa hàng",
            ["totalOrders"] = "Tổng đơn", ["completedOrders"] = "Đơn hoàn tất",
            ["cancelledOrders"] = "Đơn hủy", ["cancellationRate"] = "Tỷ lệ hủy",
            ["netSales"] = "Doanh thu thuần", ["averageOrderValue"] = "Giá trị đơn trung bình",
            ["paymentMethodName"] = "Phương thức thanh toán", ["amount"] = "Giá trị",
            ["share"] = "Tỷ trọng", ["isoWeekday"] = "Thứ", ["hourOfDay"] = "Giờ",
            ["alertType"] = "Loại cảnh báo", ["severity"] = "Mức độ", ["message"] = "Nội dung",
            ["ingredientCode"] = "Mã nguyên liệu", ["ingredientName"] = "Nguyên liệu",
            ["onHandQuantity"] = "Tồn thực tế", ["reservedQuantity"] = "Đã giữ",
            ["availableQuantity"] = "Tồn khả dụng", ["minimumStock"] = "Tồn tối thiểu",
            ["shortageQuantity"] = "Mức thiếu", ["suggestedReorderQuantity"] = "Đề xuất đặt",
            ["requestedQuantity"] = "Số lượng yêu cầu", ["suggestedQuantity"] = "Số lượng đề xuất",
            ["wasteQuantity"] = "Lượng hao hụt", ["wasteValue"] = "Giá trị hao hụt",
            ["supplierName"] = "Nhà cung cấp", ["rejectionRate"] = "Tỷ lệ từ chối",
            ["acceptedBaseQuantity"] = "Số lượng đạt", ["rejectedBaseQuantity"] = "Số lượng bị từ chối",
            ["averageBaseUnitCost"] = "Giá mua bình quân", ["spend"] = "Chi phí mua",
            ["issueType"] = "Loại sự cố", ["issueCount"] = "Số sự cố",
            ["purchaseOrderCount"] = "Số đơn mua", ["orderedValue"] = "Giá trị đặt mua",
            ["overdueDays"] = "Số ngày quá hạn", ["drinkName"] = "Sản phẩm",
            ["categoryName"] = "Danh mục", ["totalSold"] = "Số lượng bán",
            ["productRevenue"] = "Doanh thu sản phẩm", ["revenue"] = "Doanh thu",
            ["confirmedCogs"] = "COGS xác nhận", ["confirmedGrossProfit"] = "Lợi nhuận gộp",
            ["confirmedMarginRate"] = "Biên lợi nhuận", ["volume"] = "Số lượng",
            ["sizeName"] = "Kích cỡ", ["toppingName"] = "Topping", ["totalUsed"] = "Số lượng dùng",
            ["shiftName"] = "Ca làm", ["fullName"] = "Nhân viên",
            ["scheduledStaffCount"] = "Nhân sự đã xếp", ["ordersPerWorkShift"] = "Đơn trên ca",
            ["cashDiscrepancy"] = "Chênh lệch tiền mặt", ["absoluteDiscrepancy"] = "Chênh lệch tuyệt đối",
            ["movementDate"] = "Ngày", ["quantity"] = "Số lượng", ["totalCost"] = "Tổng chi phí",
            ["consumedQuantity"] = "Lượng tiêu thụ", ["confirmedCost"] = "Chi phí xác nhận",
            ["status"] = "Trạng thái", ["riskLevel"] = "Mức rủi ro"
        };

    public static DashboardWidgetDefinition Get(DashboardAnalyticsWidget widget) => Definitions[widget];

    public static IReadOnlyList<DashboardWidgetMetadataDto> Metadata() =>
        Definitions.Values.Select(definition => new DashboardWidgetMetadataDto
        {
            Widget = definition.Widget,
            Section = definition.Section,
            Key = definition.Widget.ToString(),
            Title = definition.Title,
            ChartType = definition.ChartType,
            LabelField = definition.LabelField,
            ValueField = definition.ValueField,
            XField = string.IsNullOrWhiteSpace(definition.XField) ? definition.LabelField : definition.XField,
            YField = string.IsNullOrWhiteSpace(definition.YField) ? definition.ValueField : definition.YField,
            SeriesField = definition.SeriesField,
            XUnit = definition.XUnit,
            YUnit = string.IsNullOrWhiteSpace(definition.YUnit) ? definition.Unit : definition.YUnit,
            Unit = definition.Unit,
            MinimumRows = definition.MinimumRows,
            FieldLabels = Labels,
            SupportsComparison = definition.SupportsComparison
        }).ToList();

    public static IReadOnlyDictionary<string, string> FieldLabels => Labels;

    private static DashboardWidgetDefinition Create(DashboardAnalyticsWidget widget) => widget switch
    {
        DashboardAnalyticsWidget.NetSalesTrend => D(widget, DashboardSection.Executive, "Xu hướng doanh thu", DashboardChartType.Line, "bucketDate", "netSales", "VND", x: "bucketDate", y: "netSales", xUnit: "DAY", minimum: 2),
        DashboardAnalyticsWidget.StoreRanking => D(widget, DashboardSection.Executive, "Xếp hạng cửa hàng", DashboardChartType.HorizontalBar, "storeName", "netSales", "VND"),
        DashboardAnalyticsWidget.PaymentMethodMix => D(widget, DashboardSection.Executive, "Cơ cấu thanh toán", DashboardChartType.Donut, "paymentMethodName", "amount", "VND"),
        DashboardAnalyticsWidget.OrderHeatmap => D(widget, DashboardSection.Executive, "Phân bố đơn theo ngày và giờ", DashboardChartType.Heatmap, "isoWeekday", "totalOrders", "ORDER", false, "hourOfDay", "isoWeekday", "", "HOUR", "DAY", 2),
        DashboardAnalyticsWidget.OperationalAlerts => D(widget, DashboardSection.Executive, "Cảnh báo vận hành", DashboardChartType.Table, "alertType", "alertValue", "COUNT", false),
        DashboardAnalyticsWidget.OrderStatusSummary => D(widget, DashboardSection.Executive, "Tình trạng đơn hàng", DashboardChartType.Bar, "storeName", "totalOrders", "ORDER"),

        DashboardAnalyticsWidget.WorkShiftCashDiscrepancy => D(widget, DashboardSection.Operations, "Chênh lệch tiền mặt theo ca", DashboardChartType.HorizontalBar, "storeName", "cashDiscrepancy", "VND", false),
        DashboardAnalyticsWidget.WorkShiftSales => D(widget, DashboardSection.Operations, "Doanh thu theo ca", DashboardChartType.Bar, "workShiftId", "netSales", "VND"),
        DashboardAnalyticsWidget.WorkShiftPaymentMix => D(widget, DashboardSection.Operations, "Thanh toán theo ca", DashboardChartType.StackedBar, "workShiftId", "amount", "VND", true, series: "paymentMethodName"),
        DashboardAnalyticsWidget.OfflineReconciliationExceptions => D(widget, DashboardSection.Operations, "Đối soát đơn ngoại tuyến", DashboardChartType.Table, "workShiftId", "offlineEstimatedTotalAtClose", "VND", false),
        DashboardAnalyticsWidget.HourlyOrders => D(widget, DashboardSection.Operations, "Đơn hàng theo giờ", DashboardChartType.Bar, "hourOfDay", "totalOrders", "ORDER", true, "hourOfDay", "totalOrders", "", "HOUR"),
        DashboardAnalyticsWidget.WorkShiftTopDiscrepancies => D(widget, DashboardSection.Operations, "Ca có chênh lệch tiền mặt cao", DashboardChartType.HorizontalBar, "workShiftId", "absoluteDiscrepancy", "VND", false),
        DashboardAnalyticsWidget.WorkShiftKpis => D(widget, DashboardSection.Operations, "Chỉ số vận hành ca", DashboardChartType.Kpi, "totalWorkShifts", "absoluteCashDiscrepancy", "VND"),

        DashboardAnalyticsWidget.InventoryShortageRisk => D(widget, DashboardSection.Inventory, "Nguyên liệu dưới ngưỡng tồn", DashboardChartType.HorizontalBar, "ingredientName", "shortageQuantity", "INGREDIENT", false),
        DashboardAnalyticsWidget.InventoryMovementByType => D(widget, DashboardSection.Inventory, "Biến động kho", DashboardChartType.StackedBar, "movementDate", "quantity", "INGREDIENT", true, series: "transactionType"),
        DashboardAnalyticsWidget.InventoryThresholdRisk => D(widget, DashboardSection.Inventory, "Rủi ro ngưỡng tồn kho", DashboardChartType.HorizontalBar, "ingredientName", "quantityAboveMinimum", "INGREDIENT", false),
        DashboardAnalyticsWidget.InventoryReorderSuggestions => D(widget, DashboardSection.Inventory, "Đề xuất đặt bổ sung", DashboardChartType.HorizontalBar, "ingredientName", "suggestedQuantity", "INGREDIENT", false),
        DashboardAnalyticsWidget.InventoryWasteByStoreIngredient => D(widget, DashboardSection.Inventory, "Hao hụt kho", DashboardChartType.Bar, "ingredientName", "wasteValue", "VND"),
        DashboardAnalyticsWidget.InventoryFifoLayerAge => D(widget, DashboardSection.Inventory, "Tuổi lớp tồn FIFO", DashboardChartType.Scatter, "ageDays", "remainingValue", "VND", false, "ageDays", "remainingValue", "", "DAY", "VND", 2),
        DashboardAnalyticsWidget.IngredientConsumptionTrend => D(widget, DashboardSection.Inventory, "Xu hướng tiêu thụ nguyên liệu", DashboardChartType.Line, "bucketDate", "consumedQuantity", "INGREDIENT", true, "bucketDate", "consumedQuantity", "ingredientName", "DAY", "INGREDIENT", 2),

        DashboardAnalyticsWidget.PurchaseOrderPipeline => D(widget, DashboardSection.Procurement, "Tiến độ đơn mua hàng", DashboardChartType.Donut, "status", "purchaseOrderCount", "COUNT", false),
        DashboardAnalyticsWidget.OverduePurchaseOrders => D(widget, DashboardSection.Procurement, "Đơn mua hàng quá hạn", DashboardChartType.HorizontalBar, "code", "overdueDays", "DAY", false),
        DashboardAnalyticsWidget.SupplierQuality => D(widget, DashboardSection.Procurement, "Chất lượng nhà cung cấp", DashboardChartType.HorizontalBar, "supplierName", "rejectionRate", "PERCENT"),
        DashboardAnalyticsWidget.PurchasePriceTrend => D(widget, DashboardSection.Procurement, "Xu hướng giá mua", DashboardChartType.Line, "receiptDate", "averageBaseUnitCost", "VND", true, "receiptDate", "averageBaseUnitCost", "ingredientName", "DAY", "VND", 2),
        DashboardAnalyticsWidget.ProcurementSpendBreakdown => D(widget, DashboardSection.Procurement, "Chi phí mua hàng", DashboardChartType.HorizontalBar, "supplierName", "spend", "VND"),
        DashboardAnalyticsWidget.SupplierIssueMix => D(widget, DashboardSection.Procurement, "Sự cố nhà cung cấp", DashboardChartType.Bar, "supplierName", "issueCount", "COUNT", false, series: "issueType"),

        DashboardAnalyticsWidget.TopProducts => D(widget, DashboardSection.Product, "Sản phẩm bán chạy", DashboardChartType.HorizontalBar, "drinkName", "productRevenue", "VND"),
        DashboardAnalyticsWidget.VolumeMarginMatrix => D(widget, DashboardSection.Product, "Số lượng và biên lợi nhuận", DashboardChartType.Scatter, "volume", "confirmedMarginRate", "PERCENT", true, "volume", "confirmedMarginRate", "drinkName", "PRODUCT", "PERCENT", 2),
        DashboardAnalyticsWidget.SizeMargin => D(widget, DashboardSection.Product, "Hiệu quả theo kích cỡ", DashboardChartType.Bar, "sizeName", "confirmedGrossProfit", "VND"),
        DashboardAnalyticsWidget.TopToppings => D(widget, DashboardSection.Product, "Topping bán chạy", DashboardChartType.HorizontalBar, "toppingName", "revenue", "VND"),
        DashboardAnalyticsWidget.BomHealth => D(widget, DashboardSection.Product, "Tình trạng BOM", DashboardChartType.Table, "drinkName", "invalidLineCount", "COUNT", false),
        DashboardAnalyticsWidget.HighConsumptionLowEfficiency => D(widget, DashboardSection.Product, "Sản phẩm tiêu thụ cao, hiệu quả thấp", DashboardChartType.HorizontalBar, "drinkName", "confirmedGrossProfit", "VND"),
        DashboardAnalyticsWidget.CategoryPerformance => D(widget, DashboardSection.Product, "Hiệu quả danh mục", DashboardChartType.HorizontalBar, "categoryName", "revenue", "VND"),
        DashboardAnalyticsWidget.ProductPeriodPerformance => D(widget, DashboardSection.Product, "Hiệu quả sản phẩm theo kỳ", DashboardChartType.HorizontalBar, "drinkName", "revenue", "VND"),

        DashboardAnalyticsWidget.WorkforceShiftStatus => D(widget, DashboardSection.Workforce, "Tình trạng ca nhân sự", DashboardChartType.Table, "shiftName", "staffShiftId", "COUNT", false),
        DashboardAnalyticsWidget.WorkforceHourlyDemand => D(widget, DashboardSection.Workforce, "Nhu cầu nhân sự theo giờ", DashboardChartType.Line, "hourOfDay", "totalOrders", "ORDER", true, "hourOfDay", "totalOrders", "", "HOUR", "ORDER", 2),
        DashboardAnalyticsWidget.WorkforceStaffPerformance => D(widget, DashboardSection.Workforce, "Hiệu suất nhân sự", DashboardChartType.HorizontalBar, "fullName", "ordersPerWorkShift", "ORDER")
    };

    private static DashboardWidgetDefinition D(
        DashboardAnalyticsWidget widget, DashboardSection section, string title, DashboardChartType chart,
        string label, string value, string unit, bool comparison = true, string x = "", string y = "",
        string series = "", string xUnit = "", string yUnit = "", int minimum = 1) =>
        new(widget, section, title, chart, label, value, unit, comparison, x, y, series, xUnit, yUnit, minimum);
}
