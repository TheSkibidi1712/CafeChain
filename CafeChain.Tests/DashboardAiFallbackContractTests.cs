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
