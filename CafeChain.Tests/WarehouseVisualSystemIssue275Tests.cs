using Xunit;

namespace CafeChain.Tests;

public sealed class WarehouseVisualSystemIssue275Tests
{
    private const string TokensPath = "CafeChain/wwwroot/css/Admin/Procurement/procurement-design-system.css";
    private const string DashboardPath = "CafeChain/wwwroot/css/Admin/Dashboard/dashboard.css";

    [Fact]
    public void WarehousePages_UseSharedVisualTokens()
    {
        var css = Read(TokensPath);
        var dashboardCss = Read(DashboardPath);
        var inventoryCss = Read("CafeChain/wwwroot/css/Admin/InventoryDocument/inventorydocument.css");
        var inventoryView = Read("CafeChain/Areas/Admin/Views/AdminInventoryDocument/Index.cshtml");

        Assert.Contains("--an-bg:", dashboardCss);
        Assert.Contains("#F7F4F0", dashboardCss, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--cc-app-bg: #f7f4f0", css);
        Assert.Contains("--cc-surface: #fffdfb", css);
        Assert.Contains("--cc-primary: #70482f", css);
        Assert.Contains("--cc-primary-hover: #3d2418", css);
        Assert.Contains("--cc-accent: #a97750", css);
        Assert.Contains("--cc-text-primary: #201812", css);
        Assert.Contains("--cc-border: #e9ded4", css);
        Assert.Contains("--cc-surface-accent", css);
        Assert.Contains("--cc-shadow-soft", css);
        Assert.Contains(".cc-warehouse-page", css);
        Assert.Contains(".cc-warehouse-header", css);
        Assert.Contains(".cc-warehouse-summary-grid", css);
        Assert.Contains(".cc-warehouse-table-shell", css);
        Assert.Contains("var(--cc-app-bg)", inventoryCss);
        Assert.Contains("cc-warehouse-page", inventoryView);
        Assert.Contains("cc-warehouse-header", inventoryView);
    }

    [Fact]
    public void WarehousePages_UseConsistentPrimaryActions()
    {
        var css = Read(TokensPath);
        var inventoryCss = Read("CafeChain/wwwroot/css/Admin/InventoryDocument/inventorydocument.css");

        Assert.Contains(".cc-button-primary", css);
        Assert.Contains("background: var(--cc-primary)", css);
        Assert.Contains("background: var(--cc-primary)", inventoryCss);
        Assert.Contains("background: var(--cc-primary-hover)", inventoryCss);
    }

    [Fact]
    public void WarehouseStatusBadges_AreConsistent()
    {
        var css = Read(TokensPath);

        Assert.Contains(".cc-status-badge", css);
        Assert.Contains(".cc-status-success", css);
        Assert.Contains(".cc-status-warning", css);
        Assert.Contains(".cc-status-danger", css);
    }

    [Fact]
    public void WarehouseEmptyStates_AreAccessible()
    {
        var css = Read(TokensPath);

        Assert.Contains(".cc-warehouse-empty", css);
        Assert.Contains("place-items: center", css);
        Assert.Contains("color: var(--cc-text-secondary)", css);
        Assert.Contains("text-align: center", css);
    }

    [Fact]
    public void WarehouseAlerts_DoNotRenderEmptyContent()
    {
        var css = Read(TokensPath);

        Assert.Contains(".cc-warehouse-alert:empty", css);
        Assert.Contains("display: none", css);
    }

    [Fact]
    public void WarehouseHeadersAndFilters_ExposeDashboardBasedVariants()
    {
        var css = Read(TokensPath);

        Assert.Contains(".cc-warehouse-header::before", css);
        Assert.Contains("background: linear-gradient(180deg, #c08a62, var(--cc-primary))", css);
        Assert.Contains(".cc-warehouse-header--compact", css);
        Assert.Contains(".cc-warehouse-header--detail", css);
        Assert.Contains(".cc-warehouse-filter-header", css);
        Assert.Contains(".cc-warehouse-filter:has(.cc-warehouse-filter-header)", css);
        Assert.Contains(".cc-warehouse-table-shell tbody tr:hover > td", css);
    }

    private static string Read(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
