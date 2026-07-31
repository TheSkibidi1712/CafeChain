namespace CafeChain.Application.DTOs.Admin.Dashboard;

public enum DashboardFocusType
{
    Canonical,
    Dynamic
}

public enum DashboardAnswerFocus
{
    OperationalPriorities,
    RevenueComparison,
    StoreUnderperformance,
    RevenueDriver,
    DailyRevenueStatistics,
    OrderCancellationByStore,
    PaymentUsage,
    TopSellingProducts,
    TopSellingCategories,
    LowVolumeProducts,
    LowMarginProducts,
    InventoryShortage,
    ReorderPriority,
    IngredientConsumptionTrend,
    SupplierAndOverdueRisk,
    OperationalAnomaly,
    Dynamic
}

public static class DashboardTabCodes
{
    public const string OverviewRevenue = "OVERVIEW_REVENUE";
    public const string OrdersProducts = "ORDERS_PRODUCTS";
    public const string InventoryReorder = "INVENTORY_REORDER";
    public const string SupplierAnomaly = "SUPPLIER_ANOMALY";
}

public static class DashboardAnswerStyleIds
{
    public const string ExecutiveDiagnostic = "EXECUTIVE_DIAGNOSTIC";
    public const string TransactionRankingAnalysis = "TRANSACTION_RANKING_ANALYSIS";
    public const string OperationalActionAnalysis = "OPERATIONAL_ACTION_ANALYSIS";
    public const string RiskInvestigationAnalysis = "RISK_INVESTIGATION_ANALYSIS";
}

public sealed class DashboardGuidePageDto
{
    public List<DashboardGuideQuestionGroupDto> QuestionGroups { get; set; } = [];
}

public sealed class DashboardGuideQuestionGroupDto
{
    public string Name { get; set; } = string.Empty;
    public List<DashboardGuideQuestionDto> Questions { get; set; } = [];
}

public sealed class DashboardGuideQuestionDto
{
    public string Question { get; set; } = string.Empty;
    public DashboardAnswerFocus ExpectedAnswerFocus { get; set; }
    public DashboardAnalyticsWidget PrimaryWidget { get; set; }
    public string AnswerStyleId { get; set; } = string.Empty;
}

public sealed class DashboardQuestionUnderstandingDto
{
    public string OriginalQuestion { get; set; } = string.Empty;
    public string NormalizedQuestion { get; set; } = string.Empty;
    public DashboardBusinessIntent BusinessIntent { get; set; }
    public DashboardAnswerFocus AnswerFocus { get; set; }
    public DashboardFocusType FocusType { get; set; } = DashboardFocusType.Canonical;
    public string? DynamicFocus { get; set; }
    public decimal FocusConfidence { get; set; } = 1m;
    public string TabCode { get; set; } = DashboardTabCodes.OverviewRevenue;
    public string AnswerStyleId { get; set; } = DashboardAnswerStyleIds.ExecutiveDiagnostic;
    public string PrimaryEntity { get; set; } = string.Empty;
    public string PrimaryMetric { get; set; } = string.Empty;
    public List<string> SecondaryMetrics { get; set; } = [];
    public List<string> Dimensions { get; set; } = [];
    public List<string> GroupBy { get; set; } = [];
    public string RankingDirection { get; set; } = "DESC";
    public int RequestedLimit { get; set; } = 10;
    public DashboardPeriodDto TimeRange { get; set; } = new();
    public DashboardComparison ComparisonPeriod { get; set; }
    public string TimeGrain { get; set; } = "Day";
    public List<int> RequestedStoreIds { get; set; } = [];
    public List<int> EffectiveStoreIds { get; set; } = [];
    public List<string> RequestedOutput { get; set; } = [];
    public List<string> ExplicitExclusions { get; set; } = [];
    public bool RequiresRanking { get; set; }
    public bool RequiresTrend { get; set; }
    public bool RequiresComparison { get; set; }
    public bool RequiresComposition { get; set; }
    public bool RequiresAnomalyDetection { get; set; }
    public bool RequiresRecommendation { get; set; }
    public bool IsDashboardQuestion { get; set; } = true;
    public bool IsAmbiguous { get; set; }
}

public sealed class DashboardDataPlanDto
{
    public string PlanId { get; set; } = string.Empty;
    public string AnalysisGoal { get; set; } = string.Empty;
    public List<string> RequiredDataSources { get; set; } = [];
    public List<string> RequiredFields { get; set; } = [];
    public List<string> RequiredMetrics { get; set; } = [];
    public Dictionary<string, string> Filters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<int> EffectiveStoreIds { get; set; } = [];
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<string> GroupBy { get; set; } = [];
    public string SortBy { get; set; } = string.Empty;
    public string SortDirection { get; set; } = "DESC";
    public int Limit { get; set; } = 10;
    public DashboardComparison ComparisonDefinition { get; set; }
    public string TimeGrain { get; set; } = "Day";
    public DashboardAnalyticsWidget PrimaryWidget { get; set; }
    public List<DashboardAnalyticsWidget> SupportingWidgets { get; set; } = [];
    public List<string> DataQualityRules { get; set; } = [];
    public string FallbackPattern { get; set; } = string.Empty;
}

public sealed class DashboardEvidencePackDto
{
    public string OriginalQuestion { get; set; } = string.Empty;
    public string AnalysisGoal { get; set; } = string.Empty;
    public Dictionary<string, string> AppliedFilters { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public List<DashboardEvidenceDto> PrimaryFacts { get; set; } = [];
    public List<DashboardEvidenceDto> SupportingFacts { get; set; } = [];
    public List<DashboardEvidenceDto> ChartEvidence { get; set; } = [];
    public List<DashboardEvidenceDto> TableEvidence { get; set; } = [];
    public string DataStatus { get; set; } = "NO_DATA";
    public List<string> MissingFields { get; set; } = [];
    public List<string> Limitations { get; set; } = [];
}

public sealed class DashboardChartPlanDto
{
    public string ChartId { get; set; } = string.Empty;
    public DashboardChartType ChartType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string XAxis { get; set; } = string.Empty;
    public string YAxis { get; set; } = string.Empty;
    public List<string> Series { get; set; } = [];
    public string Sort { get; set; } = string.Empty;
    public int Limit { get; set; }
    public Dictionary<string, string> AppliedFilters { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public List<string> EvidenceIds { get; set; } = [];
    public string DataStatus { get; set; } = "NO_DATA";
}

public sealed class DashboardAnswerStyleProfileDto
{
    public string TabCode { get; set; } = string.Empty;
    public string AnswerStyleId { get; set; } = string.Empty;
    public string OpeningPattern { get; set; } = string.Empty;
    public string EvidencePattern { get; set; } = string.Empty;
    public string ChartInterpretationPattern { get; set; } = string.Empty;
    public string LimitationPattern { get; set; } = string.Empty;
    public string ClosingPattern { get; set; } = string.Empty;
}
