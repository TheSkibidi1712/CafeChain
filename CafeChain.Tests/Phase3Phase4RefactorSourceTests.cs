namespace CafeChain.Tests;

public sealed class Phase3Phase4RefactorSourceTests
{
    [Fact]
    public void Inventory_document_uses_shared_conversion_and_typed_price_semantics()
    {
        var service = Read("CafeChain", "Application", "Services", "Admin", "InventoryDocuments", "AdminInventoryDocumentCreateService.cs");
        var dto = Read("CafeChain", "Application", "DTOs", "Admin", "InventoryDocuments", "Create", "SupplierIngredientDTO.cs");
        var client = Read("CafeChain", "wwwroot", "js", "Admin", "InventoryDocument", "inventorydocumentcreate.js");

        Assert.Contains("_unitConversionService.ConvertAsync", service, StringComparison.Ordinal);
        Assert.Contains("InventoryPriceSemantics.BaseUnitCost", service, StringComparison.Ordinal);
        Assert.Contains("BASE_UNIT_COST", dto, StringComparison.Ordinal);
        Assert.Contains("userMessage", client, StringComparison.Ordinal);
        Assert.Contains("displayAvailable", client, StringComparison.Ordinal);
    }

    [Fact]
    public void Application_permissions_protect_launcher_and_destinations()
    {
        var policies = Read("CafeChain", "Extensions", "Services", "AuthorizationServiceExtensions.cs");
        var policyConstants = Read("CafeChain", "Application", "Constants", "AuthorizationPolicyConstants.cs");
        var dashboard = Read("CafeChain", "Areas", "Admin", "Controllers", "DashboardController.cs");
        var staffHub = Read("CafeChain", "Controllers", "StaffHubController.cs");
        var pos = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminPOSController.cs");
        var launcher = Read("CafeChain", "Application", "Services", "AppLauncher", "AppLauncherService.cs");

        Assert.Contains("AdminDashboardApp", policies, StringComparison.Ordinal);
        Assert.Contains("StaffHubApp", policies, StringComparison.Ordinal);
        Assert.Contains("PosApp", policies, StringComparison.Ordinal);
        Assert.Contains("RequireAdminDashboardApp", policyConstants, StringComparison.Ordinal);
        Assert.Contains("RequireStaffHubApp", policyConstants, StringComparison.Ordinal);
        Assert.Contains("RequirePosApp", policyConstants, StringComparison.Ordinal);
        Assert.Contains("AdminDashboardApp", dashboard, StringComparison.Ordinal);
        Assert.Contains("StaffHubApp", staffHub, StringComparison.Ordinal);
        Assert.Contains("PosApp", pos, StringComparison.Ordinal);
        Assert.Contains("HasPermissionAsync", launcher, StringComparison.Ordinal);
    }

    [Fact]
    public void Login_redirect_preserves_local_return_url_and_routes_staff_to_launcher()
    {
        var controller = Read("CafeChain", "Controllers", "AccountController.cs");
        var accountService = Read("CafeChain", "Application", "Services", "Accounts", "AccountService.cs");

        Assert.Contains("Url.IsLocalUrl(returnUrl)", controller, StringComparison.Ordinal);
        Assert.Contains("login.StaffId.HasValue", controller, StringComparison.Ordinal);
        Assert.Contains("RedirectToAction(\"Index\", \"AppLauncher\")", controller, StringComparison.Ordinal);
        Assert.Contains("rolePriority", accountService, StringComparison.Ordinal);
        Assert.DoesNotContain("r.Contains(\"Admin\")", accountService, StringComparison.Ordinal);
    }

    // [Fact]
    // public void Permission_migration_is_data_only_and_idempotent()
    // {
    //     var migration = Read("CafeChain", "Migrations", "20260717193000_AddApplicationPermissions.cs");

    //     Assert.Contains("App.AdminDashboard", migration, StringComparison.Ordinal);
    //     Assert.Contains("App.StaffHub", migration, StringComparison.Ordinal);
    //     Assert.Contains("App.POS", migration, StringComparison.Ordinal);
    //     Assert.Contains("NOT EXISTS", migration, StringComparison.OrdinalIgnoreCase);
    //     Assert.DoesNotContain("CreateTable", migration, StringComparison.Ordinal);
    //     Assert.DoesNotContain("AddColumn", migration, StringComparison.Ordinal);
    // }

    [Fact]
    public void Dashboard_guide_exists_in_markdown_and_admin_view()
    {
        var markdown = Read("CafeChain", "docs", "user-guides", "dashboard-analytics.md");
        var view = Read("CafeChain", "Areas", "Admin", "Views", "Dashboard", "Guide.cshtml");
        var controller = Read("CafeChain", "Areas", "Admin", "Controllers", "DashboardController.cs");

        foreach (var term in new[] { "Điều hành", "POS / WorkShift", "Kho", "Mua hàng", "Sản phẩm", "Nhân sự" })
        {
            Assert.Contains(term, markdown, StringComparison.Ordinal);
            Assert.Contains(term, view, StringComparison.Ordinal);
        }
        Assert.Contains("IActionResult Guide", controller, StringComparison.Ordinal);
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
