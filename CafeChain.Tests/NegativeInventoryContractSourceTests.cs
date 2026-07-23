using System.Text.RegularExpressions;

namespace CafeChain.Tests;

public sealed class NegativeInventoryContractSourceTests
{
    [Fact]
    public void InventoryControllersUseConventionalRoutingAndVerbConstraints()
    {
        var root = FindRepoRoot();
        var document = Read(root, "CafeChain", "Areas", "Admin", "Controllers", "AdminInventoryDocumentController.cs");
        var transfer = Read(root, "CafeChain", "Areas", "Admin", "Controllers", "AdminInventoryTransferController.cs");

        Assert.Contains("[AutoValidateAntiforgeryToken]", document, StringComparison.Ordinal);
        Assert.Contains("[AutoValidateAntiforgeryToken]", transfer, StringComparison.Ordinal);
        Assert.DoesNotContain("[Route(", document, StringComparison.Ordinal);
        Assert.DoesNotContain("[Route(", transfer, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"\[Http(?:Get|Post)\s*\(\s*[^)]", RegexOptions.CultureInvariant), document);
        Assert.DoesNotMatch(new Regex(@"\[Http(?:Get|Post)\s*\(\s*[^)]", RegexOptions.CultureInvariant), transfer);

        AssertPostAction(document, "Preflight");
        AssertPostAction(document, "SaveDraft");
        AssertPostAction(document, "Submit");
        AssertPostAction(document, "ConfirmDraft");
        AssertPostAction(document, "ApproveNegative");
        AssertPostAction(document, "RejectNegative");
        AssertPostAction(document, "CancelInventoryDocument");
        Assert.DoesNotContain("Task<IActionResult> Create([FromBody]", document, StringComparison.Ordinal);

        AssertPostAction(transfer, "Preflight");
        AssertPostAction(transfer, "SaveDraft");
        AssertPostAction(transfer, "UpdateDraft");
        AssertPostAction(transfer, "Dispatch");
        AssertPostAction(transfer, "Receive");
        AssertPostAction(transfer, "Cancel");
        Assert.DoesNotContain("Task<IActionResult> ValidateStock(", transfer, StringComparison.Ordinal);
        Assert.DoesNotContain("Task<IActionResult> CreateDraft(", transfer, StringComparison.Ordinal);
        Assert.DoesNotContain("Task<IActionResult> Confirm(", transfer, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryClientsUseRazorGeneratedEndpointsWithoutHardcodedRoutes()
    {
        var root = FindRepoRoot();
        var document = Read(root, "CafeChain", "Areas", "Admin", "Controllers", "AdminInventoryDocumentController.cs");
        var documentIndex = Read(root, "CafeChain", "Areas", "Admin", "Views", "AdminInventoryDocument", "Index.cshtml");
        var documentClient = Read(root, "CafeChain", "wwwroot", "js", "Admin", "InventoryDocument", "inventorydocument.js")
            + Read(root, "CafeChain", "wwwroot", "js", "Admin", "InventoryDocument", "inventorydocumentcreate.js");
        var transferCreate = Read(root, "CafeChain", "Areas", "Admin", "Views", "AdminInventoryTransfer", "Create.cshtml");
        var transferClient = Read(root, "CafeChain", "wwwroot", "js", "Admin", "InventoryTransfer", "inventorytransfercreate.js");
        var affectedViews = string.Join(
            Environment.NewLine,
            Directory.GetFiles(
                    Path.Combine(root, "CafeChain", "Areas", "Admin", "Views", "AdminInventoryDocument"),
                    "*.cshtml",
                    SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(
                    Path.Combine(root, "CafeChain", "Areas", "Admin", "Views", "AdminInventoryTransfer"),
                    "*.cshtml",
                    SearchOption.AllDirectories))
                .Select(File.ReadAllText));

        Assert.Contains("Url.Action(\"Submit\"", documentIndex, StringComparison.Ordinal);
        Assert.Contains("Url.Action(\"Preflight\"", documentIndex, StringComparison.Ordinal);
        Assert.Contains("Url.Action(\"StoreInventoryIngredients\"", documentIndex, StringComparison.Ordinal);
        Assert.Contains("StoreInventoryIngredients", document, StringComparison.Ordinal);
        Assert.DoesNotContain("StoreExportIngredients", document, StringComparison.Ordinal);
        Assert.Contains("storeInventoryIngredientsUrl", documentClient, StringComparison.Ordinal);
        Assert.DoesNotContain("storeExportIngredientsUrl", documentClient, StringComparison.Ordinal);
        Assert.Contains("type === documentType.stockTake", documentClient, StringComparison.Ordinal);
        Assert.Contains("allowsManualNegativeExport() || isStockTake()", documentClient, StringComparison.Ordinal);
        Assert.Contains("Url.Action(\"Dispatch\"", transferCreate, StringComparison.Ordinal);
        Assert.Contains("Url.Action(\"SaveDraft\"", transferCreate, StringComparison.Ordinal);
        Assert.DoesNotContain("/Admin/AdminInventoryDocument/", documentClient, StringComparison.Ordinal);
        Assert.DoesNotContain("/Admin/InventoryDocument/", documentClient, StringComparison.Ordinal);
        Assert.DoesNotContain("/Admin/AdminInventoryTransfer/", transferClient, StringComparison.Ordinal);
        Assert.DoesNotContain("/Admin/InventoryTransfer/", transferClient, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/Admin/AdminInventoryDocument/", affectedViews, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/Admin/AdminInventoryTransfer/", affectedViews, StringComparison.Ordinal);
        Assert.DoesNotContain("action=\"/Admin/AdminInventoryDocument/", affectedViews, StringComparison.Ordinal);
        Assert.DoesNotContain("action=\"/Admin/AdminInventoryTransfer/", affectedViews, StringComparison.Ordinal);
        Assert.Contains("RequestVerificationToken", documentClient, StringComparison.Ordinal);
        Assert.Contains("RequestVerificationToken", transferClient, StringComparison.Ordinal);
        Assert.DoesNotContain("transfer-price", transferClient, StringComparison.Ordinal);
        Assert.DoesNotContain("suggestedUnitPrice", transferClient, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NegativeApprovalUiIsPermissionGatedAndRequiresRejectNote()
    {
        var root = FindRepoRoot();
        var viewModel = Read(root, "CafeChain", "ViewModels", "Admin", "InventoryDocuments", "Detail", "AdminInventoryDocumentDetailVM.cs");
        var service = Read(root, "CafeChain", "Application", "Services", "Admin", "InventoryDocuments", "AdminInventoryDocumentService.cs");
        var detail = Read(root, "CafeChain", "Areas", "Admin", "Views", "AdminInventoryDocument", "Partials", "Detail", "_DetailDocument.cshtml");
        var modal = Read(root, "CafeChain", "Areas", "Admin", "Views", "AdminInventoryDocument", "Partials", "Detail", "_DetailModal.cshtml");
        var table = Read(root, "CafeChain", "Areas", "Admin", "Views", "AdminInventoryDocument", "Partials", "Detail", "_DocumentTable.cshtml");
        var client = Read(root, "CafeChain", "wwwroot", "js", "Admin", "InventoryDocument", "inventorydocument.js");
        var mutationService = Read(root, "CafeChain", "Application", "Services", "Admin", "InventoryDocuments", "AdminInventoryDocumentCreateService.cs");

        Assert.Contains("CanReviewNegativeApproval", viewModel, StringComparison.Ordinal);
        Assert.Contains("GetNegativeApprovalReviewMessage", service, StringComparison.Ordinal);
        Assert.Contains("Bạn không thể tự duyệt phiếu", service, StringComparison.Ordinal);
        Assert.Contains("CanApproveNegative(roles)", service, StringComparison.Ordinal);
        Assert.Contains("Model.CanReviewNegativeApproval", modal, StringComparison.Ordinal);
        Assert.Contains("Model.NegativeApprovalReviewMessage", modal, StringComparison.Ordinal);
        Assert.Contains("modal-footer inventory-negative-approval-footer", modal, StringComparison.Ordinal);
        Assert.Contains("btn-approve-negative", modal, StringComparison.Ordinal);
        Assert.Contains("btn-reject-negative", modal, StringComparison.Ordinal);
        Assert.DoesNotContain("btn-approve-negative", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("btn-reject-negative", detail, StringComparison.Ordinal);
        Assert.Contains("Chờ duyệt xuất âm", table, StringComparison.Ordinal);
        Assert.Contains("Mở để duyệt", table, StringComparison.Ordinal);
        Assert.Contains("Lý do từ chối là bắt buộc", client, StringComparison.Ordinal);
        Assert.Contains("target: dialogTarget", client, StringComparison.Ordinal);
        Assert.Contains("button.closest(selectors.modal)", client, StringComparison.Ordinal);
        Assert.Contains("popup.querySelector(\".swal2-textarea\")", client, StringComparison.Ordinal);
        Assert.Contains("input.focus({ preventScroll: true })", client, StringComparison.Ordinal);
        Assert.Contains("không được tự phê duyệt", mutationService, StringComparison.Ordinal);
        Assert.Contains("dữ liệu tồn kho hoặc chính sách đã thay đổi", mutationService, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveSeedContainsOnlyFailClosedManualNegativeSettings()
    {
        var source = Read(FindRepoRoot(), "CafeChain", "Data", "Configurations", "Systems", "SystemSettingConfiguration.cs");

        Assert.Contains("inventory_manual_external_export_negative_enabled", source, StringComparison.Ordinal);
        Assert.DoesNotContain("inventory_allow_negative_stock", source, StringComparison.Ordinal);
        Assert.DoesNotContain("inventory_default_max_negative_quantity", source, StringComparison.Ordinal);
    }

    private static void AssertPostAction(string source, string action) =>
        Assert.Matches(new Regex($@"\[HttpPost\]\s+public\s+async\s+Task<IActionResult>\s+{Regex.Escape(action)}\s*\(", RegexOptions.CultureInvariant), source);

    private static string Read(string root, params string[] path) =>
        File.ReadAllText(Path.Combine([root, .. path]));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "CafeChain"))
                && Directory.Exists(Path.Combine(directory.FullName, "CafeChain.Tests")))
                return directory.FullName;
            directory = directory.Parent;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }
}
