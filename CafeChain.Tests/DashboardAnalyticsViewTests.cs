//namespace CafeChain.Tests;

//public sealed class DashboardAnalyticsViewTests
//{
//    [Fact]
//    public void Dashboard_shell_and_script_use_lazy_sections_without_embedded_analytics_dataset()
//    {
//        var root = FindRepoRoot();
//        var view = File.ReadAllText(Path.Combine(root, "CafeChain", "Areas", "Admin", "Views", "Dashboard", "Index.cshtml"));
//        var script = File.ReadAllText(Path.Combine(root, "CafeChain", "wwwroot", "js", "Admin", "Dashboard", "dashboard.js"));
//        var stylesheet = File.ReadAllText(Path.Combine(root, "CafeChain", "wwwroot", "css", "Admin", "Dashboard", "dashboard.css"));

//        foreach (var section in new[] { "Executive", "Operations", "Inventory", "Procurement", "Product", "Workforce" })
//            Assert.Contains($"data-section=\"@tab.Item1\"", view);

//        Assert.Contains("GetSection", view);
//        Assert.Contains("AbortController", script);
//        Assert.Contains("cache.clear()", script);
//        Assert.Contains("NO_DATA", script);
//        Assert.Contains("context.instance.resize()", script);
//        Assert.Contains("ResizeObserver", script);
//        Assert.Contains("interval: categoryAxis ? 0 : \"auto\"", script);
//        Assert.Contains("hideOverlap: dateAxis", script);
//        Assert.Contains("categoryCapacity", script);
//        Assert.Contains("wrapAxisLabel", script);
//        Assert.Contains("nonEmptyLabel", script);
//        Assert.Contains("dataZoom", script);
//        Assert.Contains("chartTooltip", script);
//        Assert.Contains("seriesBy: \"transactionType\"", script);
//        Assert.Contains("seriesBy: \"ingredientName\"", script);
//        Assert.Contains("aggregate: \"count\"", script);
//        Assert.Contains("stack: true", script);
//        Assert.Contains("Nhân sự & lịch dự kiến", view);
//        Assert.Contains("kế hoạch dự kiến, không phải dữ liệu chấm công", script);
//        Assert.Contains("overflow-wrap: anywhere", stylesheet);
//        Assert.Contains("word-break: break-word", stylesheet);
//        Assert.Contains("white-space: normal", stylesheet);
//        Assert.DoesNotContain("Model.Revenue", view);
//        Assert.DoesNotContain("dashboardData", view);
//    }

//    private static string FindRepoRoot()
//    {
//        var directory = new DirectoryInfo(AppContext.BaseDirectory);
//        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.csproj")))
//            directory = directory.Parent;
//        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
//    }
//}
