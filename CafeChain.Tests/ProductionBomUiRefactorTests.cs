using System;
using System.Collections.Generic;
using System.IO;
using CafeChain.Application.DTOs.Admin.Production;
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
    public void WriterModeNotReady_MapsToVietnameseBusinessMessage()
    {
        var message = ProductionReadinessDisplay.MessageFor(ProductionReadinessCodes.WriterMode);

        Assert.Contains("chưa sẵn sàng cho quy trình sản xuất", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Bán thành phẩm", message, StringComparison.Ordinal);
        Assert.DoesNotContain("WriterMode", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LegacyRecipe", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PreparedItem", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductionWarning_DoesNotRenderRawWriterModeReason()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminProductionOrder/Create.cshtml");

        Assert.DoesNotContain("<code>${escapeHtml(reason.code)}</code>", view, StringComparison.Ordinal);
        Assert.DoesNotContain("return labels[value] || value", view, StringComparison.Ordinal);
        Assert.DoesNotContain("indexOf('LegacyRecipe')", view, StringComparison.Ordinal);
        Assert.DoesNotContain("let html = res.message", view, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeList_UsesFullAvailableTableWidth_AndAlignsActionColumn()
    {
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains(".production-bom-page .production-data-table", css, StringComparison.Ordinal);
        Assert.Contains("width: 100%;", css, StringComparison.Ordinal);
        Assert.Contains(".production-bom-page .rb-recipe-table.is-standard .rb-col-actions", css, StringComparison.Ordinal);
        Assert.Contains(".production-bom-page .rb-actions-cell", css, StringComparison.Ordinal);
        Assert.Contains("text-align: right !important", css, StringComparison.Ordinal);
    }

    [Fact]
    public void PreparedItemList_RightmostColumnUsesRemainingWidth()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminPreparedItem/Index.cshtml");
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains("rb-prepared-recipe-cell", view, StringComparison.Ordinal);
        Assert.Contains(".production-bom-page .rb-prepared-recipe-cell", css, StringComparison.Ordinal);
        Assert.Contains("text-align: right", css, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyProduction_FormGridUsesFullWidth_AndAlignsRightColumn()
    {
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains(".production-bom-page .production-operation-grid", css, StringComparison.Ordinal);
        Assert.Contains(".production-bom-page .production-operation-preview", css, StringComparison.Ordinal);
        Assert.Contains("justify-self: stretch", css, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeForm_SidebarAlignsWithPageRightEdge()
    {
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains(".production-bom-page .production-form-sidebar", css, StringComparison.Ordinal);
        Assert.Contains("justify-self: stretch", css, StringComparison.Ordinal);
        Assert.Contains("width: 100%;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionBomPages_DoNotUseNegativeMarginAlignmentHack()
    {
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.DoesNotContain("margin-right: -", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("margin-left: -", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("width: calc(100% +", css, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecipeTypeToggle_UsesInverseTextOnBrownHoverAndActiveStates()
    {
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains(".production-bom-page .rb-toggle .btn:hover", css, StringComparison.Ordinal);
        Assert.Contains(".production-bom-page .rb-toggle .btn-check:checked + .btn", css, StringComparison.Ordinal);
        Assert.Contains("color: var(--cc-text-inverse", css, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionHero_MatchesStoreInventoryVisualLanguage()
    {
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains("radial-gradient(circle at 92% 4%", css, StringComparison.Ordinal);
        Assert.Contains("linear-gradient(135deg", css, StringComparison.Ordinal);
        Assert.Contains("var(--cc-brown-600", css, StringComparison.Ordinal);
        Assert.Contains("var(--cc-caramel-500", css, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionBomPages_UseUnifiedPageShell()
    {
        foreach (var view in PageViews)
        {
            var source = Read($"CafeChain/Areas/Admin/Views/{view}");
            Assert.Contains("production-bom-page production-bom-shell", source, StringComparison.Ordinal);
        }

        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");
        Assert.Contains("--production-bom-content-gutter", css, StringComparison.Ordinal);
        Assert.Contains(".production-bom-shell", css, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionBomPages_UseUnifiedPageHero()
    {
        foreach (var view in PageViews)
        {
            Assert.Contains("_PageHero", Read($"CafeChain/Areas/Admin/Views/{view}"), StringComparison.Ordinal);
        }

        var partial = Read("CafeChain/Areas/Admin/Views/Shared/_PageHero.cshtml");
        Assert.Contains("cc-page-hero__content", partial, StringComparison.Ordinal);
        Assert.Contains("cc-page-hero__actions", partial, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionList_FilterAndTableShareOuterEdges()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminProductionOrder/Index.cshtml");
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains("production-filter-card", view, StringComparison.Ordinal);
        Assert.Contains("production-list-section", view, StringComparison.Ordinal);
        Assert.Contains("production-table-panel", view, StringComparison.Ordinal);
        Assert.Contains(".production-bom-page .production-filter-card", css, StringComparison.Ordinal);
        Assert.Contains(".production-bom-page .production-table-panel", css, StringComparison.Ordinal);
        Assert.Contains("width: 100%;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeList_FilterAndTableShareOuterEdges()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Index.cshtml");

        Assert.Contains("production-filter-card", view, StringComparison.Ordinal);
        Assert.Contains("production-section-header", view, StringComparison.Ordinal);
        Assert.Contains("production-table-panel", view, StringComparison.Ordinal);
        Assert.Contains("production-filter-reset", view, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionTables_ActionColumnIsConsistent()
    {
        var recipe = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Index.cshtml");
        var prepared = Read("CafeChain/Areas/Admin/Views/AdminPreparedItem/Index.cshtml");
        var production = Read("CafeChain/Areas/Admin/Views/AdminProductionOrder/Index.cshtml");
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains("rb-actions-heading", recipe, StringComparison.Ordinal);
        Assert.Contains("rb-actions-heading", prepared, StringComparison.Ordinal);
        Assert.Contains("production-action-column", production, StringComparison.Ordinal);
        Assert.Contains("text-align: right !important", css, StringComparison.Ordinal);
        Assert.Contains("justify-content: flex-end !important", css, StringComparison.Ordinal);
        Assert.Contains(".production-col-action", css, StringComparison.Ordinal);
        Assert.Contains("min-width: 1000px", css, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeCreate_FormColumnsAlignAtTop()
    {
        var create = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Create.cshtml");
        var edit = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Edit.cshtml");
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains("production-form-grid", create, StringComparison.Ordinal);
        Assert.Contains("production-form-main", create, StringComparison.Ordinal);
        Assert.Contains("production-form-sidebar", create, StringComparison.Ordinal);
        Assert.Contains("production-form-grid", edit, StringComparison.Ordinal);
        Assert.Contains("align-items: start", css, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeCreate_SectionCardsUseSharedSpacing()
    {
        var create = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Create.cshtml");
        var edit = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Edit.cshtml");
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains("production-form-section", create, StringComparison.Ordinal);
        Assert.Contains("production-form-section", edit, StringComparison.Ordinal);
        Assert.Contains("--production-bom-section-gap", css, StringComparison.Ordinal);
        Assert.Contains(".production-bom-page .production-form-section", css, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeCreate_BomRowsUseConsistentControlHeight()
    {
        var create = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Create.cshtml");
        var builder = Read("CafeChain/wwwroot/js/Admin/Recipe/bom-builder.js");
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains("id=\"bomTable\"", create, StringComparison.Ordinal);
        Assert.Contains("form-control-sm", builder, StringComparison.Ordinal);
        Assert.Contains("min-height: 40px", css, StringComparison.Ordinal);
        Assert.Contains("height: 40px", css, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeEdit_HydratesProductTypeSizeAndBomRowsOnInitialLoad()
    {
        var edit = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Edit.cshtml");
        var builder = Read("CafeChain/wwwroot/js/Admin/Recipe/bom-builder.js");

        Assert.Contains("initialSizeId: @(form.SizeId?.ToString()", edit, StringComparison.Ordinal);
        Assert.Contains("addInitialRow: false", edit, StringComparison.Ordinal);
        Assert.Contains("$('#drinkSelect').trigger('change')", edit, StringComparison.Ordinal);
        Assert.Contains("$('#bomTableBody .item-qty').first().trigger('input')", edit, StringComparison.Ordinal);
        Assert.Contains("var initialSizeId = cfg.initialSizeId", builder, StringComparison.Ordinal);
        Assert.Contains("sizeSelect.val(initialSizeId)", builder, StringComparison.Ordinal);
        Assert.Contains("sizeSelect.trigger('change')", builder, StringComparison.Ordinal);
        Assert.Contains("cfg.addInitialRow !== false", builder, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeForms_GiveBomComponentColumnSemanticWidth()
    {
        var create = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Create.cshtml");
        var edit = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Edit.cshtml");
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains("bom-col-component", create, StringComparison.Ordinal);
        Assert.Contains("bom-col-component", edit, StringComparison.Ordinal);
        Assert.Contains(".bom-col-component { width: 28%; }", css, StringComparison.Ordinal);
        Assert.Contains("table-layout: fixed", css, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadOnlyFields_AreVisuallyDistinctFromButtons()
    {
        var builder = Read("CafeChain/wwwroot/js/Admin/Recipe/bom-builder.js");
        var productionCreate = Read("CafeChain/Areas/Admin/Views/AdminProductionOrder/Create.cshtml");
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains("production-readonly-field", builder, StringComparison.Ordinal);
        Assert.Contains("production-readonly-field", productionCreate, StringComparison.Ordinal);
        Assert.Contains("border-style: dashed", css, StringComparison.Ordinal);
        Assert.Contains("cursor: default", css, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionBomUi_DoesNotRenderRawTechnicalTerms()
    {
        var combined = string.Join('\n', Array.ConvertAll(PageViews, view =>
            Read($"CafeChain/Areas/Admin/Views/{view}")));

        Assert.DoesNotContain(">PreparedItem<", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">ProductionRun<", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Expected Yield<", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Actual Yield<", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">RowVersion<", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">GUID<", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("quy trình cũ", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">SỐ MẺ<", combined, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionHeader_UsesUnifiedHeroLayout()
    {
        var partial = Read("CafeChain/Areas/Admin/Views/Shared/_PageHero.cshtml");
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains("cc-page-hero", partial, StringComparison.Ordinal);
        Assert.Contains("cc-page-hero__accent", partial, StringComparison.Ordinal);
        Assert.Contains("linear-gradient(180deg, var(--cc-brown-600", css, StringComparison.Ordinal);
        Assert.Contains("var(--cc-caramel-500", css, StringComparison.Ordinal);
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
        Assert.Contains("Sơ chế độc lập", combined, StringComparison.Ordinal);
        Assert.DoesNotContain("quy trình cũ", combined, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("@media (max-width: 768px)", css, StringComparison.Ordinal);
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
    public void RecipeDetail_UsesReadableBusinessLayout()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml");
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains("bom-detail-page", view, StringComparison.Ordinal);
        Assert.Contains("bom-overview-grid", view, StringComparison.Ordinal);
        Assert.Contains("bom-health-board", view, StringComparison.Ordinal);
        Assert.Contains("bom-section-heading", view, StringComparison.Ordinal);
        Assert.Contains(".bom-detail-page .bom-overview-grid", css, StringComparison.Ordinal);
        Assert.Contains(".bom-detail-page .bom-health-board", css, StringComparison.Ordinal);
    }

    [Fact]
    public void DataHealth_IsADedicatedBusinessReadableWorkspace()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminRecipe/DataHealth.cshtml");
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains("bom-health-page", view, StringComparison.Ordinal);
        Assert.Contains("bom-health-intro", view, StringComparison.Ordinal);
        Assert.Contains("bom-health-list", view, StringComparison.Ordinal);
        Assert.DoesNotContain("reason.Code", view, StringComparison.Ordinal);
        Assert.Contains(".bom-health-page .bom-health-list", css, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionLists_UseUnifiedTablesAndPagination()
    {
        var recipe = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Index.cshtml");
        var recipeDetail = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml");
        var preparedItem = Read("CafeChain/Areas/Admin/Views/AdminPreparedItem/Index.cshtml");
        var productionRun = Read("CafeChain/Areas/Admin/Views/AdminProductionOrder/Index.cshtml");
        var css = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains("production-data-table", recipe, StringComparison.Ordinal);
        Assert.Contains("production-data-table", preparedItem, StringComparison.Ordinal);
        Assert.Contains("production-data-table", productionRun, StringComparison.Ordinal);
        Assert.Contains("production-pagination", recipe, StringComparison.Ordinal);
        Assert.Contains("production-pagination", preparedItem, StringComparison.Ordinal);
        Assert.Contains("production-pagination", productionRun, StringComparison.Ordinal);
        Assert.Contains("pageNumber = firstPage", productionRun, StringComparison.Ordinal);
        Assert.Contains("production-action-column", productionRun, StringComparison.Ordinal);
        Assert.Contains("bom-source-link", recipeDetail, StringComparison.Ordinal);
        Assert.Contains("production-row-menu", recipe, StringComparison.Ordinal);
        Assert.Contains("production-row-menu", preparedItem, StringComparison.Ordinal);
        Assert.Contains("rb-actions-heading", recipe, StringComparison.Ordinal);
        Assert.Contains("rb-actions-heading", preparedItem, StringComparison.Ordinal);
        Assert.Contains("<colgroup>", recipe, StringComparison.Ordinal);
        Assert.Contains("<colgroup>", preparedItem, StringComparison.Ordinal);
        Assert.DoesNotContain("<th scope=\"col\">Mã BTP</th>", preparedItem, StringComparison.Ordinal);
        Assert.Contains(".production-bom-page .production-data-table", css, StringComparison.Ordinal);
        Assert.Contains(".production-bom-page .production-pagination", css, StringComparison.Ordinal);
        Assert.Contains(".production-bom-page .production-row-actions", css, StringComparison.Ordinal);
        Assert.Contains(".production-bom-page .rb-actions-heading", css, StringComparison.Ordinal);
        Assert.Contains(".production-bom-page.production-run-list-page .production-action-column", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".production-bom-page .production-data-table td:last-child", css, StringComparison.Ordinal);
        Assert.Contains("@media (max-width: 768px)", css, StringComparison.Ordinal);
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
