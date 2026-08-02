using Xunit;

namespace CafeChain.Tests;

public sealed class WarehousePurchasingIssue279Tests
{
    private static readonly string[] Pages =
    {
        "CafeChain/Areas/Admin/Views/AdminPurchaseOrders/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseOrders/Create.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseOrders/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminBranchReceipts/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminBranchReceipts/Create.cshtml",
        "CafeChain/Areas/Admin/Views/AdminBranchReceipts/PurchaseOrderDraft.cshtml",
        "CafeChain/Areas/Admin/Views/AdminBranchReceipts/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminSupplierQuality/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminSupplierQuality/Create.cshtml",
        "CafeChain/Areas/Admin/Views/AdminSupplier/Index.cshtml"
    };

    [Fact]
    public void PurchasingPages_UseSharedWarehouseShellAndHeader()
    {
        foreach (var path in Pages)
        {
            var view = Read(path);
            Assert.Contains("cc-warehouse-page", view);
            Assert.Contains("cc-warehouse-header", view);
        }
    }

    [Fact]
    public void PurchasingLists_UseResponsiveSharedTablesAndAccessibleEmptyStates()
    {
        var purchaseOrders = Read("CafeChain/Areas/Admin/Views/AdminPurchaseOrders/Index.cshtml");
        var receipts = Read("CafeChain/Areas/Admin/Views/AdminBranchReceipts/Index.cshtml");
        var quality = Read("CafeChain/Areas/Admin/Views/AdminSupplierQuality/Index.cshtml");
        var suppliers = Read("CafeChain/Areas/Admin/Views/AdminSupplier/Index.cshtml");

        foreach (var view in new[] { purchaseOrders, receipts, quality, suppliers })
        {
            Assert.Contains("cc-warehouse-table-shell", view);
            Assert.Contains("cc-warehouse-empty", view);
            Assert.Contains("role=\"status\"", view);
        }
    }

    [Fact]
    public void PurchasingForms_PreserveValidationAndExistingPostActions()
    {
        var purchaseOrder = Read("CafeChain/Areas/Admin/Views/AdminPurchaseOrders/Create.cshtml");
        var receipt = Read("CafeChain/Areas/Admin/Views/AdminBranchReceipts/Create.cshtml");
        var receiptDraft = Read("CafeChain/Areas/Admin/Views/AdminBranchReceipts/PurchaseOrderDraft.cshtml");
        var quality = Read("CafeChain/Areas/Admin/Views/AdminSupplierQuality/Create.cshtml");

        Assert.Contains("asp-validation-summary", purchaseOrder);
        Assert.Contains("cc-warehouse-alert", purchaseOrder);
        Assert.Contains("asp-action=\"Create\"", receipt);
        Assert.Contains("asp-validation-summary", receipt);
        Assert.Contains("asp-action=\"SavePurchaseOrderDraft\"", receiptDraft);
        Assert.Contains("method=\"post\"", quality);
        Assert.Contains("asp-validation-summary", quality);
    }

    [Fact]
    public void PurchaseAndReceiptWorkflowActions_RemainAvailable()
    {
        var order = Read("CafeChain/Areas/Admin/Views/AdminPurchaseOrders/Details.cshtml");
        var receipt = Read("CafeChain/Areas/Admin/Views/AdminBranchReceipts/Details.cshtml");

        Assert.Contains("asp-action=\"Approve\"", order);
        Assert.Contains("asp-action=\"MarkSent\"", order);
        Assert.Contains("asp-action=\"ReceivePurchaseOrder\"", order);
        Assert.Contains("asp-action=\"Confirm\"", receipt);
        Assert.Contains("asp-controller=\"AdminSupplierQuality\"", receipt);
    }

    [Fact]
    public void SupplierWorkspace_UsesSharedTokensAndPreservesInteractiveHooks()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminSupplier/Index.cshtml");
        var css = Read("CafeChain/wwwroot/css/Admin/Supplier/supplier.css");

        Assert.Contains("cc-warehouse-summary-grid", view);
        Assert.Contains("cc-modal", view);
        Assert.Contains("id=\"supplierSearch\"", view);
        Assert.Contains("id=\"supplierDetail\"", view);
        Assert.Contains("id=\"createSupplierModal\"", view);
        Assert.Contains("var(--cc-primary)", css);
        Assert.Contains("prefers-reduced-motion", css);
        Assert.DoesNotContain("linear-gradient", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("backdrop-filter", css, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SupplierWorkspace_UsesServerPagingAndLazyDetailSections()
    {
        var controller = Read("CafeChain/Areas/Admin/Controllers/AdminSupplierController.cs");
        var repository = Read("CafeChain/Infrastructure/Repositories/Admin/Suppliers/AdminSupplierRepository.cs");
        var view = Read("CafeChain/Areas/Admin/Views/AdminSupplier/Index.cshtml");
        var script = Read("CafeChain/wwwroot/js/Admin/Supplier/supplier.js");

        Assert.Contains("GetPagedAsync(search, status, page, pageSize", controller);
        Assert.Contains(".Skip((page - 1) * pageSize)", repository);
        Assert.Contains(".Take(pageSize)", repository);
        Assert.Contains(".Select(x => new AdminSupplierDTO", repository);
        Assert.Contains("name=", view);
        Assert.Contains("supplierSearch", view);
        Assert.Contains("supplierStatusFilter", view);
        Assert.Contains("supplier-pagination", view);
        Assert.Contains("GetAuditHistory", controller);
        Assert.Contains("ensureTabData", script);
        Assert.Contains("loadAuditHistory", script);
        Assert.DoesNotContain("addEventListener('input', applyFilters)", script);
        Assert.DoesNotContain("await Promise.all([loadOffers(), loadStores(), loadReferenceData()]);", script);
    }

    [Fact]
    public void SupplierWorkspace_UsesOperationalDrawerAndStructuredCreateModal()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminSupplier/Index.cshtml");
        var css = Read("CafeChain/wwwroot/css/Admin/Supplier/supplier.css");
        var script = Read("CafeChain/wwwroot/js/Admin/Supplier/supplier.js");

        Assert.Contains("supplier-list-heading", view);
        Assert.Contains("role=\"dialog\" aria-modal=\"true\" aria-label=\"Chi tiết nhà cung cấp\"", view);
        Assert.Contains("supplier-modal-body", view);
        Assert.Contains("supplier-modal-footer", view);
        Assert.Contains("id=\"supplierConfirmModal\"", view);
        Assert.Contains("role=\"alertdialog\"", view);
        Assert.Contains("supplier-form-section", view);
        Assert.Contains("Thông tin doanh nghiệp", view);
        Assert.Contains("Đầu mối chính", view);
        Assert.Contains("Ghi chú vận hành", view);

        Assert.Contains("inset: 0 0 0 auto", css);
        Assert.Contains("height: 100dvh", css);
        Assert.Contains("grid-template-rows: auto auto minmax(0, 1fr)", css);
        Assert.Contains("max-height: calc(100dvh - 48px)", css);
        Assert.Contains("grid-template-rows: minmax(0, 1fr) auto", css);

        Assert.Contains("function trapFocus", script);
        Assert.Contains("function syncPageScrollLock", script);
        Assert.Contains("detailReturnFocus?.focus()", script);
        Assert.Contains("modalReturnFocus?.focus()", script);
        Assert.Contains("function requestConfirmation", script);
        Assert.DoesNotContain("window.confirm", script);
    }

    [Fact]
    public void SupplierWorkspace_FollowsDashboardHierarchyAndStableOperationalLayout()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminSupplier/Index.cshtml");
        var css = Read("CafeChain/wwwroot/css/Admin/Supplier/supplier.css");
        var script = Read("CafeChain/wwwroot/js/Admin/Supplier/supplier.js");

        Assert.Contains("Kho & Cung ứng / Đối tác", view);
        Assert.Contains("Quản lý thông tin liên hệ, phạm vi cung ứng và trạng thái hợp tác.", view);
        Assert.Contains("supplier-filter-heading", view);
        Assert.Contains("supplier-filter-grid", view);
        Assert.Contains("supplier-filter-actions", view);
        Assert.Contains("supplier-create-grid", view);
        Assert.Contains("supplier-col-identity", view);
        Assert.Contains("id=\"detailStatus\"", view);
        Assert.Contains("aria-label=\"Xem chi tiết nhà cung cấp @item.Name\"", view);

        Assert.Contains("min-height: 148px", css);
        Assert.Contains("grid-template-columns: minmax(320px, 1.55fr)", css);
        Assert.Contains("width: min(1040px, 100%)", css);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr))", css);
        Assert.Contains("detailStatus.className", script);
    }

    private static string Read(
        string relativePath,
        [System.Runtime.CompilerServices.CallerFilePath] string callerFilePath = "")
    {
        var normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var sourceRoot = Directory.GetParent(Path.GetDirectoryName(callerFilePath)!)?.FullName;
        foreach (var startPath in new[] { sourceRoot, Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            if (string.IsNullOrWhiteSpace(startPath)) continue;
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, normalizedPath);
                if (File.Exists(candidate)) return File.ReadAllText(candidate);
                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException($"Không tìm thấy source contract: {relativePath}");
    }
}
