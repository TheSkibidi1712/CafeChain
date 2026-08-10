namespace CafeChain.Tests.POS;

public sealed class ModalViewportContractTests
{
    [Fact]
    public void Bootstrap_modals_have_global_viewport_and_scroll_contracts()
    {
        var site = Read("CafeChain", "wwwroot", "css", "site.css");
        var admin = Read("CafeChain", "wwwroot", "css", "Admin", "admin-unified-depth.css");
        var staffHub = Read("CafeChain", "Views", "StaffHub", "Index.cshtml");
        var supplier = Read("CafeChain", "Areas", "Admin", "Views", "AdminSupplier", "Index.cshtml");

        Assert.Contains("100dvh", site, StringComparison.Ordinal);
        Assert.Contains("env(safe-area-inset-top)", site, StringComparison.Ordinal);
        Assert.Contains("overflow-y: auto", site, StringComparison.Ordinal);
        Assert.Contains("100dvh", admin, StringComparison.Ordinal);
        Assert.Contains("overflow-y: auto", admin, StringComparison.Ordinal);
        Assert.True(Count(staffHub, "modal-body staffhub-pos-dialog-body") >= 3);
        Assert.Contains("modal-body supplier-modal-body", supplier, StringComparison.Ordinal);

        var viewRoot = Path.Combine(RepoRoot(), "CafeChain", "Views");
        var modalDialogs = Directory.EnumerateFiles(viewRoot, "*.cshtml", SearchOption.AllDirectories)
            .SelectMany(path => System.Text.RegularExpressions.Regex.Matches(
                    File.ReadAllText(path),
                    "class=\"[^\"]*modal-dialog[^\"]*\"")
                .Select(match => match.Value))
            .ToArray();
        Assert.NotEmpty(modalDialogs);
        Assert.All(modalDialogs, value => Assert.Contains("modal-dialog-scrollable", value, StringComparison.Ordinal));
    }

    [Fact]
    public void Staffhub_modals_are_portaled_above_the_body_backdrop()
    {
        var script = Read("CafeChain", "wwwroot", "js", "StaffHub", "staffhub-schedule.js");
        var css = Read("CafeChain", "wwwroot", "css", "StaffHub", "staffhub.css");
        var guard = Read("CafeChain", "wwwroot", "js", "shared", "mutation-guard.js");

        Assert.Contains("document.body.appendChild(dialog)", script, StringComparison.Ordinal);
        Assert.Contains("AdminMutationGuard.run(\"staffhub-preview-terminal\", terminalSelect", script, StringComparison.Ordinal);
        Assert.Contains("body:has(.staffhub-page) > .modal-backdrop", css, StringComparison.Ordinal);
        Assert.Contains("body:has(.staffhub-page) > .swal2-container", css, StringComparison.Ordinal);
        Assert.Contains("z-index: 2000 !important", css, StringComparison.Ordinal);
        Assert.Contains("z-index: 1990 !important", css, StringComparison.Ordinal);
        Assert.Contains("z-index: 2010 !important", css, StringComparison.Ordinal);
        Assert.Contains("const isButton = button instanceof HTMLButtonElement", guard, StringComparison.Ordinal);
        Assert.Contains("if (isButton && originalLabels.has(button))", guard, StringComparison.Ordinal);
    }

    [Fact]
    public void Inventory_history_modal_has_independent_vertical_and_horizontal_scroll_regions()
    {
        var modal = Read("CafeChain", "Areas", "Admin", "Views", "AdminStoreInventory", "Partials", "_TransactionModalPartial.cshtml");
        var transactions = Read("CafeChain", "Areas", "Admin", "Views", "AdminStoreInventory", "Partials", "_TransactionPartial.cshtml");
        var css = Read("CafeChain", "wwwroot", "css", "Admin", "StoreInventory", "storeinventory.css");

        Assert.Contains("modal-dialog-scrollable", modal, StringComparison.Ordinal);
        Assert.Contains("class=\"modal-body\"", modal, StringComparison.Ordinal);
        Assert.Contains(".store-inventory-modal .modal-body", css, StringComparison.Ordinal);
        Assert.Contains("overflow-y: auto", css, StringComparison.Ordinal);
        Assert.Contains("height: calc(100dvh - 2rem)", css, StringComparison.Ordinal);
        Assert.Contains("class=\"transaction-table-wrap\"", transactions, StringComparison.Ordinal);
        Assert.Contains(".transaction-table-wrap", css, StringComparison.Ordinal);
        Assert.Contains("overflow-x: auto", css, StringComparison.Ordinal);
        Assert.DoesNotContain("margin: 5% auto", css, StringComparison.Ordinal);
    }

    [Fact]
    public void React_overlays_are_bounded_by_dynamic_viewport()
    {
        var branch = Read("CafeChain.Frontend", "src", "pages", "BranchInventory.tsx");
        var modifier = Read("CafeChain.Frontend", "src", "components", "ProductModifierModal.tsx");
        var payment = Read("CafeChain.Frontend", "src", "components", "pos", "payment", "PaymentWorkspace.tsx");
        var orderHistory = Read("CafeChain.Frontend", "src", "pages", "OrderHistory.tsx");

        Assert.Contains("max-h-[calc(100dvh-2rem)]", branch, StringComparison.Ordinal);
        Assert.Contains("overflow-y-auto", branch, StringComparison.Ordinal);
        Assert.Contains("max-h-[100dvh] min-h-0", modifier, StringComparison.Ordinal);
        Assert.Contains("max-h-[100dvh] min-h-0", payment, StringComparison.Ordinal);
        Assert.Contains("max-h-[100dvh] min-h-0", orderHistory, StringComparison.Ordinal);
        Assert.Contains("overscroll-contain", orderHistory, StringComparison.Ordinal);
    }

    private static int Count(string value, string token) =>
        value.Split(token, StringSplitOptions.None).Length - 1;

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([RepoRoot(), .. path]));

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "CafeChain"))
                && Directory.Exists(Path.Combine(directory.FullName, "CafeChain.Tests")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
