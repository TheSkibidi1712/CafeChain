using System.Text.RegularExpressions;
using Xunit;

namespace CafeChain.Tests;

public sealed class ProcurementOperationalMinimalismIssue240Tests
{
    private const string TokensPath = "CafeChain/wwwroot/css/Admin/Procurement/procurement-design-system.css";
    private const string RequestDetailPath = "CafeChain/Areas/Admin/Views/AdminRestockRequests/Details.cshtml";
    private const string CreatePaPath = "CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Create.cshtml";

    [Fact]
    public void DesignTokens_AreCentralized()
    {
        var css = Read(TokensPath);

        foreach (var token in new[]
        {
            "--cc-app-bg: #f7f4f0", "--cc-surface: #fffdfb", "--cc-primary: #70482f",
            "--cc-primary-hover: #3d2418", "--cc-accent: #a97750", "--cc-text-primary: #201812",
            "--cc-text-secondary: #66584f", "--cc-success: #2f6f5e", "--cc-warning: #99623b",
            "--cc-danger: #991b1b"
        })
        {
            Assert.Contains(token, css);
        }
    }

    [Fact]
    public void NoLegacyOrangeIndigoBlueLeak_InTargetViews()
    {
        var source = ReadTargets();
        var legacyColors = new[] { "#f97316", "#ea580c", "#ff7a00", "#4f46e5", "#4338ca", "#2563eb", "#0d6efd", "#0dcaf0" };

        foreach (var color in legacyColors)
        {
            Assert.DoesNotContain(color, source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void NoEmptyDangerAlert()
    {
        var views = ReadTargetViews();
        Assert.DoesNotMatch(new Regex("<div[^>]*class=\"alert alert-danger\"[^>]*>\\s*</div>", RegexOptions.IgnoreCase),
            Regex.Replace(views, "@if \\(!ViewData\\.ModelState\\.IsValid\\)\\s*\\{\\s*<div asp-validation-summary=\"ModelOnly\"[^>]*>\\s*</div>\\s*\\}", string.Empty));
        Assert.All(UnitConversionViews.Skip(1).Select(Read), view => Assert.Contains("@if (!ViewData.ModelState.IsValid)", view));
    }

    [Fact]
    public void UnitConversion_UsesCafeChainTokens()
    {
        Assert.All(UnitConversionViews.Select(Read), view => Assert.Contains("cc-page", view));
        Assert.Contains("var(--cc-primary)", Read("CafeChain/wwwroot/css/unit-conversion.css"));
    }

    [Fact]
    public void StoreInventory_UsesCafeChainTokens()
    {
        Assert.Contains("cc-page", Read("CafeChain/Areas/Admin/Views/AdminStoreInventory/Index.cshtml"));
        Assert.Contains("cc-modal", Read("CafeChain/Areas/Admin/Views/AdminStoreInventory/Partials/_TransactionModalPartial.cshtml"));
        Assert.Contains("var(--cc-primary)", Read("CafeChain/wwwroot/css/Admin/StoreInventory/storeinventory.css"));
    }

    [Fact]
    public void ReorderSuggestions_UsesCafeChainTokens()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminReorderSuggestions/Index.cshtml");
        Assert.Contains("cc-page", view);
        Assert.Contains("cc-empty-state", view);
        Assert.Contains("var(--cc-primary)", Read("CafeChain/wwwroot/css/Admin/Procurement/reorder-suggestions.css"));
    }

    [Fact]
    public void Notifications_UsesSemanticTokens()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminNotifications/Index.cshtml");
        Assert.Contains("cc-status-badge", view);
        Assert.Contains("ops-notice-success", view);
        Assert.Contains("ops-notice-error", view);
        Assert.DoesNotContain("list-group-item-warning", view);
    }

    [Fact]
    public void RequestDetail_HidesZeroAllocationCardsByDefault()
    {
        var view = Read(RequestDetailPath);
        Assert.Contains("@if (transferAllocated > 0)", view);
        Assert.Contains("@if (purchaseAllocated > 0)", view);
        Assert.Contains("@if (productionAllocated > 0)", view);
        Assert.Contains("@if (rejectedAllocated > 0)", view);
    }

    [Fact]
    public void RequestDetail_CanExpandAllocationDetails()
    {
        var view = Read(RequestDetailPath);
        Assert.Contains("<details class=\"ops-disclosure\">", view);
        Assert.Contains("Xem chi tiết phân bổ", view);
    }

    [Fact]
    public void SourcingOptions_ShowWhenRemainingPositive()
    {
        var view = Read(RequestDetailPath);
        Assert.Contains("remainingSource <= 0", view);
        Assert.Contains("data-source-option=\"TRANSFER\"", view);
        Assert.Contains("data-source-option=\"PURCHASE\"", view);
        Assert.Contains("data-source-option=\"PRODUCTION\"", view);
    }

    [Fact]
    public void SelectingOneSource_OpensOnlySelectedForm()
    {
        var view = Read(RequestDetailPath);
        Assert.Contains("data-source-form hidden", view);
        Assert.Contains("sourceForm.hidden = false", view);
        Assert.Contains("has-selection", view);
    }

    [Fact]
    public void FullAllocation_HidesOtherSourceForms()
    {
        var view = Read(RequestDetailPath);
        Assert.Contains("@if (remainingSource <= 0)", view);
        Assert.Contains("Đã xác định nguồn cung", view);
    }

    [Fact]
    public void PartialAllocation_KeepsRemainingSourceOptions()
    {
        var view = Read(RequestDetailPath);
        Assert.Contains("Còn @AdminStatusDisplay.Quantity(remainingSource)", view);
        Assert.Contains("chia nhu cầu cho nhiều nguồn", view);
    }

    [Fact]
    public void ReleasedAllocation_ReopensSourceOptions()
    {
        Assert.Contains("Nếu một nguồn bị hủy hoặc giải phóng, các lựa chọn nguồn sẽ tự xuất hiện lại", Read(RequestDetailPath));
    }

    [Fact]
    public void WorkflowStepper_RemainsVisible()
    {
        var view = Read(RequestDetailPath);
        Assert.Contains("ops-workflow", view);
        Assert.Contains("Model.WorkflowSteps", view);
        Assert.Contains("aria-current", view);
    }

    [Fact]
    public void ActionPanel_HasSinglePrimaryCta()
    {
        var view = Read(RequestDetailPath);
        Assert.Contains("ops-panel ops-next-action", view);
        Assert.Contains("Tiếp nhận xử lý", view);
        Assert.Contains("ops-btn ops-btn-primary w-100", view);
    }

    [Fact]
    public void CancelAction_IsCollapsedDangerZone()
    {
        var view = Read(RequestDetailPath);
        Assert.Contains("<details class=\"ops-danger-disclosure\">", view);
        Assert.Contains("Hủy hoặc từ chối yêu cầu", view);
        Assert.Contains("ops-btn ops-btn-danger", view);
    }

    [Fact]
    public void CreatePa_SaveDisabledWithoutSelection()
    {
        var view = Read(CreatePaPath);
        Assert.Contains("disabled=\"@(!hasInitialValidSelection)\"", view);
        Assert.Contains("rows.some(rowIsValid)", view);
    }

    [Fact]
    public void CreatePa_PreventsDoubleSubmit()
    {
        var view = Read(CreatePaPath);
        Assert.Contains("let submitting = false", view);
        Assert.Contains("if (submitting || !rows.some(rowIsValid))", view);
        Assert.Contains("Đang lưu...", view);
    }

    [Fact]
    public void ProcurementViews_UseVietnameseLabels()
    {
        var views = ReadTargetViews();
        foreach (var label in new[] { "Yêu cầu nhập hàng", "Xét nguồn cung", "Đề nghị mua", "Đơn đặt hàng", "Nhận hàng" })
        {
            Assert.Contains(label, views);
        }

        Assert.DoesNotContain("(read-only)", views);
        Assert.DoesNotContain("package-like", views, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoHorizontalPageOverflow_1366x768()
    {
        var css = Read(TokensPath);
        Assert.Contains("@media (max-width: 1366px)", css);
        Assert.Contains("overflow-x: auto", css);
    }

    [Fact]
    public void NoHorizontalPageOverflow_1280x800()
    {
        var css = Read(TokensPath);
        Assert.Contains("@media (max-width: 1280px)", css);
        Assert.Contains("max-width: 100%", css);
    }

    [Fact]
    public void KeyboardFocus_IsVisible()
    {
        var css = Read(TokensPath);
        Assert.Contains(":focus-visible", css);
        Assert.Contains("outline: 3px solid var(--cc-focus)", css);
    }

    [Fact]
    public void ReducedMotion_IsRespected()
    {
        var css = Read(TokensPath);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css);
        Assert.Contains("animation-duration: .01ms !important", css);
    }

    private static readonly string[] UnitConversionViews =
    {
        "CafeChain/Areas/Admin/Views/AdminUnitConversion/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminUnitConversion/Create.cshtml",
        "CafeChain/Areas/Admin/Views/AdminUnitConversion/Edit.cshtml"
    };

    private static readonly string[] TargetViewDirectories =
    {
        "AdminUnitConversion", "AdminStoreInventory", "AdminReorderSuggestions", "AdminNotifications",
        "AdminInventoryThresholds", "AdminStockAlerts", "AdminRestockRequests", "AdminPurchaseAdvices",
        "AdminPurchaseAdviceConsolidation", "AdminPurchaseOrderBatches", "AdminPurchaseOrders",
        "AdminSupplierQuality", "AdminBranchReceipts", "AdminIngredient"
    };

    private static readonly string[] TargetCssPaths =
    {
        TokensPath,
        "CafeChain/wwwroot/css/unit-conversion.css",
        "CafeChain/wwwroot/css/Admin/StoreInventory/storeinventory.css",
        "CafeChain/wwwroot/css/Admin/Procurement/reorder-suggestions.css",
        "CafeChain/wwwroot/css/Admin/InventoryOperations/inventory-operations.css",
        "CafeChain/wwwroot/css/Admin/PurchaseAdvice/purchase-advice.css",
        "CafeChain/wwwroot/css/Admin/Ingredient/ingredient.css",
        "CafeChain/wwwroot/css/admin-white-orange-forms.css"
    };

    private static string ReadTargets() => ReadTargetViews() + string.Join('\n', TargetCssPaths.Select(Read));

    private static string ReadTargetViews()
    {
        var root = ProjectRoot();
        return string.Join('\n', TargetViewDirectories
            .SelectMany(directory => Directory.GetFiles(Path.Combine(root, "CafeChain", "Areas", "Admin", "Views", directory), "*.cshtml", SearchOption.AllDirectories))
            .Select(File.ReadAllText));
    }

    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(ProjectRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string ProjectRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
}
