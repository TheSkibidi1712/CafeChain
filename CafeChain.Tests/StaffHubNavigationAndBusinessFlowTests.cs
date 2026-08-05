using System.Text.Json;

namespace CafeChain.Tests;

public sealed class StaffHubNavigationAndBusinessFlowTests
{
    [Fact]
    public void Frontend_uses_patched_router_without_changing_import_contract()
    {
        using var package = JsonDocument.Parse(Read("CafeChain.Frontend", "package.json"));
        var routerDependency = package.RootElement
            .GetProperty("dependencies")
            .GetProperty("react-router-dom")
            .GetString();
        Assert.Equal("npm:react-router@8.3.0", routerDependency);

        using var packageLock = JsonDocument.Parse(Read("CafeChain.Frontend", "package-lock.json"));
        var routerPackage = packageLock.RootElement
            .GetProperty("packages")
            .GetProperty("node_modules/react-router-dom");
        Assert.Equal("react-router", routerPackage.GetProperty("name").GetString());
        Assert.Equal("8.3.0", routerPackage.GetProperty("version").GetString());
    }

    [Fact]
    public void StaffHub_navigation_uses_authorization_and_named_routes()
    {
        var controller = Read("CafeChain", "Controllers", "StaffHubController.cs");
        var view = Read("CafeChain", "Views", "StaffHub", "Index.cshtml");
        var layout = Read("CafeChain", "Views", "Shared", "_Layout.cshtml");
        var launcher = Read("CafeChain", "Application", "Services", "AppLauncher", "AppLauncherService.cs");

        Assert.Contains("AuthorizationPolicyConstants.AdminDashboardApp", controller, StringComparison.Ordinal);
        Assert.Contains("ViewBag.CanAccessDashboard", controller, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"AppLauncher\"", view, StringComparison.Ordinal);
        Assert.Contains("asp-area=\"Admin\"", view, StringComparison.Ordinal);
        Assert.Contains("asp-controller=\"Dashboard\"", view, StringComparison.Ordinal);
        Assert.Contains("@if (canAccessDashboard)", view, StringComparison.Ordinal);
        Assert.Contains("@if (canAccessPos)", view, StringComparison.Ordinal);
        Assert.Contains("currentController != \"StaffHub\"", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("Chấm công, theo dõi ca", launcher, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StaffHub_open_pos_uses_read_only_preview_before_issuing_exchange_code()
    {
        var controller = Read("CafeChain", "Controllers", "StaffHubController.cs");
        var view = Read("CafeChain", "Views", "StaffHub", "Index.cshtml");
        var script = Read("CafeChain", "wwwroot", "js", "StaffHub", "staffhub-schedule.js");

        Assert.Contains("PreviewOpenPos", controller, StringComparison.Ordinal);
        Assert.Contains("AssessOpenContextAsync", controller, StringComparison.Ordinal);
        Assert.Contains("ValidateAntiForgeryToken", controller, StringComparison.Ordinal);
        Assert.Contains("data-preview-pos-url", view, StringComparison.Ordinal);
        Assert.Contains("openPosPreviewDialog", view, StringComparison.Ordinal);
        Assert.Contains("Terminal, lý do và OTP (nếu cần) được xác nhận tại StaffHub", view, StringComparison.Ordinal);
        Assert.Contains("POS chỉ yêu cầu nhập tiền đầu phiên", view, StringComparison.Ordinal);
        Assert.Contains("root.dataset.previewPosUrl", script, StringComparison.Ordinal);
        Assert.Contains("WITHIN_SCHEDULE", script, StringComparison.Ordinal);
        Assert.Contains("redirectWithTicket", script, StringComparison.Ordinal);
    }

    [Fact]
    public void StaffHub_business_flow_document_covers_required_decisions_and_states()
    {
        var document = Read("CafeChain", "Doc", "STAFFHUB_USER_BUSINESS_FLOWS.md");
        foreach (var required in new[]
                 {
                     "Chưa có lịch — Thời gian nghỉ hoặc chưa được phân ca.",
                     "WITHIN_SCHEDULE",
                     "LATE_FOR_SCHEDULE",
                     "OUTSIDE_SCHEDULE",
                     "EXPIRED_PENDING_CLOSE",
                     "RECONCILIATION_REQUIRED",
                     "AutoCloseAtUtc = StartTimeUtc + 6 giờ",
                     "TERMINAL_NOT_FOUND",
                     "DUPLICATE_REQUEST",
                     "CONCURRENCY_CONFLICT",
                     "AppLauncher",
                     "AdminDashboardApp"
                 })
        {
            Assert.Contains(required, document, StringComparison.Ordinal);
        }

        Assert.Contains("STAFFHUB_USER_BUSINESS_FLOWS.md",
            Read("CafeChain", "Doc", "STAFFHUB_POS_WORKSHIFT_BUSINESS_RULES.md"),
            StringComparison.Ordinal);
        Assert.Contains("STAFFHUB_USER_BUSINESS_FLOWS.md",
            Read("CafeChain", "Doc", "STAFFHUB_POS_WORKSHIFT_REFACTOR_GUIDE.md"),
            StringComparison.Ordinal);
    }

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([FindRepoRoot(), .. path]));

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
