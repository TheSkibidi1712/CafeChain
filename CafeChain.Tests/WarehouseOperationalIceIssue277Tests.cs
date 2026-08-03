using Xunit;

namespace CafeChain.Tests;

public sealed class WarehouseOperationalIceIssue277Tests
{
    private static readonly string IndexView = Read(
        "CafeChain/Areas/Admin/Views/AdminOperationalIce/Index.cshtml");
    private static readonly string DetailView = Read(
        "CafeChain/Areas/Admin/Views/AdminOperationalIce/Details.cshtml");
    private static readonly string ReportView = Read(
        "CafeChain/Areas/Admin/Views/AdminOperationalIce/Report.cshtml");
    private static readonly string Styles = Read(
        "CafeChain/wwwroot/css/Admin/OperationalIce/operational-ice.css");

    [Fact]
    public void OperationalIcePages_UseWarehouseVisualFoundation()
    {
        Assert.Contains("ice-shell cc-warehouse-page", IndexView);
        Assert.Contains("ice-shell cc-warehouse-page", DetailView);
        Assert.Contains("ice-report-shell cc-warehouse-page", ReportView);
        Assert.Contains("cc-warehouse-header", IndexView);
        Assert.Contains("cc-warehouse-summary-grid", DetailView);
        Assert.Contains("cc-warehouse-table-shell", ReportView);
    }

    [Fact]
    public void PolicySections_AndNumbers_AreHumanReadable()
    {
        Assert.Contains("Nguyên liệu và đơn vị", IndexView);
        Assert.Contains("Định mức", IndexView);
        Assert.Contains("Quy tắc duyệt và bàn giao", IndexView);
        Assert.Contains("value.ToString(\"0.##\", viCulture)", IndexView);
        Assert.Contains("value.ToString(\"0.##\", System.Globalization.CultureInfo.InvariantCulture)", IndexView);
        Assert.DoesNotContain("ToString(\"0.000\")", IndexView);
        Assert.DoesNotContain("ToString(\"0.0000\")", IndexView);
    }

    [Fact]
    public void CreateModes_AndScheduleStates_AreVisuallyDistinct()
    {
        Assert.Contains("class=\"ice-mode-switch", IndexView);
        Assert.Contains("Tạo từ lịch làm việc", IndexView);
        Assert.Contains("Tạo thủ công", IndexView);
        Assert.Contains("scheduleLoadingState", IndexView);
        Assert.Contains("scheduleHasDataState", IndexView);
        Assert.Contains("scheduleEmptyState", IndexView);
        Assert.Contains("scheduleErrorState", IndexView);
        Assert.Contains(".ice-mode-switch label:has(input:checked)", Styles);
    }

    [Fact]
    public void ShiftList_ShowsOperationalSummaryAndNextAction()
    {
        Assert.Contains("<th>Ca vận hành</th>", IndexView);
        Assert.Contains("<th>Khung giờ</th>", IndexView);
        Assert.Contains("Định mức", IndexView);
        Assert.Contains("Đã cấp", IndexView);
        Assert.Contains("Lý thuyết", IndexView);
        Assert.Contains("Chênh lệch", IndexView);
        Assert.Contains("Đã liên kết @row.LinkedWorkShiftCount ca POS", IndexView);
        Assert.Contains("asp-action=\"OpenAllocation\"", IndexView);
    }

    [Fact]
    public void Detail_ShowsWorkflowWorkShiftStatesAndValidActions()
    {
        Assert.Contains("aria-label=\"Tiến trình ca vận hành đá\"", DetailView);
        Assert.Contains("WorkShift POS", DetailView);
        Assert.Contains("Chưa liên kết WorkShift POS.", DetailView);
        Assert.Contains("isOpen && Model.CanSubmitClose", DetailView);
        Assert.Contains("isPendingApproval && Model.CanApproveVariance", DetailView);
        Assert.Contains("needsReconciliation && Model.CanApproveVariance", DetailView);
        Assert.Contains("asp-action=\"CloseAllocation\"", DetailView);
        Assert.Contains("asp-action=\"ApproveVariance\"", DetailView);
        Assert.Contains("asp-action=\"ReconcileVariance\"", DetailView);
    }

    [Fact]
    public void EmptyStates_AreAccessible()
    {
        Assert.Contains("cc-warehouse-empty\" role=\"status", IndexView);
        Assert.Contains("cc-warehouse-empty\" role=\"status", DetailView);
        Assert.Contains("cc-warehouse-empty\" role=\"status", ReportView);
    }

    [Fact]
    public void OperationalIce_UsesSharedTokensAndTabletResponsiveGrids()
    {
        Assert.Contains("var(--cc-app-bg", Styles);
        Assert.Contains("var(--cc-primary", Styles);
        Assert.Contains("@media (max-width: 1180px)", Styles);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr))", Styles);
        Assert.Contains(".ice-table-scroll", Styles);
        Assert.Contains("overflow-x: auto", Styles);
        Assert.DoesNotContain("var(--ice-coffee)", Styles);
        Assert.DoesNotContain("var(--ice-caramel)", Styles);
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null
               && !File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.slnx")))
            directory = directory.Parent;

        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
