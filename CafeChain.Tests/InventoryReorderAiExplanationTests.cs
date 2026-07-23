using CafeChain.Application.DTOs.AI;
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

public sealed class InventoryReorderAiExplanationTests
{
    [Fact]
    public async Task Disabled_ai_returns_deterministic_fallback_without_calling_ollama()
    {
        var ollama = new Mock<IOllamaClient>(MockBehavior.Strict);
        var service = CreateService(ollama, enabled: false);

        var result = await service.ExplainInventoryReorderAsync(Context());

        Assert.True(result.Success);
        Assert.True(result.UsedFallback);
        Assert.False(result.UsedOllama);
        Assert.Equal("Rule reason", result.Explanation);
    }

    [Fact]
    public async Task Ollama_timeout_returns_deterministic_fallback()
    {
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(x => x.ChatAsync(
                It.IsAny<string>(), It.IsAny<string>(), "inventory-reorder-explanation", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await CreateService(ollama).ExplainInventoryReorderAsync(Context());

        Assert.True(result.UsedFallback);
        Assert.Equal("Rule reason", result.Explanation);
    }

    [Fact]
    public async Task Unknown_field_or_echo_mismatch_is_rejected_to_fallback()
    {
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(x => x.ChatAsync(
                It.IsAny<string>(), It.IsAny<string>(), "inventory-reorder-explanation", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResultDTO
            {
                Success = true,
                Content = "{\"ingredientId\":99,\"recommendationLevel\":\"URGENT\",\"usableStock\":3,\"minimumStock\":10,\"pendingIncoming\":0,\"suggestedQuantity\":7,\"explanation\":\"Wrong\",\"unknown\":true}"
            });

        var result = await CreateService(ollama).ExplainInventoryReorderAsync(Context());

        Assert.True(result.UsedFallback);
        Assert.Equal("Rule reason", result.Explanation);
    }

    [Fact]
    public async Task Valid_structured_echo_uses_ollama_explanation()
    {
        var ollama = new Mock<IOllamaClient>();
        ollama.Setup(x => x.ChatAsync(
                It.IsAny<string>(), It.IsAny<string>(), "inventory-reorder-explanation", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OllamaResultDTO
            {
                Success = true,
                Content = "{\"ingredientId\":15,\"recommendationLevel\":\"URGENT\",\"usableStock\":3,\"minimumStock\":10,\"pendingIncoming\":0,\"suggestedQuantity\":7,\"explanation\":\"Cần ưu tiên nhập vì tồn khả dụng thấp hơn ngưỡng.\"}"
            });

        var result = await CreateService(ollama).ExplainInventoryReorderAsync(Context());

        Assert.True(result.UsedOllama);
        Assert.False(result.UsedFallback);
        Assert.Contains("ưu tiên", result.Explanation);
    }

    private static InventoryReorderExplanationContextDto Context() => new()
    {
        IngredientId = 15,
        IngredientName = "Sữa tươi",
        RecommendationLevel = "URGENT",
        UsableStock = 3,
        MinimumStock = 10,
        PendingIncoming = 0,
        SuggestedQuantity = 7,
        Unit = "L",
        DeterministicReason = "Rule reason"
    };

    private static AIService CreateService(Mock<IOllamaClient> ollama, bool enabled = true)
    {
        var skills = new Mock<IAISkillCatalog>();
        skills.Setup(x => x.GetNamedSkillAsync("inventory-reorder-explanation", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AINamedSkillContext(
                "inventory-reorder-explanation", "Skill", "{\"type\":\"object\"}", [], []));
        return new AIService(
            Mock.Of<IAdminCategoryRepository>(),
            Mock.Of<IAdminDrinkRepository>(),
            Mock.Of<IAdminSizeRepository>(),
            Mock.Of<IAdminToppingRepository>(),
            ollama.Object,
            Mock.Of<IVisualSpecificationBuilder>(),
            skills.Object,
            Mock.Of<IAISuggestionHistoryStore>(),
            Options.Create(new AIOptions { Enabled = enabled, Provider = "Ollama" }),
            NullLogger<AIService>.Instance);
    }
}
