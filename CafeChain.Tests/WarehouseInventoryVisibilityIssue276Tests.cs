using Xunit;

namespace CafeChain.Tests;

public sealed class WarehouseInventoryVisibilityIssue276Tests
{
    [Fact]
    public void InventoryVisibilityPages_UseSharedWarehouseShell()
    {
        foreach (var path in new[]
        {
            "CafeChain/Areas/Admin/Views/AdminStoreInventory/Index.cshtml",
            "CafeChain/Areas/Admin/Views/AdminInventoryThresholds/Index.cshtml",
            "CafeChain/Areas/Admin/Views/AdminStockAlerts/Index.cshtml",
            "CafeChain/Areas/Admin/Views/AdminStockAlerts/Details.cshtml",
            "CafeChain/Areas/Admin/Views/AdminReorderSuggestions/Index.cshtml",
            "CafeChain/Areas/Admin/Views/AdminNotifications/Index.cshtml"
        })
        {
            Assert.Contains("cc-warehouse-page", Read(path));
        }
    }

    [Fact]
    public void InventoryTables_UseResponsiveSharedShell()
    {
        Assert.Contains("cc-warehouse-table-shell", Read("CafeChain/Areas/Admin/Views/AdminStoreInventory/Partials/_InventoryTablePartial.cshtml"));
        Assert.Contains("cc-warehouse-table-shell", Read("CafeChain/Areas/Admin/Views/AdminInventoryThresholds/Index.cshtml"));
        Assert.Contains("cc-warehouse-table-shell", Read("CafeChain/Areas/Admin/Views/AdminStockAlerts/Index.cshtml"));
        Assert.Contains("cc-warehouse-table-shell", Read("CafeChain/Areas/Admin/Views/AdminReorderSuggestions/Index.cshtml"));
    }

    [Fact]
    public void InventoryEmptyStates_AreAccessibleAndActionable()
    {
        var inventory = Read("CafeChain/Areas/Admin/Views/AdminStoreInventory/Partials/_InventoryTablePartial.cshtml");
        var thresholds = Read("CafeChain/Areas/Admin/Views/AdminInventoryThresholds/Index.cshtml");
        var alerts = Read("CafeChain/Areas/Admin/Views/AdminStockAlerts/Index.cshtml");
        var notifications = Read("CafeChain/Areas/Admin/Views/AdminNotifications/Index.cshtml");

        Assert.Contains("cc-warehouse-empty", inventory);
        Assert.Contains("Mở tồn kho cửa hàng", thresholds);
        Assert.Contains("Xem yêu cầu bổ sung", alerts);
        Assert.Contains("role=\"status\"", notifications);
    }

    [Fact]
    public void ReorderSuggestions_UseSemanticTokensWithoutGradientButtons()
    {
        var css = Read("CafeChain/wwwroot/css/Admin/Procurement/reorder-suggestions.css");

        Assert.Contains("background: var(--cc-primary)", css);
        Assert.Contains("background: var(--cc-primary-hover)", css);
        Assert.Contains("background: var(--cc-surface)", css);
        Assert.DoesNotContain("linear-gradient", css, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
