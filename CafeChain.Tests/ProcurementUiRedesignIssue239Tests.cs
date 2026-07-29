using System.Text.RegularExpressions;
using Xunit;

namespace CafeChain.Tests;

public sealed class ProcurementUiRedesignIssue239Tests
{
    private const string RequestDetailPath = "CafeChain/Areas/Admin/Views/AdminRestockRequests/Details.cshtml";
    private const string CreatePaPath = "CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Create.cshtml";
    private const string TokensPath = "CafeChain/wwwroot/css/Admin/Procurement/procurement-design-system.css";

    [Fact]
    public void DesignTokens_AreCentralized()
    {
        var css = Read(TokensPath);

        Assert.Contains("--cc-app-bg: #f7f3ee", css);
        Assert.Contains("--cc-surface: #ffffff", css);
        Assert.Contains("--cc-primary: #6f4e37", css);
        Assert.Contains("--cc-primary-hover: #5c3f2b", css);
        Assert.Contains("--cc-accent: #c67a45", css);
        Assert.Contains("--cc-text-primary: #1f2937", css);
        Assert.Contains("--cc-text-secondary: #475569", css);
        Assert.Contains("--cc-success: #2f6f5e", css);
        Assert.Contains("--cc-warning: #99623b", css);
        Assert.Contains("--cc-danger: #991b1b", css);
    }

    [Fact]
    public void NoHardcodedPaletteLeak_InProcurementViews()
    {
        var views = ReadProcurementViews();

        Assert.DoesNotMatch(new Regex("#[0-9a-fA-F]{6}(?![0-9a-fA-F])"), views);
    }

    [Fact]
    public void RequestDetail_HidesZeroAllocationCardsByDefault()
    {
        var view = Read(RequestDetailPath);

        Assert.Contains("ops-summary-metrics", view);
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

        Assert.Contains("else if (!canWarehouse)", view);
        Assert.Contains("data-source-option=\"TRANSFER\"", view);
        Assert.Contains("data-source-option=\"PURCHASE\"", view);
        Assert.Contains("data-source-option=\"PRODUCTION\"", view);
    }

    [Fact]
    public void SelectingOneSource_OpensOnlySelectedForm()
    {
        var view = Read(RequestDetailPath);
        var css = Read(TokensPath);

        Assert.Contains("data-source-form hidden", view);
        Assert.Contains("sourceForm.hidden = false", view);
        Assert.Contains("sourceOptionGroup?.classList.add('has-selection')", view);
        Assert.Contains(".ops-source-options.has-selection .ops-source-option:not(.is-active)", css);
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

        Assert.Contains("var hasAllocationDetail", view);
        Assert.Contains("Còn @AdminStatusDisplay.Quantity(remainingSource)", view);
        Assert.Contains("Bạn vẫn có thể chia nhu cầu cho nhiều nguồn", view);
    }

    [Fact]
    public void ReleasedAllocation_ReopensSourceOptions()
    {
        var view = Read(RequestDetailPath);

        Assert.Contains("Nếu một nguồn bị hủy hoặc giải phóng, các lựa chọn nguồn sẽ tự xuất hiện lại", view);
    }

    [Fact]
    public void WorkflowStepper_RemainsVisible()
    {
        var view = Read(RequestDetailPath);

        Assert.Contains("ops-workflow", view);
        Assert.Contains("aria-current", view);
        Assert.Contains("Model.WorkflowSteps", view);
    }

    [Fact]
    public void RequestActionCard_HasPrimaryNextAction()
    {
        var view = Read(RequestDetailPath);

        Assert.Contains("ops-panel ops-next-action", view);
        Assert.Contains("Tiếp nhận xử lý", view);
        Assert.Contains("ops-btn ops-btn-primary w-100", view);
    }

    [Fact]
    public void CancelAction_IsCollapsedDangerSection()
    {
        var view = Read(RequestDetailPath);

        Assert.Contains("<details class=\"ops-danger-disclosure\">", view);
        Assert.Contains("Hủy hoặc từ chối yêu cầu", view);
        Assert.Contains("ops-btn ops-btn-danger", view);
    }

    [Fact]
    public void CreatePa_EmptyAlertNotRendered()
    {
        var view = Read(CreatePaPath);

        Assert.Contains("@if (!ViewData.ModelState.IsValid)", view);
        Assert.DoesNotContain("<div class=\"alert alert-danger\"></div>", view);
    }

    [Fact]
    public void CreatePa_EmptyStateHasGuidance()
    {
        var view = Read(CreatePaPath);

        Assert.Contains("pa-create-empty", view);
        Assert.Contains("Chưa có yêu cầu nào sẵn sàng để lập đề nghị mua", view);
        Assert.Contains("Xem danh sách yêu cầu bổ sung", view);
        Assert.Contains("Làm mới", view);
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
        var views = ReadProcurementViews();

        Assert.Contains("Yêu cầu nhập hàng", views);
        Assert.Contains("Xét nguồn cung", views);
        Assert.Contains("Đề nghị mua", views);
        Assert.Contains("Đơn đặt hàng", views);
        Assert.Contains("Nhận hàng", views);
        Assert.DoesNotContain("Gói mua active", views);
        Assert.DoesNotContain("(read-only)", views);
        Assert.DoesNotContain("authority giá vốn", views);
    }

    [Fact]
    public void ProcurementViews_HaveNoHorizontalPageOverflow()
    {
        var css = Read(TokensPath);

        Assert.Contains(".ops-table-scroll", css);
        Assert.Contains("max-width: 100%", css);
        Assert.Contains("overflow-x: auto", css);
    }

    [Fact]
    public void Responsive_1366x768()
    {
        var css = Read(TokensPath);
        Assert.Contains("@media (max-width: 1366px)", css);
    }

    [Fact]
    public void Responsive_1280x800()
    {
        var css = Read(TokensPath);
        Assert.Contains("@media (max-width: 1280px)", css);
    }

    [Fact]
    public void KeyboardFocus_IsVisible()
    {
        var css = Read(TokensPath);

        Assert.Contains(":focus-visible", css);
        Assert.Contains("outline: 3px solid var(--cc-focus)", css);
    }

    private static string ReadProcurementViews() => string.Join('\n', new[]
    {
        "CafeChain/Areas/Admin/Views/AdminRestockRequests/Index.cshtml",
        RequestDetailPath,
        "CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Index.cshtml",
        CreatePaPath,
        "CafeChain/Areas/Admin/Views/AdminPurchaseAdvices/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseAdviceConsolidation/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseOrderBatches/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseOrderBatches/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseOrders/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminPurchaseOrders/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminBranchReceipts/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminBranchReceipts/Details.cshtml",
        "CafeChain/Areas/Admin/Views/AdminSupplier/Index.cshtml",
        "CafeChain/Areas/Admin/Views/AdminUnitConversion/Index.cshtml"
    }.Select(Read));

    private static string Read(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
