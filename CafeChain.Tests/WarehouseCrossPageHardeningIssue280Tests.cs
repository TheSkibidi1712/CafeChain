using Xunit;

namespace CafeChain.Tests;

public sealed class WarehouseCrossPageHardeningIssue280Tests
{
    private static readonly string[] CreateOrEditPages =
    [
        "CafeChain/Areas/Admin/Views/AdminPurchaseOrders/Create.cshtml",
        "CafeChain/Areas/Admin/Views/AdminSupplierQuality/Create.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Create.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Edit.cshtml",
        "CafeChain/Areas/Admin/Views/AdminBranchReceipts/Create.cshtml",
        "CafeChain/Areas/Admin/Views/AdminBranchReceipts/PurchaseOrderDraft.cshtml",
        "CafeChain/Areas/Admin/Views/AdminRestockRequests/CreateManual.cshtml",
        "CafeChain/Areas/Admin/Views/AdminRestockRequests/CreateCentralPlanner.cshtml",
        "CafeChain/Areas/Admin/Views/AdminUnitConversion/Create.cshtml",
        "CafeChain/Areas/Admin/Views/AdminUnitConversion/Edit.cshtml"
    ];

    private static readonly string[] DetailPages =
    [
        "CafeChain/Areas/Admin/Views/AdminPurchaseOrders/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseOrderBatches/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminBranchReceipts/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminStockAlerts/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminRestockRequests/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminOperationalIce/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminOperationalIce/Report.cshtml"
    ];

    [Fact]
    public void CreateAndEditPages_UseCompactSharedHeaders()
    {
        foreach (var path in CreateOrEditPages)
        {
            var view = Read(path);

            Assert.Contains("cc-warehouse-header", view);
            Assert.Contains("cc-warehouse-header--compact", view);
        }
    }

    [Fact]
    public void DetailAndReportPages_UseCompactDetailHeaders()
    {
        foreach (var path in DetailPages)
        {
            var view = Read(path);

            Assert.Contains("cc-warehouse-header--compact", view);
            Assert.Contains("cc-warehouse-header--detail", view);
        }
    }

    [Theory]
    [InlineData("1366px")]
    [InlineData("1280px")]
    [InlineData("1024px")]
    [InlineData("800px")]
    [InlineData("600px")]
    public void SharedStyles_KeepRequiredResponsiveBreakpoints(string viewport)
    {
        var css = Read("CafeChain/wwwroot/css/Admin/Procurement/procurement-design-system.css");

        Assert.Contains($"@media (max-width: {viewport})", css);
        Assert.Contains("overflow-x: auto", css);
        Assert.Contains("overscroll-behavior-inline: contain", css);
        Assert.Contains("scrollbar-gutter: stable", css);
    }

    [Fact]
    public void IngredientPagination_ExposesCurrentPageAndNamedControls()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminIngredient/Index.cshtml");

        Assert.Contains("aria-current=", view);
        Assert.Contains("aria-label=\"Trang trước\"", view);
        Assert.Contains("aria-label=\"Trang sau\"", view);
        Assert.Contains("aria-label=\"Trang @i\"", view);
    }

    [Fact]
    public void SharedStyles_PreserveFocusMotionEmptyAndHoverStates()
    {
        var css = Read("CafeChain/wwwroot/css/Admin/Procurement/procurement-design-system.css");

        Assert.Contains(":focus-visible", css);
        Assert.Contains("prefers-reduced-motion", css);
        Assert.Contains(".cc-warehouse-empty", css);
        Assert.Contains(".cc-warehouse-table-shell tbody tr:hover > td", css);
    }

    private static string Read(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine(
            root,
            relativePath.Replace("/", Path.DirectorySeparatorChar.ToString())));
    }
}
