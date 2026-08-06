using CafeChain.Application.DTOs.AI;
using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Services.AI;
using CafeChain.Infrastructure.Configurations;
using CafeChain.Infrastrusture.Interfaces.Admin.Categories;
using CafeChain.Infrastrusture.Interfaces.Admin.Drinks;
using CafeChain.Infrastrusture.Interfaces.Admin.Sizes;
using CafeChain.Infrastrusture.Interfaces.Admin.Toppings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CafeChain.Tests;

public sealed class DashboardAiFallbackContractTests
{
    [Fact]
    public async Task NoData_does_not_call_ollama()
    {
        var ollama = new Mock<IOllamaClient>(MockBehavior.Strict);
        var result = await CreateService(ollama).ExplainDashboardInsightAsync(new DashboardInsightExplanationContextDto
        {
            AnalysisId = Guid.NewGuid(),
            Widget = DashboardAnalyticsWidget.NetSalesTrend,
            DataStatus = "NO_DATA",
            Comparison = new DashboardComparisonResultDto()
        });

        Assert.True(result.UsedFallback);
        ollama.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-json")]
    [InlineData("{\"analysisId\":\"00000000-0000-0000-0000-000000000000\"}")]
    public async Task Invalid_or_empty_response_returns_fallback(string content)
    {
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(x => x.ChatAsync(
                It.IsAny<string>(), It.IsAny<string>(), "dashboard-insight-explanation", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResultDTO { Success = true, Content = content });

        var result = await CreateService(ollama).ExplainDashboardInsightAsync(Context());

        Assert.True(result.UsedFallback);
        Assert.False(result.UsedOllama);
    }

    [Fact]
    public async Task Fabricated_numeric_claim_returns_fallback()
    {
        var context = Context();
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(x => x.ChatAsync(
                It.IsAny<string>(), It.IsAny<string>(), "dashboard-insight-explanation", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResultDTO
            {
                Success = true,
                Content = $$"""{"analysisId":"{{context.AnalysisId}}","widget":"NetSalesTrend","directAnswer":"Doanh thu giảm 99%. Mức giảm này cần được kiểm tra.","proofPoints":[{"text":"Doanh thu giảm 99%.","evidenceIds":["E-1"]}],"actionToCheck":null,"usedEvidenceIds":["E-1"],"limitations":[]}"""
            });

        var result = await CreateService(ollama).ExplainDashboardInsightAsync(context);

        Assert.True(result.UsedFallback);
        Assert.Contains("số không tồn tại", string.Join(" ", result.Warnings), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Operational_priority_fallback_uses_business_order_and_readable_statements()
    {
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(x => x.ChatAsync(
                It.IsAny<string>(), It.IsAny<string>(), "dashboard-insight-explanation", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResultDTO { Success = false });
        var context = OperationalContext(
            Alert("E-CASH", "CASH_DISCREPANCY", "WorkShift #65", "CafeChain Dĩ An", -80000, "VND", "Critical", "CRITICAL",
                "Ca #65 tại CafeChain Dĩ An đang thiếu 80.000 đ."),
            Alert("E-STOCK", "LOW_STOCK", "Hạt chia", "CafeChain Thủ Dầu Một", 2, "g", "High", "WARNING",
                "Tồn khả dụng của Hạt chia tại CafeChain Thủ Dầu Một đã xuống dưới ngưỡng, hiện ở mức 2 g."),
            Alert("E-PO", "OVERDUE_PO", "PO-OVERDUE", "CafeChain Thủ Dầu Một", 999999, "DAY", "High", "WARNING",
                "PO PO-OVERDUE tại CafeChain Thủ Dầu Một đã quá hạn 999.999 ngày."));

        var result = await CreateService(ollama).ExplainDashboardInsightAsync(context);

        Assert.True(result.UsedFallback);
        Assert.StartsWith("Có 1 cảnh báo nghiêm trọng và 2 cảnh báo khác", result.DirectAnswer, StringComparison.Ordinal);
        Assert.True(result.DirectAnswer.IndexOf("Hạt chia", StringComparison.Ordinal)
                    < result.DirectAnswer.IndexOf("PO-OVERDUE", StringComparison.Ordinal));
        Assert.Equal(new[] { "E-CASH", "E-STOCK", "E-PO" },
            result.ProofPoints.SelectMany(item => item.EvidenceIds));
        Assert.Contains("Đối chiếu tiền mặt", result.ActionToCheck!.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("INGREDIENT", result.DirectAnswer, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("CASH_DISCREPANCY", "Đối chiếu tiền mặt")]
    [InlineData("LOW_STOCK", "Kiểm đếm tồn thực tế")]
    [InlineData("OVERDUE_PO", "trạng thái giao nhận")]
    [InlineData("SUPPLIER_ISSUE", "phiếu nhập liên quan")]
    public async Task Operational_priority_action_matches_alert_type(string alertType, string expectedAction)
    {
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(x => x.ChatAsync(
                It.IsAny<string>(), It.IsAny<string>(), "dashboard-insight-explanation", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResultDTO { Success = false });
        var context = OperationalContext(Alert(
            "E-1", alertType, "Đối tượng kiểm tra", "CafeChain Dĩ An", 2, "g", "High", "WARNING",
            "Cảnh báo nghiệp vụ cần kiểm tra."));

        var result = await CreateService(ollama).ExplainDashboardInsightAsync(context);

        Assert.Contains(expectedAction, result.ActionToCheck!.Text, StringComparison.OrdinalIgnoreCase);
    }

    private static DashboardInsightExplanationContextDto OperationalContext(
        params DashboardEvidenceDto[] evidence) => new()
    {
        AnalysisId = Guid.NewGuid(),
        Widget = DashboardAnalyticsWidget.OperationalAlerts,
        BusinessIntent = DashboardBusinessIntent.GeneralBusinessSummary,
        FromDate = new DateTime(2026, 8, 1),
        ToDate = new DateTime(2026, 8, 3),
        DataStatus = "OK",
        Confidence = .9m,
        Comparison = new DashboardComparisonResultDto { CurrentValue = evidence.Length },
        Understanding = new DashboardQuestionUnderstandingDto
        {
            OriginalQuestion = "Tôi nên chú ý điều gì?",
            AnswerFocus = DashboardAnswerFocus.OperationalPriorities
        },
        Evidence = evidence
    };

    private static DashboardEvidenceDto Alert(
        string id,
        string alertType,
        string entityName,
        string storeName,
        decimal value,
        string unit,
        string priority,
        string risk,
        string statement) => new()
    {
        EvidenceId = id,
        SourceWidget = DashboardAnalyticsWidget.OperationalAlerts,
        WidgetKey = "OperationalAlerts",
        Kind = "FACT",
        CurrentValue = value,
        DisplayValue = Math.Abs(value).ToString("0.##"),
        Unit = unit,
        DisplayUnit = unit,
        EntityName = entityName,
        StoreName = storeName,
        Priority = priority,
        RiskLevel = risk,
        Statement = statement,
        DataStatus = "OK",
        Metadata = new Dictionary<string, object?> { ["alertType"] = alertType }
    };

    private static DashboardInsightExplanationContextDto Context()
    {
        var id = Guid.NewGuid();
        return new DashboardInsightExplanationContextDto
        {
            AnalysisId = id,
            Widget = DashboardAnalyticsWidget.NetSalesTrend,
            BusinessIntent = DashboardBusinessIntent.SalesTrend,
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2026, 1, 7),
            DataStatus = "OK",
            Confidence = .85m,
            Comparison = new DashboardComparisonResultDto { CurrentValue = 100, BaselineValue = 110, PercentageDifference = -9.09m },
            Evidence =
            [
                new DashboardEvidenceDto
                {
                    EvidenceId = "E-1", SourceWidget = DashboardAnalyticsWidget.NetSalesTrend,
                    WidgetKey = "NetSalesTrend", Kind = "FACT", CurrentValue = 100,
                    BaselineValue = 110, DeviationPercent = -9.09m, SampleSize = 20, Unit = "VND",
                    DataStatus = "OK"
                }
            ],
            ChartAnalyses =
            [
                new DashboardChartAnalysisDto
                {
                    Widget = DashboardAnalyticsWidget.NetSalesTrend,
                    Title = "Revenue",
                    DataStatus = "OK",
                    Summary = "100",
                    Evidence =
                    [
                        new DashboardEvidenceDto { EvidenceId = "E-1" }
                    ]
                }
            ]
        };
    }

    private static AIService CreateService(Mock<IOllamaClient> ollama)
    {
        var skills = new Mock<IAISkillCatalog>();
        skills.Setup(x => x.GetNamedSkillAsync("dashboard-insight-explanation", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AINamedSkillContext(
                "dashboard-insight-explanation", "Skill", "{\"type\":\"object\"}", [], []));
        return new AIService(
            Mock.Of<IAdminCategoryRepository>(),
            Mock.Of<IAdminDrinkRepository>(),
            Mock.Of<IAdminSizeRepository>(),
            Mock.Of<IAdminToppingRepository>(),
            ollama.Object,
            Mock.Of<IVisualSpecificationBuilder>(),
            skills.Object,
            Mock.Of<IAISuggestionHistoryStore>(),
            Options.Create(new AIOptions { Enabled = true, Provider = "Ollama" }),
            NullLogger<AIService>.Instance);
    }
}
