using System.Reflection;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.Areas.Admin.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Tests;

public sealed class AdminCatalogUiAndAuthorizationRegressionTests
{
    [Fact]
    public void Every_concrete_admin_controller_has_an_authorization_barrier()
    {
        var controllers = typeof(AdminBaseController).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false }
                && typeof(Controller).IsAssignableFrom(type)
                && type.Namespace == "CafeChain.Areas.Admin.Controllers")
            .ToList();

        Assert.NotEmpty(controllers);
        foreach (var controller in controllers)
        {
            Assert.True(
                controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any(),
                $"{controller.Name} must reject direct URL access without an effective Admin permission.");
        }
    }

    [Fact]
    public void Admin_panel_policy_is_backed_by_permission_service()
    {
        var authorization = Read("CafeChain", "Application", "Authorization", "PermissionRequirement.cs");
        var registration = Read("CafeChain", "Extensions", "Services", "AuthorizationServiceExtensions.cs");

        Assert.Contains("AdminPanelAccessAuthorizationHandler", authorization, StringComparison.Ordinal);
        Assert.Contains("GetEffectivePermissionCodesAsync", authorization, StringComparison.Ordinal);
        Assert.Contains("new AdminPanelAccessRequirement()", registration, StringComparison.Ordinal);
        Assert.DoesNotContain("policy.RequireRole(", registration, StringComparison.Ordinal);
    }

    [Fact]
    public void Size_and_topping_lists_support_search_pagination_and_long_names()
    {
        var sizeView = Read("CafeChain", "Areas", "Admin", "Views", "AdminSize", "Index.cshtml");
        var toppingView = Read("CafeChain", "Areas", "Admin", "Views", "AdminTopping", "Index.cshtml");
        var sizeController = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminSizeController.cs");
        var toppingController = Read("CafeChain", "Areas", "Admin", "Controllers", "AdminToppingController.cs");
        var sizeCss = Read("CafeChain", "wwwroot", "css", "Admin", "Size", "size.css");
        var toolbarCss = Read("CafeChain", "wwwroot", "css", "Admin", "Catalog", "catalog-list-toolbar.css");
        var pagination = Read("CafeChain", "Areas", "Admin", "Views", "Shared", "_CatalogPagination.cshtml");

        Assert.Contains("name=\"keyword\"", sizeView, StringComparison.Ordinal);
        Assert.Contains("name=\"keyword\"", toppingView, StringComparison.Ordinal);
        Assert.Contains("class=\"catalog-list-tools\"", sizeView, StringComparison.Ordinal);
        Assert.Contains("class=\"catalog-list-tools\"", toppingView, StringComparison.Ordinal);
        Assert.Contains("name=\"pageSize\"", sizeView, StringComparison.Ordinal);
        Assert.Contains("name=\"pageSize\"", toppingView, StringComparison.Ordinal);
        Assert.Contains("name=\"active\"", sizeView, StringComparison.Ordinal);
        Assert.Contains("name=\"active\"", toppingView, StringComparison.Ordinal);
        Assert.Contains("Đang hoạt động", sizeView, StringComparison.Ordinal);
        Assert.Contains("Ngừng hoạt động", sizeView, StringComparison.Ordinal);
        Assert.Contains("Đang hoạt động", toppingView, StringComparison.Ordinal);
        Assert.Contains("Ngừng hoạt động", toppingView, StringComparison.Ordinal);
        Assert.Contains("bool? active", sizeController, StringComparison.Ordinal);
        Assert.Contains("size.Active == active.Value", sizeController, StringComparison.Ordinal);
        Assert.Contains("bool? active", toppingController, StringComparison.Ordinal);
        Assert.Contains("topping.Active == active.Value", toppingController, StringComparison.Ordinal);
        Assert.DoesNotContain("admin-list-search", sizeView, StringComparison.Ordinal);
        Assert.DoesNotContain("admin-list-search", toppingView, StringComparison.Ordinal);
        Assert.Contains(".catalog-search-box", toolbarCss, StringComparison.Ordinal);
        Assert.Contains("::-webkit-search-cancel-button", toolbarCss, StringComparison.Ordinal);
        Assert.Contains("::-ms-clear", toolbarCss, StringComparison.Ordinal);
        Assert.Contains(".catalog-page-size", toolbarCss, StringComparison.Ordinal);
        Assert.Contains("_CatalogPagination", sizeView, StringComparison.Ordinal);
        Assert.Contains("_CatalogPagination", toppingView, StringComparison.Ordinal);
        Assert.Contains("asp-route-keyword", pagination, StringComparison.Ordinal);
        Assert.Contains("asp-route-active", pagination, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: anywhere", sizeCss, StringComparison.Ordinal);
        Assert.DoesNotContain("text-overflow: ellipsis", CssRule(sizeCss, ".size-badge"), StringComparison.Ordinal);
    }

    [Fact]
    public void Negative_reason_ai_cancellation_and_category_sweet_alert_are_preserved()
    {
        var inventory = Read("CafeChain", "wwwroot", "js", "Admin", "InventoryDocument", "inventorydocumentcreate.js");
        var dashboard = Read("CafeChain", "wwwroot", "js", "Admin", "Dashboard", "dashboard.js");
        var intelligence = Read("CafeChain", "wwwroot", "js", "Admin", "Dashboard", "dashboard-intelligence.js");
        var category = Read("CafeChain", "wwwroot", "js", "Admin", "Category", "Category.js");
        var categoryView = Read("CafeChain", "Areas", "Admin", "Views", "AdminCategory", "Index.cshtml");

        Assert.Contains("reasonInput.disabled = !requestMode", inventory, StringComparison.Ordinal);
        Assert.Contains("cafechain:dashboard-ai-busy-changed", dashboard, StringComparison.Ordinal);
        Assert.Contains("isAiBusy || isApplyingContext", dashboard, StringComparison.Ordinal);
        Assert.Contains("cafechain:dashboard-ai-busy-changed", intelligence, StringComparison.Ordinal);
        Assert.Contains("showCategoryAlert", category, StringComparison.Ordinal);
        Assert.Contains("window.Swal.fire", category, StringComparison.Ordinal);
        Assert.Contains("await hideCategoryModal(\"createCategoryModal\")", category, StringComparison.Ordinal);
        Assert.Contains("await hideCategoryModal(\"editCategoryModal\")", category, StringComparison.Ordinal);
        Assert.Contains("resetCategoryForm(createForm", category, StringComparison.Ordinal);
        Assert.Contains("resetCategoryForm(editForm", category, StringComparison.Ordinal);
        Assert.Contains("delete form.dataset.submitting", category, StringComparison.Ordinal);
        Assert.Contains("AdminMutationGuard?.unlockForm", category, StringComparison.Ordinal);
        Assert.Contains("preserveCategoryForm", category, StringComparison.Ordinal);
        Assert.Contains("await hideCategoryModal(suspendedModal.id)", category, StringComparison.Ordinal);
        Assert.Contains("await shown", category, StringComparison.Ordinal);
        Assert.Contains("Category.js\" asp-append-version=\"true\"", categoryView, StringComparison.Ordinal);
        Assert.Contains("setCategorySubmitState", category, StringComparison.Ordinal);
        Assert.Contains("finally", category, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_dashboard_widget_has_constant_seed_and_frontend_authorization_mapping()
    {
        var widgets = Enum.GetValues<DashboardAnalyticsWidget>();
        var constants = typeof(PermissionConstants)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, FieldType: not null }
                && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToHashSet(StringComparer.Ordinal);
        var seed = Read("CafeChain", "Scripts", "SeedAll.sql");
        var dashboard = Read("CafeChain", "wwwroot", "js", "Admin", "Dashboard", "dashboard.js");

        Assert.Equal(39, widgets.Length);
        Assert.Contains("N'DASHBOARD_WIDGET',N'Widget Dashboard',9,1", seed, StringComparison.Ordinal);
        foreach (var widget in widgets)
        {
            var code = $"Dashboard.Widget.{widget}.View";
            Assert.Contains(code, constants);
            Assert.Contains($"N'{code}'", seed, StringComparison.Ordinal);
            Assert.Contains($"\"{widget}\"", dashboard, StringComparison.Ordinal);
        }

        Assert.Contains("authorizedSectionWidgets", dashboard, StringComparison.Ordinal);
        Assert.Contains("allowedWidgetKeys.has(widget.authorizationKey)", dashboard, StringComparison.Ordinal);
        Assert.Contains("scheduledStaff: \"WorkforceHourlyDemand\"", dashboard, StringComparison.Ordinal);
    }

    private static string CssRule(string css, string selector)
    {
        var start = css.IndexOf(selector, StringComparison.Ordinal);
        if (start < 0) return string.Empty;
        var end = css.IndexOf('}', start);
        return end < 0 ? css[start..] : css[start..(end + 1)];
    }

    private static string Read(params string[] segments) =>
        File.ReadAllText(Path.Combine([FindRepoRoot(), .. segments]));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "CafeChain")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
