using CafeChain.Application.Services.AI;
using CafeChain.Infrastructure.Configurations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CafeChain.Tests;

public sealed class AISkillCatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cafechain-ai-skill-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Empty_skill_tree_returns_fallback_warning_without_throwing()
    {
        CreateFile("Resources/AI/skills/skill-router/SKILL.md", string.Empty);
        CreateFile("Resources/AI/schemas/ai-suggestion.schema.json", string.Empty);
        var catalog = CreateCatalog();

        var result = await catalog.GetContextAsync("Drink", true);

        Assert.Empty(result.Content);
        Assert.Null(result.SuggestionSchema);
        Assert.Contains(result.Warnings, x => x.Contains("fallback", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Catalog_routes_entity_references_and_reloads_changed_files()
    {
        CreateFile("Resources/AI/skills/cafe-business-rules/references/drink-rules.md", "drink-v1");
        CreateFile("Resources/AI/schemas/ai-suggestion.schema.json", "{\"type\":\"object\"}");
        var catalog = CreateCatalog();
        var first = await catalog.GetContextAsync("Drink", false);

        CreateFile("Resources/AI/skills/cafe-business-rules/references/drink-rules.md", "drink-v2");
        File.SetLastWriteTimeUtc(Path.Combine(_root, "Resources/AI/skills/cafe-business-rules/references/drink-rules.md"),
            DateTime.UtcNow.AddSeconds(2));
        var second = await catalog.GetContextAsync("Drink", false);

        Assert.Contains("drink-v1", first.Content);
        Assert.Contains("drink-v2", second.Content);
        Assert.DoesNotContain("topping-rules", second.LoadedFiles);
        Assert.NotNull(second.SuggestionSchema);
    }

    [Fact]
    public async Task Catalog_strips_frontmatter_and_excludes_delivery_modules_from_suggestion_context()
    {
        CreateFile("Resources/AI/skills/cafe-business-rules/references/topping-rules.md", "ENTITY RULE");
        CreateFile("Resources/AI/skills/skill-router/SKILL.md",
            "---\nname: skill-router\ndescription: Test router.\n---\nROUTER BODY");
        CreateFile("Resources/AI/skills/pexels-image-retrieval/SKILL.md",
            "---\nname: pexels-image-retrieval\ndescription: Delivery only.\n---\nPEXELS BODY");
        CreateFile("Resources/AI/skills/comfyui-generation/SKILL.md",
            "---\nname: comfyui-generation\ndescription: Delivery only.\n---\nCOMFY BODY");
        CreateFile("Resources/AI/schemas/ai-suggestion.schema.json", "{\"type\":\"object\"}");
        CreateFile("Resources/AI/schemas/image-concept.schema.json", "{\"type\":\"object\"}");

        var result = await CreateCatalog().GetContextAsync("Topping", true);

        Assert.Contains("ENTITY RULE", result.Content);
        Assert.Contains("ROUTER BODY", result.Content);
        Assert.DoesNotContain("description: Test router", result.Content);
        Assert.DoesNotContain("PEXELS BODY", result.Content);
        Assert.DoesNotContain("COMFY BODY", result.Content);
        Assert.DoesNotContain(result.LoadedFiles, path => path.Contains("pexels", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Catalog_preserves_entity_rules_when_context_budget_is_reached()
    {
        CreateFile("Resources/AI/skills/cafe-business-rules/references/drink-rules.md", "ENTITY:" + new string('e', 1400));
        CreateFile("Resources/AI/skills/skill-router/SKILL.md",
            "---\nname: skill-router\ndescription: Test router.\n---\nGENERAL:" + new string('g', 1400));
        CreateFile("Resources/AI/schemas/ai-suggestion.schema.json", "{\"type\":\"object\"}");
        CreateFile("Resources/AI/schemas/image-concept.schema.json", "not-json");

        var result = await CreateCatalog(maxCharacters: 2000).GetContextAsync("Drink", false);

        Assert.Contains("ENTITY:", result.Content);
        Assert.DoesNotContain("GENERAL:", result.Content);
        Assert.True(result.Content.Length <= 2000);
        Assert.Contains(result.Warnings, warning => warning.Contains("image-concept.schema.json"));
        Assert.Contains(result.Warnings, warning => warning.Contains("2000"));
    }

    [Fact]
    public void Similarity_policy_detects_accent_and_near_name_duplicates()
    {
        var score = AISuggestionUniquenessPolicy.NameSimilarity("Trà đào cam sả", "Tra dao cam sa");
        var duplicate = AISuggestionUniquenessPolicy.IsNearDuplicate(
            "Trà đào cam xả", "trà trái cây đào cam",
            [("Trà đào cam sả", "trà trái cây đào cam và sả")], 0.80, 0.75, out var signals);

        Assert.Equal(1d, score, 3);
        Assert.True(duplicate);
        Assert.NotEmpty(signals);
    }

    private AISkillCatalog CreateCatalog(int maxCharacters = 12000)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(x => x.ContentRootPath).Returns(_root);
        return new AISkillCatalog(environment.Object,
            Options.Create(new AIOptions { MaximumSkillContextCharacters = maxCharacters }),
            NullLogger<AISkillCatalog>.Instance);
    }

    private void CreateFile(string relative, string content)
    {
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
