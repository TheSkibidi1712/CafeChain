using Xunit;

namespace CafeChain.Tests;

public sealed class WarehouseSurfaceAndReceiptHoverRegressionTests
{
    [Fact]
    public void OperationalIceAndPurchaseAdvice_UseTheSameWarehouseCanvas()
    {
        var operationalIceCss = Read("CafeChain/wwwroot/css/Admin/OperationalIce/operational-ice.css");
        var purchaseAdviceCss = Read("CafeChain/wwwroot/css/Admin/PurchaseAdvice/purchase-advice.css");

        Assert.Contains("--ice-bg: var(--cc-app-bg", operationalIceCss);
        Assert.Contains("background: var(--cc-app-bg", purchaseAdviceCss);
    }

    [Fact]
    public void BranchReceiptHover_IsPaintedPerCellWithoutMovingReferenceContent()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminBranchReceipts/Index.cshtml");
        var css = Read("CafeChain/wwwroot/css/Admin/InventoryOperations/inventory-operations.css");

        Assert.Contains("class=\"ops-status-tabs\"", view);
        Assert.DoesNotContain("class=\"ops-tabs\"", view);
        Assert.Contains("branch-receipt-table", view);
        Assert.Contains("ops-reference-cell", view);
        Assert.Contains(".branch-receipt-table tbody tr:hover > td", css);
        Assert.Contains("background: transparent !important", css);
        Assert.Contains("transform: none", css);
    }

    private static string Read(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine(
            root,
            relativePath.Replace("/", Path.DirectorySeparatorChar.ToString())));
    }
}
