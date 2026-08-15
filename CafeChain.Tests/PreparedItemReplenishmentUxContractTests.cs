using CafeChain.ViewModels.Admin.Shared;

namespace CafeChain.Tests;

public sealed class PreparedItemReplenishmentUxContractTests
{
    [Fact]
    public void PosInventoryThreshold_UsesSharedStrictBoundary()
    {
        var source = Read("CafeChain/Application/Services/POS/PosBranchInventoryService.cs");
        var status = CafeChain.Application.Services.POS.PosBranchInventoryService
            .MapThresholdStatus(10m, 10m);

        Assert.Equal(
            CafeChain.Application.Services.POS.PosBranchInventoryService.ThresholdStatusNormal,
            status);
        Assert.Contains("i.AvailableQty - i.ReservedQty >= i.MinStockLevel.Value", source, StringComparison.Ordinal);
        Assert.DoesNotContain("i.AvailableQty - i.ReservedQty <= i.MinStockLevel.Value", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeWorkspace_UsesAuthorizedBoundedReplenishmentProjection()
    {
        var controller = Read("CafeChain/Areas/Admin/Controllers/AdminRecipeController.cs");
        var view = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml");

        Assert.Contains("IPreparedItemReplenishmentReadService", controller, StringComparison.Ordinal);
        Assert.Contains("selectedStore == null", controller, StringComparison.Ordinal);
        Assert.Contains("return Forbid()", controller, StringComparison.Ordinal);
        Assert.Contains("openRunLimit: 5", controller, StringComparison.Ordinal);
        Assert.Contains("Model.Replenishment", view, StringComparison.Ordinal);
        Assert.Contains("Nguồn sản xuất đang mở", view, StringComparison.Ordinal);
        Assert.Contains("Còn cần bổ sung", view, StringComparison.Ordinal);
        Assert.Contains("không làm tăng tồn kho", view, StringComparison.Ordinal);
        Assert.DoesNotContain("tồn dự kiến", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecipeWorkspace_BoundedProductionCoverageIsTransparent()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml");

        Assert.Contains("HasMoreOpenProductionRuns", view, StringComparison.Ordinal);
        Assert.Contains("OpenProductionRunTotal", view, StringComparison.Ordinal);
        Assert.Contains("nguồn sản xuất đang mở gần nhất", view, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeWorkspace_ReplenishmentLinksArePermissionBound()
    {
        var controller = Read("CafeChain/Areas/Admin/Controllers/AdminRecipeController.cs");
        var view = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml");

        Assert.Contains("PermissionConstants.StockAlertView", controller, StringComparison.Ordinal);
        Assert.Contains("PermissionConstants.RestockView", controller, StringComparison.Ordinal);
        Assert.Contains("if (Model.CanViewStockAlerts", view, StringComparison.Ordinal);
        Assert.Contains("if (Model.CanViewRestockRequests", view, StringComparison.Ordinal);
        Assert.Contains("if (Model.CanViewProduction", view, StringComparison.Ordinal);
    }

    [Fact]
    public void RestockAndProductionViews_ExposeBusinessTraceability()
    {
        var restock = Read("CafeChain/Areas/Admin/Views/AdminRestockRequests/Details.cshtml");
        var productionIndex = Read("CafeChain/Areas/Admin/Views/AdminProductionOrder/Index.cshtml");
        var productionDetails = Read("CafeChain/Areas/Admin/Views/AdminProductionOrder/Details.cshtml");

        Assert.Contains("Nhu cầu bổ sung", restock, StringComparison.Ordinal);
        Assert.Contains("Nguồn sản xuất", restock, StringComparison.Ordinal);
        Assert.Contains("SourcingAllocation", restock, StringComparison.Ordinal);
        Assert.Contains("canViewProduction", restock, StringComparison.Ordinal);
        Assert.Contains("Nhu cầu bổ sung", productionIndex, StringComparison.Ordinal);
        Assert.Contains("Nhu cầu bổ sung", productionDetails, StringComparison.Ordinal);
    }

    [Fact]
    public void PreparedItemDemandAdjustment_UsesCurrentFulfillmentAndActiveCoverage()
    {
        var controller = Read("CafeChain/Areas/Admin/Controllers/AdminRestockRequestsController.cs");

        Assert.Contains("result.Data.FulfillmentPostings.Sum(x => x.Quantity)", controller, StringComparison.Ordinal);
        Assert.Contains("RestockSourcingAllocationStatuses.Active", controller, StringComparison.Ordinal);
        Assert.Contains("RestockSourcingAllocationStatuses.PendingPurchaseAdvice", controller, StringComparison.Ordinal);
        Assert.Contains("remainingRequestDemandBase - activeRequestCoverageBase", controller, StringComparison.Ordinal);
        Assert.Contains("replenishment.Data.NetNeedBase.Value - activeRequestUnallocatedBase", controller, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "replenishment.Data.NetNeedBase.Value - result.Data.RemainingUnallocatedQuantity",
            controller,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ACTIVE", "Đang mở")]
    [InlineData("PENDING_PURCHASE", "Chờ đề nghị mua")]
    [InlineData("RELEASED", "Đã giải phóng")]
    [InlineData("CANCELLED", "Đã hủy")]
    public void SourcingAllocationStatus_IsLocalized(string code, string expected)
    {
        var descriptor = AdminStatusDisplay.SourcingAllocation(code);

        Assert.Equal(expected, descriptor.Label);
        Assert.DoesNotContain(code, descriptor.Label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReplenishmentSignal_CollapsesToOneColumnOnMobile()
    {
        var css = Read("CafeChain/wwwroot/css/Admin/Recipe/recipe-workspace.css");

        Assert.Contains(".recipe-workspace__replenishment-grid", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 767.98px)", css, StringComparison.Ordinal);
        Assert.Contains(".recipe-workspace__replenishment-links", css, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: anywhere", css, StringComparison.Ordinal);
    }

    [Fact]
    public void StoreScopeDenial_DoesNotExposeInternalReasonCode()
    {
        var view = Read("CafeChain/Areas/Admin/Views/Shared/StoreScopeError.cshtml");

        Assert.Contains("@Model.Message", view, StringComparison.Ordinal);
        Assert.DoesNotContain("@Model.ErrorCode", view, StringComparison.Ordinal);
        Assert.DoesNotContain("<code>", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReplenishmentWorkflow_DoesNotExposeUnexplainedTechnicalTerms()
    {
        var productionIndex = Read("CafeChain/Areas/Admin/Views/AdminProductionOrder/Index.cshtml");
        var productionDetails = Read("CafeChain/Areas/Admin/Views/AdminProductionOrder/Details.cshtml");
        var restockDetails = Read("CafeChain/Areas/Admin/Views/AdminRestockRequests/Details.cshtml");
        var alertDetails = Read("CafeChain/Areas/Admin/Views/AdminStockAlerts/Details.cshtml");

        Assert.DoesNotContain("Sản xuất / BOM", productionIndex, StringComparison.Ordinal);
        Assert.Contains("Nhập trước - xuất trước (FIFO)", productionDetails, StringComparison.Ordinal);
        Assert.DoesNotContain(">PA<", restockDetails, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Snapshot tồn kho", alertDetails, StringComparison.OrdinalIgnoreCase);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CafeChain.slnx"))
                || File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy thư mục gốc CafeChain.");
    }
}
