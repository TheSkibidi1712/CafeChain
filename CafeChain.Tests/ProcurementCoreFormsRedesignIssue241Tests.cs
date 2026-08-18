using Xunit;

namespace CafeChain.Tests;

public sealed class ProcurementCoreFormsRedesignIssue241Tests
{
    [Fact]
    public void InventoryThresholds_HasComposedEmptyState()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminInventoryThresholds/Index.cshtml");

        Assert.Contains("ops-empty-icon", view);
        Assert.Contains("Không tìm thấy mặt hàng cần cấu hình", view);
        Assert.Contains("Mở tồn kho cửa hàng", view);
    }

    [Fact]
    public void StoreInventory_NavigationIsAccessible()
    {
        var tabs = Read("CafeChain/Areas/Admin/Views/AdminStoreInventory/Partials/_StoreTabsPartial.cshtml");
        var pagination = Read("CafeChain/Areas/Admin/Views/AdminStoreInventory/Partials/_PaginationPartial.cshtml");

        Assert.Contains("aria-label=\"Chọn cửa hàng xem tồn kho\"", tabs);
        Assert.Contains("aria-current", tabs);
        Assert.Contains("aria-label=\"Phân trang tồn kho\"", pagination);
        Assert.Contains("aria-label=\"Trang trước\"", pagination);
        Assert.Contains("aria-label=\"Trang sau\"", pagination);
    }

    [Fact]
    public void RestockForms_UseStructuredOperationalSections()
    {
        var manual = Read("CafeChain/Areas/Admin/Views/AdminRestockRequests/CreateManual.cshtml");
        var central = Read("CafeChain/Areas/Admin/Views/AdminRestockRequests/CreateCentralPlanner.cshtml");

        Assert.Contains("ops-panel-header", manual);
        Assert.Contains("ops-form-grid", central);
        Assert.Contains("asp-validation-summary", central);
        Assert.Contains("ops-form-actions", central);
        Assert.Contains("Bằng chứng dự báo / kế hoạch", central);
    }

    [Fact]
    public void RestockAndPurchaseOrderLists_HaveActionableEmptyStates()
    {
        var restock = Read("CafeChain/Areas/Admin/Views/AdminRestockRequests/Index.cshtml");
        var purchaseOrders = Read("CafeChain/Areas/Admin/Views/AdminPurchaseOrders/Index.cshtml");

        Assert.Contains("Không có yêu cầu bổ sung phù hợp", restock);
        Assert.Contains("Tạo yêu cầu thủ công", restock);
        Assert.Contains("Chưa có đơn đặt hàng", purchaseOrders);
        Assert.Contains("Xem yêu cầu bổ sung", purchaseOrders);
    }

    [Fact]
    public void PurchaseOrderCreate_HasValidationAndDoubleSubmitGuard()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminPurchaseOrders/Create.cshtml");

        Assert.Contains("id=\"createPurchaseOrderForm\"", view);
        Assert.Contains("asp-validation-summary", view);
        Assert.Contains("data-submit-button", view);
        Assert.Contains("Đang lưu bản nháp", view);
        Assert.Contains("ops-policy-panel", view);
        Assert.Contains("ops-readonly-field", view);
    }

    [Fact]
    public void PackagedReceiptDraft_MakesPackageCountingExplicit()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminBranchReceipts/PurchaseOrderDraft.cshtml");

        Assert.Contains("Số gói thực giao", view);
        Assert.Contains("Số gói từ chối", view);
        Assert.Contains("Số gói chấp nhận dự kiến", view);
        Assert.Contains("mỗi gói =", view);
    }

    [Fact]
    public void PurchaseOrderAndStockAlert_DangerActionsAreSeparated()
    {
        var purchaseOrder = Read("CafeChain/Areas/Admin/Views/AdminPurchaseOrders/Details.cshtml");
        var alert = Read("CafeChain/Areas/Admin/Views/AdminStockAlerts/Details.cshtml");

        Assert.Contains("ops-danger-disclosure", purchaseOrder);
        Assert.Contains("Hủy đơn đặt hàng", purchaseOrder);
        Assert.Contains("Đánh dấu báo sai hoặc đóng cảnh báo", alert);
        Assert.Contains("Đóng cảnh báo mà không tạo yêu cầu", alert);
    }

    [Fact]
    public void StockAlerts_HasActionableEmptyState()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminStockAlerts/Index.cshtml");

        Assert.Contains("Không có cảnh báo kho phù hợp", view);
        Assert.Contains("Xem tồn kho", view);
        Assert.Contains("Xem yêu cầu bổ sung", view);
    }

    [Fact]
    public void SupplierQuality_UsesResponsiveLabeledForms()
    {
        var index = Read("CafeChain/Areas/Admin/Views/AdminSupplierQuality/Index.cshtml");
        var create = Read("CafeChain/Areas/Admin/Views/AdminSupplierQuality/Create.cshtml");

        Assert.Contains("ops-form-grid", index);
        Assert.Contains("ops-table-actions", index);
        Assert.Contains("visually-hidden", index);
        Assert.Contains("Không có sự cố trong khoảng đã chọn", index);
        Assert.Contains("for=\"supplierIssueType\"", create);
        Assert.Contains("for=\"supplierIssueDescription\"", create);
        Assert.Contains("asp-validation-summary", create);
    }

    [Fact]
    public void SharedStyles_SupportFocusResponsiveAndReducedMotion()
    {
        var operations = Read("CafeChain/wwwroot/css/Admin/InventoryOperations/inventory-operations.css");
        var inventory = Read("CafeChain/wwwroot/css/Admin/StoreInventory/storeinventory.css");

        Assert.Contains(":focus-visible", operations);
        Assert.Contains("prefers-reduced-motion", operations);
        Assert.Contains(".ops-form-grid", operations);
        Assert.Contains(".ops-danger-disclosure", operations);
        Assert.Contains(":focus-visible", inventory);
        Assert.Contains("prefers-reduced-motion", inventory);
    }

    private static string Read(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
