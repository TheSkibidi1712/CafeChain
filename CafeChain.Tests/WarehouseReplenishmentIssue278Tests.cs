using Xunit;

namespace CafeChain.Tests;

public sealed class WarehouseReplenishmentIssue278Tests
{
    private static readonly string[] WorkflowPages =
    {
        "CafeChain/Areas/Admin/Views/AdminRestockRequests/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminRestockRequests/CreateManual.cshtml",
        "CafeChain/Areas/Admin/Views/AdminRestockRequests/CreateCentralPlanner.cshtml",
        "CafeChain/Areas/Admin/Views/AdminRestockRequests/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Create.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Edit.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseOrderBatches/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseOrderBatches/Details.cshtml"
    };

    [Fact]
    public void ReplenishmentPages_UseSharedWarehouseShellAndHeader()
    {
        foreach (var path in WorkflowPages)
        {
            var view = Read(path);
            Assert.Contains("cc-warehouse-page", view);
            Assert.Contains("cc-warehouse-header", view);
        }
    }

    [Fact]
    public void ReplenishmentLists_UseSharedFiltersAndResponsiveTables()
    {
        foreach (var path in new[]
        {
            "CafeChain/Areas/Admin/Views/AdminRestockRequests/Index.cshtml",
            "CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Index.cshtml",
            "CafeChain/Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml",
            "CafeChain/Areas/Admin/Views/AdminPurchaseOrderBatches/Index.cshtml"
        })
        {
            var view = Read(path);
            Assert.Contains("cc-warehouse-filter", view);
            Assert.Contains("cc-warehouse-table-shell", view);
        }
    }

    [Fact]
    public void ReplenishmentForms_PreservePostsAndValidation()
    {
        var manual = Read("CafeChain/Areas/Admin/Views/AdminRestockRequests/CreateManual.cshtml");
        var planner = Read("CafeChain/Areas/Admin/Views/AdminRestockRequests/CreateCentralPlanner.cshtml");
        var advice = Read("CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Create.cshtml");
        var consolidation = Read("CafeChain/Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml");

        Assert.Contains("method=\"post\"", manual);
        Assert.Contains("asp-validation-summary", manual);
        Assert.Contains("method=\"post\"", planner);
        Assert.Contains("asp-validation-summary", planner);
        Assert.Contains("method=\"post\"", advice);
        Assert.Contains("asp-validation-summary", advice);
        Assert.Contains("asp-controller=\"AdminPurchaseOrderBatches\"", consolidation);
        Assert.Contains("createsConsolidatedOrder", consolidation);
        Assert.Contains("\"Create\" : \"CreateFromAdvice\"", consolidation);
    }

    [Fact]
    public void ReplenishmentDetails_ExposeWorkflowReferencesAndNextActions()
    {
        var request = Read("CafeChain/Areas/Admin/Views/AdminRestockRequests/Details.cshtml");
        var advice = Read("CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Details.cshtml");
        var batch = Read("CafeChain/Areas/Admin/Views/AdminPurchaseOrderBatches/Details.cshtml");

        Assert.Contains("Thao tác tiếp theo", request);
        Assert.Contains("Đề nghị mua liên kết", request);
        Assert.Contains("Đơn đặt hàng liên kết", request);
        Assert.Contains("Nguồn yêu cầu", advice);
        Assert.Contains("Tiến độ giao theo chi nhánh", batch);
        Assert.Contains("Truy vết nguồn nhu cầu", batch);
    }

    [Fact]
    public void ReplenishmentEmptyAndValidationStates_AreAccessible()
    {
        var restock = Read("CafeChain/Areas/Admin/Views/AdminRestockRequests/Index.cshtml");
        var advice = Read("CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Index.cshtml");
        var create = Read("CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Create.cshtml");

        Assert.Contains("cc-warehouse-empty", restock);
        Assert.Contains("role=\"status\"", advice);
        Assert.Contains("cc-warehouse-alert", create);
        Assert.Contains("role=\"alert\"", create);
    }

    private static string Read(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
