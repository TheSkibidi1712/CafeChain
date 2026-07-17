namespace CafeChain.Tests;

public sealed class DashboardAnalyticsViewTests
{
    [Fact]
    public void Dashboard_shell_and_script_use_lazy_sections_without_embedded_analytics_dataset()
    {
        var root = FindRepoRoot();
        var view = File.ReadAllText(Path.Combine(root, "CafeChain", "Areas", "Admin", "Views", "Dashboard", "Index.cshtml"));
        var script = File.ReadAllText(Path.Combine(root, "CafeChain", "wwwroot", "js", "Admin", "Dashboard", "dashboard.js"));

        foreach (var section in new[] { "Executive", "Operations", "Inventory", "Procurement", "Product", "Workforce" })
            Assert.Contains($"data-section=\"@tab.Item1\"", view);

        Assert.Contains("GetSection", view);
        Assert.Contains("AbortController", script);
        Assert.Contains("cache.clear()", script);
        Assert.Contains("NO_DATA", script);
        Assert.Contains("chartInstance.resize()", script);
        Assert.DoesNotContain("Model.Revenue", view);
        Assert.DoesNotContain("dashboardData", view);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.csproj")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
