using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace CafeChain.Tests;

public sealed class ProductionBomUiRefactorTests
{
    private static readonly string[] PageViews =
    [
        "AdminRecipe/Index.cshtml",
        "AdminRecipe/Create.cshtml",
        "AdminRecipe/Edit.cshtml",
        "AdminRecipe/Visualize.cshtml",
        "AdminRecipe/DataHealth.cshtml",
        "AdminPreparedItem/Index.cshtml",
        "AdminProductionOrder/Index.cshtml",
        "AdminProductionOrder/Create.cshtml",
        "AdminProductionOrder/Details.cshtml",
        "AdminDrinkProfitability/Index.cshtml"
    ];

    [Fact]
    public void ProductionHeader_UsesUnifiedHeroLayout()
    {
        var partial = Read("CafeChain/Areas/Admin/Views/Shared/_PageHero.cshtml");
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains("cc-page-hero", partial, StringComparison.Ordinal);
        Assert.Contains("border-left: 6px solid", css, StringComparison.Ordinal);
        Assert.Contains("cc-page-hero::before", css, StringComparison.Ordinal);
        Assert.Contains("cc-page-hero__actions", css, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionPages_UseConsistentBreadcrumbAndActions()
    {
        foreach (var view in PageViews)
        {
            var source = Read($"CafeChain/Areas/Admin/Views/{view}");
            Assert.Contains("_PageHero", source, StringComparison.Ordinal);
            Assert.Contains("production-bom-ui.css", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ProductionUi_DoesNotRenderRawTechnicalTerms()
    {
        var combined = string.Join('\n', Array.ConvertAll(PageViews, view =>
            Read($"CafeChain/Areas/Admin/Views/{view}")));
        var recipeBuilder = Read("CafeChain/wwwroot/js/Admin/Recipe/bom-builder.js");

        Assert.DoesNotContain("Contract v@", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("<code>@reason.Code", combined, StringComparison.Ordinal);
        Assert.DoesNotContain(">Số lượng mẻ", combined, StringComparison.Ordinal);
        Assert.DoesNotContain(">Lệnh sơ chế độc lập<", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Công thức #", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("Công thức #", recipeBuilder, StringComparison.Ordinal);
        Assert.DoesNotContain("<small>VND", recipeBuilder, StringComparison.Ordinal);
        Assert.Contains("Sơ chế độc lập (quy trình cũ)", combined, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionWorkflow_ShowsPlannedVsActualClearly()
    {
        var details = Read("CafeChain/Areas/Admin/Views/AdminProductionOrder/Details.cshtml");

        Assert.Contains("_LifecycleStepper", details, StringComparison.Ordinal);
        Assert.Contains("Sản lượng dự kiến", details, StringComparison.Ordinal);
        Assert.Contains("Sản lượng thực tế", details, StringComparison.Ordinal);
        Assert.Contains("Đầu vào thực tế", details, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadOnlyRole_DoesNotSeeMutationActions_AndGetsNextRoleGuidance()
    {
        var details = Read("CafeChain/Areas/Admin/Views/AdminProductionOrder/Details.cshtml");

        Assert.Contains("Model.CanRelease", details, StringComparison.Ordinal);
        Assert.Contains("Model.CanRecordActual", details, StringComparison.Ordinal);
        Assert.Contains("_NextActionPanel", details, StringComparison.Ordinal);
        Assert.Contains("Trang đang ở chế độ theo dõi", details, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionHero_ActionsWrapOnTablet_AndStackOnMobile()
    {
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains("@media (max-width: 991.98px)", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 767.98px)", css, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: 1fr", css, StringComparison.Ordinal);
        Assert.Contains("body:has(.production-bom-page) .sidebar", css, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyboardFocus_IsVisible()
    {
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");
        Assert.Contains(":focus-visible", css, StringComparison.Ordinal);
        Assert.Contains("--cc-focus-ring", css, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeDetail_UsesAnchorSections_AndCompactMobileNavigation()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml");
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains("production-section-nav", view, StringComparison.Ordinal);
        Assert.Contains("#bom-components", view, StringComparison.Ordinal);
        Assert.Contains("overflow-x: auto", css, StringComparison.Ordinal);
    }

    [Fact]
    public void DataHealth_IsPaged_AndNotPrimarySidebarItem()
    {
        var layout = Read("CafeChain/Areas/Admin/Views/Shared/_AdminLayout.cshtml");
        var controller = Read("CafeChain/Areas/Admin/Controllers/AdminRecipeController.cs");
        var service = Read("CafeChain/Application/Services/Admin/Recipes/AdminRecipeQueryService.cs");

        Assert.DoesNotContain("asp-action=\"DataHealth\" class=\"@Html.IsActive", layout, StringComparison.Ordinal);
        Assert.Contains("DataHealth(int page = 1)", controller, StringComparison.Ordinal);
        Assert.Contains(".Skip((page - 1) * pageSize)", service, StringComparison.Ordinal);
        Assert.Contains(".Take(pageSize)", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Profitability_RemainsADeepLinkWithoutDuplicateProductionMenu()
    {
        var layout = Read("CafeChain/Areas/Admin/Views/Shared/_AdminLayout.cshtml");
        var productionSection = layout[layout.IndexOf("SẢN XUẤT / BOM", StringComparison.Ordinal)..];

        Assert.DoesNotContain("AdminDrinkProfitability", productionSection, StringComparison.Ordinal);
    }

    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CafeChain.slnx"))
                || File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy thư mục gốc CafeChain.");
    }
}
