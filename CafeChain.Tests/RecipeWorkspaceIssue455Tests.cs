namespace CafeChain.Tests;

public sealed class RecipeWorkspaceIssue455Tests
{
    [Fact]
    public void RecipeWorkspace_HasExactlyOnePageHeading()
    {
        var hero = Read("CafeChain/Areas/Admin/Views/Shared/_PageHero.cshtml");
        var workspace = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml");

        Assert.Equal(1, CountOccurrences(hero + workspace, "<h1"));
        Assert.Contains("<h2 id=\"recipe-identity-title\">", workspace, StringComparison.Ordinal);
    }

    [Fact]
    public void WhereUsed_BoundingIsTransparentToUser()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml");
        var query = Read("CafeChain/Application/Services/Admin/Recipes/RecipeWhereUsedQueryService.cs");

        Assert.Contains("ParentResultsTruncated", view, StringComparison.Ordinal);
        Assert.Contains("PointOfSaleResultsTruncated", view, StringComparison.Ordinal);
        Assert.Contains("còn nơi sử dụng khác chưa hiển thị", view, StringComparison.Ordinal);
        Assert.Contains("còn chi nhánh khác chưa hiển thị", view, StringComparison.Ordinal);
        Assert.Contains("Take(RecipeWhereUsedLimits.MaxParentResults + 1)", query, StringComparison.Ordinal);
        Assert.Contains("Take(RecipeWhereUsedLimits.MaxPointOfSaleResults + 1)", query, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeWorkspace_UsesLogicalSectionHeadingsAndNavigation()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml");

        Assert.Contains("<h2 id=\"recipe-flow-title\">", view, StringComparison.Ordinal);
        Assert.Contains("<h3 id=\"prepared-inputs-title\">", view, StringComparison.Ordinal);
        Assert.Contains("<h3 id=\"direct-inputs-title\">", view, StringComparison.Ordinal);
        Assert.Contains("href=\"#recipe-where-used\">Được sử dụng ở đâu</a>", view, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeWorkspace_ControlsHaveLabelsAndVisibleKeyboardFocus()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml");
        var css = Read("CafeChain/wwwroot/css/Admin/Recipe/recipe-workspace.css");
        var sharedCss = Read("CafeChain/wwwroot/css/Admin/production-bom-ui.css");

        Assert.Contains("<label for=\"storeId\">", view, StringComparison.Ordinal);
        Assert.Contains("a:focus-visible", css, StringComparison.Ordinal);
        Assert.Contains("button:focus-visible", css, StringComparison.Ordinal);
        Assert.Contains("select:focus-visible", css, StringComparison.Ordinal);
        Assert.Contains("input:focus-visible", css, StringComparison.Ordinal);
        Assert.Contains("outline-offset: 2px", css, StringComparison.Ordinal);
        Assert.Contains(".cc-page-hero__action.is-primary:focus-visible", sharedCss, StringComparison.Ordinal);
        Assert.Contains("var(--cc-focus-ring", sharedCss, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeWorkspace_MobileUsesSingleColumnWithoutWideBomTable()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml");
        var css = Read("CafeChain/wwwroot/css/Admin/Recipe/recipe-workspace.css");

        Assert.DoesNotContain("<table", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@media (max-width: 767.98px)", css, StringComparison.Ordinal);
        Assert.Contains(".recipe-workspace__identity,", css, StringComparison.Ordinal);
        Assert.Contains(".recipe-workspace__where-used-list", css, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: 1fr", css, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeWorkspace_LongVietnameseNamesWrapSafely()
    {
        var css = Read("CafeChain/wwwroot/css/Admin/Recipe/recipe-workspace.css");

        Assert.Contains("min-width: 0", css, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: anywhere", css, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeWorkspace_DoesNotExposeRawTechnicalLanguage()
    {
        var views = string.Join('\n', new[]
        {
            Read("CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml"),
            Read("CafeChain/Areas/Admin/Views/AdminRecipe/CompareVersions.cshtml"),
            Read("CafeChain/Areas/Admin/Views/AdminRecipe/Create.cshtml"),
            Read("CafeChain/Areas/Admin/Views/AdminRecipe/Edit.cshtml"),
            Read("CafeChain/Areas/Admin/Views/AdminRecipe/DataHealth.cshtml")
        });

        Assert.DoesNotContain(">PreparedItem<", views, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">RecipeDetail<", views, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">ChildRecipe<", views, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Where-used<", views, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Readiness<", views, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(">Effective<", views, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bán thành phẩm lồng", views, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Phiên bản công thức", views, StringComparison.Ordinal);
        Assert.Contains("Bán thành phẩm đầu vào", views, StringComparison.Ordinal);
        Assert.Contains("Mức sẵn sàng", views, StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeWorkspace_ReadOnlyActionsRemainPermissionBound()
    {
        var view = Read("CafeChain/Areas/Admin/Views/AdminRecipe/Visualize.cshtml");

        Assert.Contains("if (Model.CanWrite)", view, StringComparison.Ordinal);
        Assert.Contains("Tạo phiên bản mới", view, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled", view, StringComparison.OrdinalIgnoreCase);
    }

    private static int CountOccurrences(string value, string token) =>
        value.Split(token, StringSplitOptions.None).Length - 1;

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));

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
