namespace CafeChain.Tests;

public sealed class InventoryAiRemovalSourceTests
{
    private static readonly string[] RemovedSymbols =
    [
        "SuggestInventoryInputAsync",
        "SuggestSupplierAsync",
        "GetSupplierOffersAsync",
        "InventoryInputSuggestionResultDTO",
        "SupplierSuggestionResultDTO"
    ];

    [Fact]
    public void InventoryCreateSources_DoNotContainRemovedAiFeatures()
    {
        var root = FindRepoRoot();
        var files = new[]
        {
            Path.Combine(root, "CafeChain", "Areas", "Admin", "Controllers", "AdminAIController.cs"),
            Path.Combine(root, "CafeChain", "Application", "Interfaces", "AI", "IAIService.cs"),
            Path.Combine(root, "CafeChain", "Application", "Services", "AI", "AIService.cs"),
            Path.Combine(root, "CafeChain", "wwwroot", "js", "Admin", "InventoryDocument", "inventorydocumentcreate.js"),
            Path.Combine(root, "CafeChain", "Areas", "Admin", "Views", "AdminInventoryDocument", "Partials", "Create", "_ActionBar.cshtml"),
            Path.Combine(root, "CafeChain", "Areas", "Admin", "Views", "AdminInventoryDocument", "Partials", "Create", "_CreateModal.cshtml")
        };

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            foreach (var removed in RemovedSymbols)
                Assert.DoesNotContain(removed, source, StringComparison.Ordinal);
        }

        Assert.False(File.Exists(Path.Combine(root, "CafeChain", "Application", "DTOs", "AI", "InventoryInputSuggestionDtos.cs")));
        Assert.False(File.Exists(Path.Combine(root, "CafeChain", "Application", "DTOs", "AI", "SupplierSuggestionDtos.cs")));
    }

    [Fact]
    public void UnrelatedAiMasterDataAndImageContracts_RemainAvailable()
    {
        var root = FindRepoRoot();
        var contract = File.ReadAllText(Path.Combine(
            root, "CafeChain", "Application", "Interfaces", "AI", "IAIService.cs"));

        Assert.Contains("SuggestCategoriesAsync", contract, StringComparison.Ordinal);
        Assert.Contains("CheckHealthAsync", contract, StringComparison.Ordinal);

        var imageContract = File.ReadAllText(Path.Combine(
            root, "CafeChain", "Application", "Interfaces", "AI", "IAIImagePipelineService.cs"));
        Assert.Contains("GenerateFromPromptAsync", imageContract, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "CafeChain"))
                && Directory.Exists(Path.Combine(directory.FullName, "CafeChain.Tests")))
                return directory.FullName;
            directory = directory.Parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
