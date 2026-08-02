using Xunit;

namespace CafeChain.Tests;

public sealed class WarehouseMasterDataIssue280Tests
{
    private const string IngredientView = "CafeChain/Areas/Admin/Views/AdminIngredient/Index.cshtml";
    private const string UnitIndexView = "CafeChain/Areas/Admin/Views/AdminUnitConversion/Index.cshtml";
    private const string UnitCreateView = "CafeChain/Areas/Admin/Views/AdminUnitConversion/Create.cshtml";
    private const string UnitEditView = "CafeChain/Areas/Admin/Views/AdminUnitConversion/Edit.cshtml";

    [Fact]
    public void MasterDataPages_UseSharedWarehouseShells()
    {
        foreach (var path in new[] { IngredientView, UnitIndexView, UnitCreateView, UnitEditView })
        {
            var view = Read(path);
            Assert.Contains("cc-warehouse-page", view);
            Assert.Contains("cc-warehouse-header", view);
        }

        Assert.Contains("cc-warehouse-form-section", Read(UnitCreateView));
        Assert.Contains("cc-warehouse-form-section", Read(UnitEditView));
    }

    [Fact]
    public void IngredientList_IsResponsiveAndIconActionsAreNamed()
    {
        var view = Read(IngredientView);

        Assert.Contains("cc-warehouse-table-shell", view);
        Assert.Contains("cc-warehouse-empty", view);
        Assert.Contains("role=\"status\"", view);
        Assert.Contains("aria-label=\"Chỉnh sửa @item.Name\"", view);
        Assert.Contains("aria-label=\"Đóng hộp thoại nguyên liệu\"", view);
        Assert.Contains("for=\"searchBox\"", view);
        Assert.Contains("for=\"statusFilter\"", view);
    }

    [Fact]
    public void UnitConversionTabsTablesAndEmptyStates_AreAccessible()
    {
        var view = Read(UnitIndexView);

        Assert.Contains("role=\"tablist\"", view);
        Assert.Contains("aria-label=\"Nhóm dữ liệu đơn vị và quy đổi\"", view);
        Assert.Contains("aria-selected=", view);
        Assert.Contains("cc-warehouse-table-shell", view);
        Assert.Contains("cc-warehouse-empty", view);
        Assert.Contains("cc-warehouse-alert", view);
    }

    [Fact]
    public void MasterDataStyles_CoverResponsiveFocusAndReducedMotion()
    {
        var ingredientCss = Read("CafeChain/wwwroot/css/Admin/Ingredient/ingredient.css");
        var unitCss = Read("CafeChain/wwwroot/css/unit-conversion.css");

        Assert.Contains("@media (max-width: 800px)", ingredientCss);
        Assert.Contains("@media (max-width: 767.98px)", unitCss);
        Assert.Contains(":focus-visible", ingredientCss);
        Assert.Contains(":focus-visible", unitCss);
        Assert.Contains("prefers-reduced-motion", ingredientCss);
        Assert.Contains("prefers-reduced-motion", unitCss);
        Assert.Contains("overflow-x: auto", unitCss);
        Assert.DoesNotContain("linear-gradient", unitCss, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("backdrop-filter", ingredientCss, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExistingMasterDataBindingsAndActions_RemainIntact()
    {
        var ingredient = Read(IngredientView);
        var unitIndex = Read(UnitIndexView);
        var unitCreate = Read(UnitCreateView);
        var unitEdit = Read(UnitEditView);

        Assert.Contains("id=\"btnCreate\"", ingredient);
        Assert.Contains("id=\"btnSave\"", ingredient);
        Assert.Contains("asp-action=\"ToggleStatus\"", unitIndex);
        Assert.Contains("asp-action=\"Create\"", unitCreate);
        Assert.Contains("id=\"conversionForm\"", unitCreate);
        Assert.Contains("asp-action=\"Edit\"", unitEdit);
        Assert.Contains("name=\"UnitConversionId\"", unitEdit);
    }

    private static string Read(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }
}
