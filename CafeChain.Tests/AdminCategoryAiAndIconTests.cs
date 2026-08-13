using CafeChain.Application.DTOs.Admin.Categories;
using CafeChain.Application.DTOs.AI;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Services.Admin.Categories;
using CafeChain.Application.Services.AI;
using CafeChain.Application.Validation;
using CafeChain.Infrastructure.Configurations;
using CafeChain.Infrastrusture.Interfaces.Admin.Categories;
using CafeChain.Infrastrusture.Interfaces.Admin.Drinks;
using CafeChain.Infrastrusture.Interfaces.Admin.Sizes;
using CafeChain.Infrastrusture.Interfaces.Admin.Toppings;
using CafeChain.Models.Drinks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;

namespace CafeChain.Tests;

public sealed class AdminCategoryAiAndIconTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("☕")]
    [InlineData("❤️")]
    [InlineData("👩‍🍳")]
    [InlineData("♨️")]
    public void IconPolicy_AcceptsOptionalSingleUnicodeSymbol(string? value)
    {
        var valid = CategoryIconPolicy.TryNormalize(value, out _, out var error);

        Assert.True(valid, error);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("1")]
    [InlineData("☕🍵")]
    [InlineData("<script>")]
    [InlineData("✨ text")]
    [InlineData("☕ ☕")]
    public void IconPolicy_RejectsTextHtmlAndMultipleSymbols(string value)
    {
        Assert.False(CategoryIconPolicy.TryNormalize(value, out _, out _));
    }

    [Fact]
    public void IconPolicy_PrioritizesSingleSymbolRuleBeforeStorageLength()
    {
        var valid = CategoryIconPolicy.TryNormalize("12345678901", out _, out var error);

        Assert.False(valid);
        Assert.Equal("Chỉ được chọn một biểu tượng Unicode.", error);
    }

    [Fact]
    public async Task CategoryService_RejectsInvalidManualIconBeforeSaving()
    {
        var repository = new Mock<IAdminCategoryRepository>();
        repository.Setup(x => x.GetAllCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var service = new AdminCategoryService(repository.Object);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateCategoryAsync(
            new AdminCreateCategoryDto
            {
                Name = "Danh mục thử",
                CategoryCode = "DM_THU",
                Icon = "abc"
            }));

        Assert.Contains("biểu tượng Unicode", exception.Message, StringComparison.OrdinalIgnoreCase);
        repository.Verify(x => x.CreateCategoryAsync(It.IsAny<DrinkCategory>()), Times.Never);
        repository.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task SuggestCategories_BlankFormCalledTwice_ReturnsOneRemainingFallbackBothTimes()
    {
        var existingNames = new[]
        {
            "Nước ép", "Đá xay", "Trà trái cây", "Đồ uống theo mùa", "Trà thảo mộc",
            "Sữa chua", "Soda", "Mocktail", "Đồ uống ít đường", "Đồ uống nóng",
            "Đồ uống đóng chai", "Kem và tráng miệng", "Bánh ngọt", "Combo nổi bật"
        };
        var existing = existingNames.Select((name, index) => new DrinkCategory
        {
            CategoryId = index + 1,
            CategoryCode = $"EXISTING_{index + 1}",
            Name = name
        }).ToList();
        var categoryRepository = new Mock<IAdminCategoryRepository>();
        categoryRepository.Setup(x => x.GetAllCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var service = CreateAiService(categoryRepository, new Mock<IOllamaClient>(), enabled: false);

        var first = await service.SuggestCategoriesAsync(new CategorySuggestionRequestDTO());
        var second = await service.SuggestCategoriesAsync(new CategorySuggestionRequestDTO());

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Single(first.Options);
        Assert.Single(second.Options);
        Assert.Equal("Sinh tố", first.Options[0].Name);
        Assert.Equal(first.Options[0].Name, second.Options[0].Name);
        Assert.Null(first.ErrorCode);
        Assert.DoesNotContain("thiếu dữ liệu", first.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SuggestCategories_InvalidJsonThenValidResponse_RetriesOnceAndUsesCurrentForm()
    {
        var categoryRepository = new Mock<IAdminCategoryRepository>();
        categoryRepository.Setup(x => x.GetAllCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var ollama = new Mock<IOllamaClient>();
        var payloads = new List<string>();
        var call = 0;
        ollama.Setup(x => x.ChatAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, payload, _) => payloads.Add(payload))
            .ReturnsAsync(() => call++ == 0
                ? new OllamaResultDTO { Success = true, Content = "not-json" }
                : new OllamaResultDTO
                {
                    Success = true,
                    Content = "{\"suggestions\":[{\"name\":\"Trà hoa quả nhiệt đới\",\"icon\":\"🍹\"}]}"
                });
        var service = CreateAiService(categoryRepository, ollama, enabled: true);

        var result = await service.SuggestCategoriesAsync(new CategorySuggestionRequestDTO
        {
            CurrentName = "Trái cây mùa hè",
            CurrentCategoryCode = "TRAI_CAY",
            CurrentIcon = "🍊"
        });

        Assert.True(result.Success);
        Assert.True(result.UsedOllama);
        Assert.Contains(result.Options, x => x.Name == "Trà hoa quả nhiệt đới");
        Assert.Equal(2, payloads.Count);
        Assert.All(payloads, payload =>
        {
            using var document = JsonDocument.Parse(payload);
            var currentForm = document.RootElement.GetProperty("CurrentForm");
            Assert.Equal("Trái cây mùa hè", currentForm.GetProperty("Name").GetString());
            Assert.Equal("TRAI_CAY", currentForm.GetProperty("CategoryCode").GetString());
            Assert.Equal("🍊", currentForm.GetProperty("Icon").GetString());
        });
    }

    private static AIService CreateAiService(
        Mock<IAdminCategoryRepository> categoryRepository,
        Mock<IOllamaClient> ollama,
        bool enabled)
    {
        return new AIService(
            categoryRepository.Object,
            Mock.Of<IAdminDrinkRepository>(),
            Mock.Of<IAdminSizeRepository>(),
            Mock.Of<IAdminToppingRepository>(),
            ollama.Object,
            Mock.Of<IVisualSpecificationBuilder>(),
            Mock.Of<IAISkillCatalog>(),
            Mock.Of<IAISuggestionHistoryStore>(),
            Options.Create(new AIOptions
            {
                Enabled = enabled,
                Provider = "Ollama",
                StructuredResponseRetries = 1
            }),
            NullLogger<AIService>.Instance);
    }
}
