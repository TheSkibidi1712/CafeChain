using System.Globalization;
using System.Text;
using CafeChain.Application.DTOs.Admin.Dashboard;

namespace CafeChain.Application.Services.Admin.Dashboard;

internal static class DashboardQuestionCatalog
{
    public static IReadOnlyList<DashboardGuideQuestionGroupDto> GetGuideQuestionGroups()
    {
        return
        [
            GuideGroup(
                "Tổng quan và doanh thu",
                [
                    ("Tôi nên chú ý điều gì trong kỳ đang chọn?", DashboardAnswerFocus.OperationalPriorities),
                    ("So sánh doanh thu kỳ này với kỳ trước.", DashboardAnswerFocus.RevenueComparison),
                    ("Chi nhánh nào đang hoạt động kém hơn?", DashboardAnswerFocus.StoreUnderperformance),
                    ("Doanh thu giảm có thể liên quan đến sản phẩm, số đơn hay giá trị đơn hàng?",
                        DashboardAnswerFocus.RevenueDriver),
                    ("Tạo thống kê doanh thu theo ngày trong kỳ đang chọn.",
                        DashboardAnswerFocus.DailyRevenueStatistics)
                ]),
            GuideGroup(
                "Đơn hàng và sản phẩm",
                [
                    ("Phân tích số đơn và tỷ lệ hủy theo chi nhánh.",
                        DashboardAnswerFocus.OrderCancellationByStore),
                    ("Phương thức thanh toán nào được sử dụng nhiều nhất?",
                        DashboardAnswerFocus.PaymentUsage),
                    ("Top 10 sản phẩm bán chạy nhất trong kỳ là gì?",
                        DashboardAnswerFocus.TopSellingProducts),
                    ("Danh mục nào bán chạy nhất trong kỳ?",
                        DashboardAnswerFocus.TopSellingCategories),
                    ("Sản phẩm nào bán chậm nhất trong kỳ?",
                        DashboardAnswerFocus.LowVolumeProducts),
                    ("Sản phẩm nào có biên lợi nhuận thấp nhất trong kỳ?",
                        DashboardAnswerFocus.LowMarginProducts)
                ]),
            GuideGroup(
                "Kho và đặt hàng",
                [
                    ("Nguyên liệu nào đang có nguy cơ thiếu?", DashboardAnswerFocus.InventoryShortage),
                    ("Nguyên liệu nào nên được đặt lại trước?", DashboardAnswerFocus.ReorderPriority),
                    ("Phân tích xu hướng tiêu thụ nguyên liệu trong kỳ.",
                        DashboardAnswerFocus.IngredientConsumptionTrend)
                ]),
            GuideGroup(
                "Nhà cung cấp và bất thường",
                [
                    ("Nhà cung cấp nào có rủi ro chất lượng hoặc đơn mua quá hạn?",
                        DashboardAnswerFocus.SupplierAndOverdueRisk),
                    ("Có bất thường vận hành nào cần chú ý không?",
                        DashboardAnswerFocus.OperationalAnomaly)
                ])
        ];
    }

    public static DashboardQuestionUnderstandingDto Understand(
        string? question,
        DashboardPeriodDto period,
        DashboardComparison comparison,
        string granularity,
        int top)
    {
        var original = question?.Trim() ?? string.Empty;
        var normalized = Normalize(original);
        var focus = ResolveFocus(normalized);
        var profile = Profile(focus, normalized);
        return new DashboardQuestionUnderstandingDto
        {
            OriginalQuestion = original,
            NormalizedQuestion = normalized,
            BusinessIntent = profile.Intent,
            AnswerFocus = focus,
            FocusType = focus == DashboardAnswerFocus.Dynamic
                ? DashboardFocusType.Dynamic
                : DashboardFocusType.Canonical,
            DynamicFocus = focus == DashboardAnswerFocus.Dynamic
                ? BuildDynamicFocus(normalized)
                : null,
            FocusConfidence = focus == DashboardAnswerFocus.Dynamic ? 0.65m : 1m,
            TabCode = profile.TabCode,
            AnswerStyleId = profile.AnswerStyleId,
            PrimaryEntity = profile.Entity,
            PrimaryMetric = profile.Metric,
            SecondaryMetrics = profile.SecondaryMetrics.ToList(),
            Dimensions = profile.Dimensions.ToList(),
            GroupBy = profile.Dimensions.ToList(),
            RankingDirection = profile.SortDirection,
            RequestedLimit = top,
            TimeRange = period,
            ComparisonPeriod = comparison,
            TimeGrain = granularity,
            RequestedOutput = ["ANALYSIS_CONTEXT", "CHART", "EVIDENCE_TABLE"],
            ExplicitExclusions = profile.Exclusions.ToList(),
            RequiresRanking = profile.RequiresRanking,
            RequiresTrend = profile.RequiresTrend,
            RequiresComparison = profile.RequiresComparison || comparison != DashboardComparison.None,
            RequiresComposition = profile.RequiresComposition,
            RequiresAnomalyDetection = profile.RequiresAnomaly,
            RequiresRecommendation = profile.RequiresRecommendation
                || normalized.Contains("nen lam gi", StringComparison.Ordinal)
                || normalized.Contains("can lam gi", StringComparison.Ordinal),
            IsDashboardQuestion = true,
            IsAmbiguous = focus == DashboardAnswerFocus.Dynamic
        };
    }

    public static DashboardDataPlanDto CreateDataPlan(
        DashboardQuestionUnderstandingDto understanding,
        IReadOnlyList<int> effectiveStoreIds,
        DateTime from,
        DateTime to)
    {
        var mapping = Widgets(understanding.AnswerFocus, understanding.NormalizedQuestion);
        var definition = DashboardWidgetCatalog.Get(mapping.Primary);
        return new DashboardDataPlanDto
        {
            PlanId = string.Join(
                ":",
                understanding.AnswerFocus.ToString().ToUpperInvariant(),
                understanding.PrimaryMetric.ToUpperInvariant(),
                string.Join("-", understanding.Dimensions).ToUpperInvariant(),
                understanding.ComparisonPeriod.ToString().ToUpperInvariant(),
                understanding.RequestedLimit),
            AnalysisGoal = Goal(understanding.AnswerFocus),
            RequiredDataSources = new[] { mapping.Primary }
                .Concat(mapping.Supporting)
                .Select(x => x.ToString())
                .ToList(),
            RequiredFields = RequiredFields(understanding.AnswerFocus).ToList(),
            RequiredMetrics = new[] { understanding.PrimaryMetric }
                .Concat(understanding.SecondaryMetrics)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["fromDate"] = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["toDate"] = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["storeIds"] = string.Join(",", effectiveStoreIds)
            },
            EffectiveStoreIds = effectiveStoreIds.ToList(),
            FromDate = from,
            ToDate = to,
            GroupBy = understanding.GroupBy.ToList(),
            SortBy = understanding.PrimaryMetric,
            SortDirection = understanding.RankingDirection,
            Limit = understanding.RequestedLimit,
            ComparisonDefinition = understanding.ComparisonPeriod,
            TimeGrain = understanding.TimeGrain,
            PrimaryWidget = mapping.Primary,
            SupportingWidgets = mapping.Supporting.ToList(),
            DataQualityRules = QualityRules(understanding.AnswerFocus).ToList(),
            FallbackPattern = FallbackFamily(understanding.AnswerFocus)
        };
    }

    public static DashboardAnswerStyleProfileDto AnswerStyle(DashboardAnswerFocus focus)
    {
        var profile = Profile(focus, string.Empty);
        return new DashboardAnswerStyleProfileDto
        {
            TabCode = profile.TabCode,
            AnswerStyleId = profile.AnswerStyleId,
            OpeningPattern = focus switch
            {
                DashboardAnswerFocus.RevenueComparison => "Doanh thu kỳ này đạt...",
                DashboardAnswerFocus.TopSellingProducts => "Sản phẩm đứng đầu về số lượng bán là...",
                DashboardAnswerFocus.InventoryShortage => "Nguyên liệu có nguy cơ thiếu cao nhất là...",
                DashboardAnswerFocus.ReorderPriority => "Nguyên liệu cần đặt lại trước là...",
                DashboardAnswerFocus.SupplierAndOverdueRisk => "Nhà cung cấp có rủi ro cao nhất là...",
                DashboardAnswerFocus.OperationalAnomaly => "Hệ thống ghi nhận bất thường...",
                _ => "Kết luận chính theo đúng mục tiêu câu hỏi..."
            },
            EvidencePattern = "Nêu metric chính và EvidenceId chứng minh.",
            ChartInterpretationPattern = "Dẫn ít nhất một số liệu xuất hiện trên biểu đồ chính.",
            LimitationPattern = "Nêu rõ Partial/Insufficient và trường dữ liệu còn thiếu.",
            ClosingPattern = focus switch
            {
                DashboardAnswerFocus.ReorderPriority => "Kết thúc bằng thứ tự ưu tiên hoặc số lượng đề xuất khi đủ dữ liệu.",
                DashboardAnswerFocus.OperationalAnomaly => "Kết thúc bằng giới hạn và bước xác minh.",
                _ => "Kết thúc bằng kết luận trực tiếp, không mở rộng chủ đề."
            }
        };
    }

    private static DashboardGuideQuestionGroupDto GuideGroup(
        string name,
        IReadOnlyList<(string Question, DashboardAnswerFocus Focus)> questions)
    {
        return new DashboardGuideQuestionGroupDto
        {
            Name = name,
            Questions = questions.Select(item =>
            {
                var profile = Profile(item.Focus, Normalize(item.Question));
                var widgets = Widgets(item.Focus, Normalize(item.Question));
                return new DashboardGuideQuestionDto
                {
                    Question = item.Question,
                    ExpectedAnswerFocus = item.Focus,
                    PrimaryWidget = widgets.Primary,
                    AnswerStyleId = profile.AnswerStyleId
                };
            }).ToList()
        };
    }

    private static DashboardAnswerFocus ResolveFocus(string text)
    {
        if (string.IsNullOrWhiteSpace(text)
            || text.Contains("nen chu y", StringComparison.Ordinal)
            || text.Contains("tong quan", StringComparison.Ordinal))
            return DashboardAnswerFocus.OperationalPriorities;
        if (text.Contains("doanh thu theo ngay", StringComparison.Ordinal)
            || text.Contains("thong ke doanh thu", StringComparison.Ordinal))
            return DashboardAnswerFocus.DailyRevenueStatistics;
        if (text.Contains("doanh thu", StringComparison.Ordinal)
            && (text.Contains("so sanh", StringComparison.Ordinal)
                || text.Contains("ky truoc", StringComparison.Ordinal)))
            return DashboardAnswerFocus.RevenueComparison;
        if ((text.Contains("chi nhanh", StringComparison.Ordinal)
                || text.Contains("cua hang", StringComparison.Ordinal))
            && (text.Contains("kem", StringComparison.Ordinal)
                || text.Contains("thap nhat", StringComparison.Ordinal)))
            return DashboardAnswerFocus.StoreUnderperformance;
        if (text.Contains("doanh thu", StringComparison.Ordinal)
            && (text.Contains("lien quan", StringComparison.Ordinal)
                || text.Contains("nguyen nhan", StringComparison.Ordinal)
                || text.Contains("yeu to", StringComparison.Ordinal)))
            return DashboardAnswerFocus.RevenueDriver;
        if (text.Contains("huy", StringComparison.Ordinal)
            && text.Contains("don", StringComparison.Ordinal)
            && !text.Contains("cuoi tuan", StringComparison.Ordinal))
            return DashboardAnswerFocus.OrderCancellationByStore;
        if (text.Contains("thanh toan", StringComparison.Ordinal))
            return DashboardAnswerFocus.PaymentUsage;
        if ((text.Contains("top", StringComparison.Ordinal)
                || text.Contains("ban chay", StringComparison.Ordinal))
            && text.Contains("danh muc", StringComparison.Ordinal)
            && !text.Contains("san pham", StringComparison.Ordinal))
            return DashboardAnswerFocus.TopSellingCategories;
        if (text.Contains("ban cham", StringComparison.Ordinal)
            || text.Contains("san luong thap", StringComparison.Ordinal))
            return DashboardAnswerFocus.LowVolumeProducts;
        if (text.Contains("bien loi nhuan", StringComparison.Ordinal)
            || text.Contains("margin", StringComparison.Ordinal))
            return DashboardAnswerFocus.LowMarginProducts;
        if ((text.Contains("top", StringComparison.Ordinal)
                || text.Contains("ban chay", StringComparison.Ordinal))
            && (text.Contains("san pham", StringComparison.Ordinal)
                || text.Contains("mon", StringComparison.Ordinal)))
            return DashboardAnswerFocus.TopSellingProducts;
        if (text.Contains("tieu thu", StringComparison.Ordinal)
            && text.Contains("nguyen lieu", StringComparison.Ordinal)
            && (text.Contains("xu huong", StringComparison.Ordinal)
                || text.Contains("theo ngay", StringComparison.Ordinal)
                || text.Contains("theo tuan", StringComparison.Ordinal)))
            return DashboardAnswerFocus.IngredientConsumptionTrend;
        if (text.Contains("dat lai", StringComparison.Ordinal)
            || text.Contains("nen dat", StringComparison.Ordinal)
            || text.Contains("goi y nhap", StringComparison.Ordinal)
            || text.Contains("reorder", StringComparison.Ordinal))
            return DashboardAnswerFocus.ReorderPriority;
        if (text.Contains("thieu", StringComparison.Ordinal)
            || text.Contains("sap het", StringComparison.Ordinal)
            || text.Contains("ton kho", StringComparison.Ordinal))
            return DashboardAnswerFocus.InventoryShortage;
        if (text.Contains("nha cung cap", StringComparison.Ordinal)
            || text.Contains("qua han", StringComparison.Ordinal)
            || text.Contains("giao tre", StringComparison.Ordinal))
            return DashboardAnswerFocus.SupplierAndOverdueRisk;
        if (text.Contains("bat thuong", StringComparison.Ordinal)
            || text.Contains("canh bao", StringComparison.Ordinal))
            return DashboardAnswerFocus.OperationalAnomaly;
        return DashboardAnswerFocus.Dynamic;
    }

    private static FocusProfile Profile(DashboardAnswerFocus focus, string normalized) => focus switch
    {
        DashboardAnswerFocus.OperationalPriorities => P(DashboardBusinessIntent.GeneralBusinessSummary,
            DashboardTabCodes.OverviewRevenue, DashboardAnswerStyleIds.ExecutiveDiagnostic,
            "Operation", "AlertCount", ["Impact"], ["Severity"], anomaly: true, recommendation: true),
        DashboardAnswerFocus.RevenueComparison => P(DashboardBusinessIntent.RevenueAnalysis,
            DashboardTabCodes.OverviewRevenue, DashboardAnswerStyleIds.ExecutiveDiagnostic,
            "Revenue", "NetSales", ["TotalOrders", "AverageOrderValue"], ["Period"], comparison: true),
        DashboardAnswerFocus.StoreUnderperformance => P(DashboardBusinessIntent.StoreComparison,
            DashboardTabCodes.OverviewRevenue, DashboardAnswerStyleIds.ExecutiveDiagnostic,
            "Store", "NetSales", ["TotalOrders", "AverageOrderValue"], ["Store"], ranking: true, sort: "ASC"),
        DashboardAnswerFocus.RevenueDriver => P(DashboardBusinessIntent.RevenueAnalysis,
            DashboardTabCodes.OverviewRevenue, DashboardAnswerStyleIds.ExecutiveDiagnostic,
            "Revenue", "NetSales", ["TotalOrders", "AverageOrderValue"], ["Period"], comparison: true),
        DashboardAnswerFocus.DailyRevenueStatistics => P(DashboardBusinessIntent.StatisticsRequest,
            DashboardTabCodes.OverviewRevenue, DashboardAnswerStyleIds.ExecutiveDiagnostic,
            "Revenue", "NetSales", ["Average", "Minimum", "Maximum"], ["Day"], trend: true),
        DashboardAnswerFocus.OrderCancellationByStore => P(DashboardBusinessIntent.OrderAnalysis,
            DashboardTabCodes.OrdersProducts, DashboardAnswerStyleIds.TransactionRankingAnalysis,
            "Order", "CancellationRate", ["CancelledOrders", "TotalOrders"], ["Store"], ranking: true),
        DashboardAnswerFocus.PaymentUsage => P(DashboardBusinessIntent.OrderAnalysis,
            DashboardTabCodes.OrdersProducts, DashboardAnswerStyleIds.TransactionRankingAnalysis,
            "PaymentMethod", "TotalTransactions", ["TransactionShare"], ["PaymentMethod"], ranking: true),
        DashboardAnswerFocus.TopSellingProducts => P(DashboardBusinessIntent.ProductPerformance,
            DashboardTabCodes.OrdersProducts, DashboardAnswerStyleIds.TransactionRankingAnalysis,
            "Product", "TotalSold", ["NetSales", "QuantityShare"], ["Product"], ranking: true,
            exclusions: ["INVENTORY", "SUPPLIER", "PAYMENT", "MARGIN"]),
        DashboardAnswerFocus.TopSellingCategories => P(DashboardBusinessIntent.ProductPerformance,
            DashboardTabCodes.OrdersProducts, DashboardAnswerStyleIds.TransactionRankingAnalysis,
            "Category", "TotalSold", ["NetSales"], ["Category"], ranking: true),
        DashboardAnswerFocus.LowVolumeProducts => P(DashboardBusinessIntent.ProductPerformance,
            DashboardTabCodes.OrdersProducts, DashboardAnswerStyleIds.TransactionRankingAnalysis,
            "Product", "TotalSold", ["NetSales"], ["Product"], ranking: true, sort: "ASC",
            exclusions: ["MARGIN"]),
        DashboardAnswerFocus.LowMarginProducts => P(DashboardBusinessIntent.ProductPerformance,
            DashboardTabCodes.OrdersProducts, DashboardAnswerStyleIds.TransactionRankingAnalysis,
            "Product", "MarginPercent", ["TotalSold", "NetSales", "COGS"], ["Product"], ranking: true, sort: "ASC"),
        DashboardAnswerFocus.InventoryShortage => P(DashboardBusinessIntent.InventoryAnalysis,
            DashboardTabCodes.InventoryReorder, DashboardAnswerStyleIds.OperationalActionAnalysis,
            "Ingredient", "ShortageQuantity", ["AvailableQuantity", "MinimumStock"], ["Ingredient"], ranking: true),
        DashboardAnswerFocus.ReorderPriority => P(DashboardBusinessIntent.ReorderAnalysis,
            DashboardTabCodes.InventoryReorder, DashboardAnswerStyleIds.OperationalActionAnalysis,
            "Ingredient", "FinalSuggestedQuantity", ["AvailableQuantity", "MinimumStock", "LeadTimeDays"],
            ["Ingredient"], ranking: true, recommendation: true),
        DashboardAnswerFocus.IngredientConsumptionTrend => P(DashboardBusinessIntent.InventoryAnalysis,
            DashboardTabCodes.InventoryReorder, DashboardAnswerStyleIds.OperationalActionAnalysis,
            "Ingredient", "ConsumedQuantity", ["ConfirmedCost"], ["Day", "Ingredient"], trend: true),
        DashboardAnswerFocus.SupplierAndOverdueRisk => P(DashboardBusinessIntent.SupplierAnalysis,
            DashboardTabCodes.SupplierAnomaly, DashboardAnswerStyleIds.RiskInvestigationAnalysis,
            "Supplier", "RiskScore", ["OverdueOrderCount", "QualityIssueCount", "RejectionRate"],
            ["Supplier"], ranking: true, anomaly: true),
        DashboardAnswerFocus.OperationalAnomaly => P(DashboardBusinessIntent.AnomalyDetection,
            DashboardTabCodes.SupplierAnomaly, DashboardAnswerStyleIds.RiskInvestigationAnalysis,
            "Operation", "AlertCount", ["Severity", "Impact"], ["AlertType"], anomaly: true),
        _ => DynamicProfile(normalized)
    };

    private static (DashboardAnalyticsWidget Primary, IReadOnlyList<DashboardAnalyticsWidget> Supporting)
        Widgets(DashboardAnswerFocus focus, string normalized) => focus switch
    {
        DashboardAnswerFocus.OperationalPriorities =>
            (DashboardAnalyticsWidget.OperationalAlerts, [DashboardAnalyticsWidget.StoreRanking]),
        DashboardAnswerFocus.RevenueComparison =>
            (DashboardAnalyticsWidget.NetSalesTrend, [DashboardAnalyticsWidget.StoreRanking]),
        DashboardAnswerFocus.StoreUnderperformance =>
            (DashboardAnalyticsWidget.StoreRanking, []),
        DashboardAnswerFocus.RevenueDriver =>
            (DashboardAnalyticsWidget.NetSalesTrend, [DashboardAnalyticsWidget.StoreRanking]),
        DashboardAnswerFocus.DailyRevenueStatistics =>
            (DashboardAnalyticsWidget.NetSalesTrend, []),
        DashboardAnswerFocus.OrderCancellationByStore =>
            (DashboardAnalyticsWidget.OrderStatusSummary, []),
        DashboardAnswerFocus.PaymentUsage =>
            (DashboardAnalyticsWidget.PaymentMethodMix, []),
        DashboardAnswerFocus.TopSellingProducts =>
            (DashboardAnalyticsWidget.TopProducts, []),
        DashboardAnswerFocus.TopSellingCategories =>
            (DashboardAnalyticsWidget.CategoryPerformance, []),
        DashboardAnswerFocus.LowVolumeProducts =>
            (DashboardAnalyticsWidget.LowVolumeProducts, []),
        DashboardAnswerFocus.LowMarginProducts =>
            (DashboardAnalyticsWidget.LowMarginProducts, []),
        DashboardAnswerFocus.InventoryShortage =>
            (DashboardAnalyticsWidget.InventoryShortageRisk, []),
        DashboardAnswerFocus.ReorderPriority =>
            (DashboardAnalyticsWidget.InventoryReorderSuggestions, []),
        DashboardAnswerFocus.IngredientConsumptionTrend =>
            (DashboardAnalyticsWidget.IngredientConsumptionTrend, []),
        DashboardAnswerFocus.SupplierAndOverdueRisk =>
            (DashboardAnalyticsWidget.SupplierQuality,
                [DashboardAnalyticsWidget.OverduePurchaseOrders, DashboardAnalyticsWidget.SupplierIssueMix]),
        DashboardAnswerFocus.OperationalAnomaly =>
            (DashboardAnalyticsWidget.OperationalAlerts, []),
        _ => DynamicWidgets(normalized)
    };

    private static (DashboardAnalyticsWidget Primary, IReadOnlyList<DashboardAnalyticsWidget> Supporting)
        DynamicWidgets(string normalized)
    {
        if (normalized.Contains("huy", StringComparison.Ordinal))
            return (DashboardAnalyticsWidget.OrderStatusSummary, []);
        if (normalized.Contains("thanh toan", StringComparison.Ordinal))
            return (DashboardAnalyticsWidget.PaymentMethodMix, []);
        if (normalized.Contains("san pham", StringComparison.Ordinal))
            return (DashboardAnalyticsWidget.ProductPeriodPerformance, []);
        if (normalized.Contains("kho", StringComparison.Ordinal)
            || normalized.Contains("nguyen lieu", StringComparison.Ordinal))
            return (DashboardAnalyticsWidget.InventoryShortageRisk, []);
        if (normalized.Contains("nha cung cap", StringComparison.Ordinal))
            return (DashboardAnalyticsWidget.SupplierQuality, []);
        return (DashboardAnalyticsWidget.NetSalesTrend, []);
    }

    private static FocusProfile DynamicProfile(string normalized)
    {
        var widgets = DynamicWidgets(normalized);
        var definition = DashboardWidgetCatalog.Get(widgets.Primary);
        var tab = definition.Section switch
        {
            DashboardSection.Product or DashboardSection.Operations =>
                DashboardTabCodes.OrdersProducts,
            DashboardSection.Inventory => DashboardTabCodes.InventoryReorder,
            DashboardSection.Procurement => DashboardTabCodes.SupplierAnomaly,
            _ => DashboardTabCodes.OverviewRevenue
        };
        var style = tab switch
        {
            DashboardTabCodes.OrdersProducts => DashboardAnswerStyleIds.TransactionRankingAnalysis,
            DashboardTabCodes.InventoryReorder => DashboardAnswerStyleIds.OperationalActionAnalysis,
            DashboardTabCodes.SupplierAnomaly => DashboardAnswerStyleIds.RiskInvestigationAnalysis,
            _ => DashboardAnswerStyleIds.ExecutiveDiagnostic
        };
        return P(DashboardBusinessIntent.GeneralBusinessSummary, tab, style,
            definition.Metric!.DimensionField, definition.Metric.Name, [],
            [definition.Metric.DimensionField]);
    }

    private static FocusProfile P(
        DashboardBusinessIntent intent,
        string tab,
        string style,
        string entity,
        string metric,
        IReadOnlyList<string> secondary,
        IReadOnlyList<string> dimensions,
        bool ranking = false,
        bool trend = false,
        bool comparison = false,
        bool composition = false,
        bool anomaly = false,
        bool recommendation = false,
        string sort = "DESC",
        IReadOnlyList<string>? exclusions = null) =>
        new(intent, tab, style, entity, metric, secondary, dimensions, ranking, trend,
            comparison, composition, anomaly, recommendation, sort, exclusions ?? []);

    private static IEnumerable<string> RequiredFields(DashboardAnswerFocus focus) => focus switch
    {
        DashboardAnswerFocus.TopSellingProducts =>
            ["DrinkId", "DrinkName", "TotalSold", "ProductRevenue", "QuantityShare", "RevenueShare"],
        DashboardAnswerFocus.PaymentUsage =>
            ["PaymentMethodId", "PaymentMethodName", "TotalTransactions", "TransactionShare"],
        DashboardAnswerFocus.ReorderPriority =>
            ["IngredientId", "IngredientName", "AvailableQuantity", "MinimumStock", "FinalSuggestedQuantity",
             "SuggestionLeadTimeDaysSnapshot", "Priority", "SuggestionReason", "DataStatus"],
        DashboardAnswerFocus.LowMarginProducts =>
            ["DrinkId", "DrinkName", "TotalSold", "Revenue", "ConfirmedCogs", "ConfirmedMarginRate", "DataStatus"],
        _ => ["EntityId", "EntityName", "MetricValue", "DataStatus"]
    };

    private static IEnumerable<string> QualityRules(DashboardAnswerFocus focus)
    {
        yield return "Không tạo entity hoặc số liệu ngoài dataset.";
        yield return "Không kết luận trend khi có ít hơn hai điểm thời gian.";
        if (focus == DashboardAnswerFocus.LowMarginProducts)
            yield return "Chỉ kết luận margin khi COGS và DataStatus đều Complete.";
        if (focus == DashboardAnswerFocus.ReorderPriority)
            yield return "Không đề xuất mua khi supplier, package, giá, conversion hoặc lead time không hợp lệ.";
    }

    private static string Goal(DashboardAnswerFocus focus) => focus switch
    {
        DashboardAnswerFocus.TopSellingProducts => "Xếp hạng sản phẩm theo số lượng bán.",
        DashboardAnswerFocus.PaymentUsage => "Xếp hạng phương thức thanh toán theo số giao dịch.",
        DashboardAnswerFocus.ReorderPriority => "Xác định nguyên liệu cần đặt lại và số lượng deterministic.",
        DashboardAnswerFocus.LowMarginProducts => "Xác định sản phẩm biên lợi nhuận thấp trong tập COGS đầy đủ.",
        _ => $"Phân tích trọng tâm {focus}."
    };

    private static string FallbackFamily(DashboardAnswerFocus focus) => focus switch
    {
        DashboardAnswerFocus.OperationalPriorities => "ExecutiveDiagnosticFallback",
        DashboardAnswerFocus.RevenueComparison or DashboardAnswerFocus.StoreUnderperformance => "ComparisonFallback",
        DashboardAnswerFocus.DailyRevenueStatistics or DashboardAnswerFocus.IngredientConsumptionTrend => "TrendFallback",
        DashboardAnswerFocus.TopSellingProducts or DashboardAnswerFocus.TopSellingCategories
            or DashboardAnswerFocus.LowVolumeProducts or DashboardAnswerFocus.LowMarginProducts
            or DashboardAnswerFocus.PaymentUsage or DashboardAnswerFocus.OrderCancellationByStore => "RankingFallback",
        DashboardAnswerFocus.InventoryShortage => "InventoryRiskFallback",
        DashboardAnswerFocus.ReorderPriority => "ReorderFallback",
        DashboardAnswerFocus.SupplierAndOverdueRisk => "SupplierRiskFallback",
        DashboardAnswerFocus.OperationalAnomaly => "AnomalyFallback",
        _ => "NoDataFallback"
    };

    private static string BuildDynamicFocus(string normalized)
    {
        var goal = normalized.Contains("so sanh", StringComparison.Ordinal) ? "Comparison"
            : normalized.Contains("xu huong", StringComparison.Ordinal) ? "Trend"
            : normalized.Contains("top", StringComparison.Ordinal) ? "Ranking"
            : "Analysis";
        return $"{goal}:{normalized}";
    }

    public static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(char.ToLowerInvariant(character));
        }
        return string.Join(
            " ",
            builder.ToString().Normalize(NormalizationForm.FormC)
                .Replace('đ', 'd')
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record FocusProfile(
        DashboardBusinessIntent Intent,
        string TabCode,
        string AnswerStyleId,
        string Entity,
        string Metric,
        IReadOnlyList<string> SecondaryMetrics,
        IReadOnlyList<string> Dimensions,
        bool RequiresRanking,
        bool RequiresTrend,
        bool RequiresComparison,
        bool RequiresComposition,
        bool RequiresAnomaly,
        bool RequiresRecommendation,
        string SortDirection,
        IReadOnlyList<string> Exclusions);
}
