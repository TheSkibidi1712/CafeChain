namespace CafeChain.Tests;

using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

public sealed class AppearanceAndLocalizationContractTests
{
    private static readonly string Root = FindRoot();

    [Fact]
    public void RequestLocalization_SupportsOnlyVietnameseAndEnglish()
    {
        var source = Read("CafeChain/Extensions/Pipeline/ApplicationBuilderExtensions.cs");
        Assert.Contains("new CultureInfo(\"vi-VN\")", source);
        Assert.Contains("new CultureInfo(\"en-US\")", source);
        Assert.Contains("new CookieRequestCultureProvider()", source);
    }

    [Fact]
    public void CultureEndpoint_UsesAllowListAndLocalRedirect()
    {
        var source = Read("CafeChain/Controllers/UiPreferencesController.cs");
        Assert.Contains("{ \"vi-VN\", \"en-US\" }", source);
        Assert.Contains("Url.IsLocalUrl(returnUrl)", source);
        Assert.Contains("LocalRedirect", source);
    }

    [Fact]
    public void SharedLayouts_LoadLightAppearanceAndCultureSelector()
    {
        foreach (var path in new[]
        {
            "CafeChain/Views/Shared/_Layout.cshtml",
            "CafeChain/Areas/Admin/Views/Shared/_AdminLayout.cshtml"
        })
        {
            var source = Read(path);
            Assert.Contains("js/shared/appearance.js", source);
            Assert.Contains("css/shared/appearance.css", source);
            Assert.DoesNotContain("data-theme-toggle", source);
            Assert.Contains("data-culture-selector", source);
        }
    }

    [Fact]
    public void PrintSurfaces_RemainExplicitlyLight()
    {
        var source = Read("CafeChain.Frontend/src/index.css");
        Assert.Contains(".receipt-template", source);
        Assert.Contains("background: #ffffff", source);
        Assert.Contains("color: #000000", source);
        Assert.Contains("@media print", source);
    }

    [Fact]
    public void StandaloneAuthenticationAndRecipeViews_DoNotLockTheme()
    {
        foreach (var path in new[]
        {
            "CafeChain/Views/Account/Login.cshtml",
            "CafeChain/Views/Password/ForgotPassword.cshtml",
            "CafeChain/Views/Password/VerifyOtp.cshtml",
            "CafeChain/Views/Password/ResetPassword.cshtml",
            "CafeChain/Views/AppLauncher/Index.cshtml",
            "CafeChain/Areas/Admin/Views/AdminRecipe/Create.cshtml",
            "CafeChain/Areas/Admin/Views/AdminRecipe/Edit.cshtml"
        })
        {
            var source = Read(path);
            Assert.DoesNotContain("<html lang=\"vi\" data-theme=", source);
            Assert.DoesNotContain("recipe-form-page\" data-theme=", source);
            Assert.DoesNotContain("cafechain_theme", source);
        }
    }

    [Fact]
    public void StandaloneAuthenticationViews_ExposeCultureWithoutThemeSwitcher()
    {
        foreach (var path in new[]
        {
            "CafeChain/Views/Account/Login.cshtml",
            "CafeChain/Views/Password/ForgotPassword.cshtml",
            "CafeChain/Views/Password/VerifyOtp.cshtml"
        })
        {
            var source = Read(path);
            Assert.Contains("data-culture-selector", source);
            Assert.DoesNotContain("themeToggleBtn", source);
            Assert.DoesNotContain("CafeChainAppearance", source);
        }
    }

    [Fact]
    public void SharedResources_HaveVietnameseAndEnglishCatalogs()
    {
        var vietnamese = Read("CafeChain/Resources/SharedResource.vi-VN.resx");
        var english = Read("CafeChain/Resources/SharedResource.en-US.resx");
        foreach (var key in new[] { "Theme.Label", "Theme.System", "Language.Label", "Navigation.Home" })
        {
            Assert.Contains($"name=\"{key}\"", vietnamese);
            Assert.Contains($"name=\"{key}\"", english);
        }
    }

    [Fact]
    public void SharedResources_HaveMatchingKeysAndPlaceholders()
    {
        var vietnamese = ReadResource("CafeChain/Resources/SharedResource.vi-VN.resx");
        var english = ReadResource("CafeChain/Resources/SharedResource.en-US.resx");

        Assert.Equal(vietnamese.Keys.Order(), english.Keys.Order());
        foreach (var key in vietnamese.Keys)
        {
            Assert.Equal(Placeholders(vietnamese[key]), Placeholders(english[key]));
        }
    }

    [Theory]
    [InlineData("SharedResource")]
    [InlineData("AdminNavigationResource")]
    [InlineData("ProductResource")]
    [InlineData("InventoryResource")]
    [InlineData("ProductionResource")]
    public void LocalizedResourcePairs_HaveMatchingKeysAndPlaceholders(string resourceName)
    {
        var vietnamese = ReadResource($"CafeChain/Resources/{resourceName}.vi-VN.resx");
        var english = ReadResource($"CafeChain/Resources/{resourceName}.en-US.resx");

        Assert.Equal(vietnamese.Keys.Order(), english.Keys.Order());
        foreach (var key in vietnamese.Keys)
            Assert.Equal(Placeholders(vietnamese[key]), Placeholders(english[key]));
    }

    [Theory]
    [InlineData("vi-VN")]
    [InlineData("en-US")]
    public void ResourceManager_ResolvesModuleResourcesWithoutReturningRawKeys(string cultureName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization(options => options.ResourcesPath = "Resources");
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IStringLocalizerFactory>();
        var previousCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
            Assert.False(factory.Create(typeof(CafeChain.AdminNavigationResource))["Products.Category"].ResourceNotFound);
            var product = factory.Create(typeof(CafeChain.ProductResource));
            Assert.False(product["Drink.Title"].ResourceNotFound);
            var formattedImageLabel = product["Topping.ImageAria", "Pearl"];
            Assert.False(formattedImageLabel.ResourceNotFound);
            Assert.Contains("Pearl", formattedImageLabel.Value);
            var inventory = factory.Create(typeof(CafeChain.InventoryResource));
            Assert.False(inventory["Transfer.Index.Title"].ResourceNotFound);
            Assert.False(inventory["Transfer.Js.SourceRequired"].ResourceNotFound);
            var production = factory.Create(typeof(CafeChain.ProductionResource));
            Assert.False(production["ProductionOrder.Title"].ResourceNotFound);
            Assert.False(production["ProductionOrder.Status.Completed"].ResourceNotFound);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    [Fact]
    public void AppLauncher_UsesLightAppearanceAndStableLocalizedAppCodes()
    {
        var source = Read("CafeChain/Views/AppLauncher/Index.cshtml");
        Assert.Contains("js/shared/appearance.js", source);
        Assert.Contains("css/shared/appearance.css", source);
        Assert.DoesNotContain("data-theme-toggle", source);
        Assert.Contains("data-culture-selector", source);
        Assert.Contains("AppLauncher.App.{app.Code}.Title", source);
        Assert.DoesNotContain("app.Title.ToLower().Contains", source);
    }

    [Fact]
    public void LoginQuickAccounts_UsesAnOverflowSafeGrid()
    {
        var source = Read("CafeChain/Views/Account/Login.cshtml");
        Assert.Contains("repeat(4, minmax(0, 1fr))", source);
        Assert.Contains("min-width: 0;", source);
        Assert.Contains("overflow-wrap: anywhere;", source);
    }

    [Fact]
    public void AdminAppearanceAdapter_LoadsAfterFeatureStyles()
    {
        var source = Read("CafeChain/Areas/Admin/Views/Shared/_AdminLayout.cshtml");
        var featureStyles = source.IndexOf("RenderSectionAsync(\"Styles\"", StringComparison.Ordinal);
        var appearance = source.IndexOf("css/shared/appearance.css", StringComparison.Ordinal);
        Assert.True(featureStyles >= 0 && appearance > featureStyles);
    }

    [Fact]
    public void AdminJavaScript_UsesCanonicalDataCultureInsteadOfHtmlLangGuessing()
    {
        var dashboard = Read("CafeChain/wwwroot/js/Admin/Dashboard/dashboard.js");
        Assert.Contains("document.documentElement.dataset.culture", dashboard);
        Assert.DoesNotContain("document.documentElement.lang ===", dashboard);
    }

    [Fact]
    public void AdminShell_HasNoHardCodedVietnameseNavigationLabels()
    {
        var source = Read("CafeChain/Areas/Admin/Views/Shared/_AdminLayout.cshtml");
        foreach (var text in new[] { "> Danh mục</a>", "> Đồ uống</a>", "> Menu cửa hàng</a>", "> Quản lý nhân viên" })
            Assert.DoesNotContain(text, source);
        Assert.Contains("AdminNavigationResource", source);
    }

    [Fact]
    public void ProductScreenshotViews_UseProductResourcesForSystemText()
    {
        foreach (var path in new[]
        {
            "CafeChain/Areas/Admin/Views/AdminDrink/Index.cshtml",
            "CafeChain/Areas/Admin/Views/AdminSize/Index.cshtml",
            "CafeChain/Areas/Admin/Views/AdminTopping/Index.cshtml",
            "CafeChain/Areas/Admin/Views/AdminDrinkProfitability/Index.cshtml",
            "CafeChain/Areas/Admin/Views/AdminStoreMenu/Index.cshtml"
        })
            Assert.Contains("ProductResource", Read(path));
    }

    [Fact]
    public void AdminDarkAdapter_CoversDashboardAndCategorySurfaces()
    {
        var source = Read("CafeChain/wwwroot/css/shared/appearance.css");
        Assert.Contains(":root[data-theme=\"dark\"] .analytics-header", source);
        Assert.Contains(":root[data-theme=\"dark\"] .category-table", source);
        Assert.Contains("var(--cc-surface-raised)", source);
    }

    [Fact]
    public void AdminScreenshotViews_UseStableLocalizationKeys()
    {
        var dashboard = Read("CafeChain/Areas/Admin/Views/Dashboard/Index.cshtml");
        var category = Read("CafeChain/Areas/Admin/Views/AdminCategory/Index.cshtml");
        Assert.Contains("Dashboard.Title", dashboard);
        Assert.Contains("Category.Title", category);
        Assert.DoesNotContain("Theme.SwitchTo", Read("CafeChain/Areas/Admin/Views/Shared/_AdminLayout.cshtml"));
    }

    [Fact]
    public void JavaScriptUiCatalog_FailsClosedForMissingKeysAndValues()
    {
        var source = Read("CafeChain/wwwroot/js/shared/ui-catalog.js");
        Assert.Contains("Missing UI localization key", source);
        Assert.Contains("Missing UI localization value", source);
        Assert.DoesNotContain("return key", source);
    }

    [Fact]
    public void SharedLayouts_LoadJavaScriptUiCatalog()
    {
        foreach (var path in new[]
        {
            "CafeChain/Views/Shared/_Layout.cshtml",
            "CafeChain/Areas/Admin/Views/Shared/_AdminLayout.cshtml"
        })
            Assert.Contains("js/shared/ui-catalog.js", Read(path));
    }

    private static Dictionary<string, string> ReadResource(string relativePath) =>
        XDocument.Parse(Read(relativePath))
            .Root!
            .Elements("data")
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")?.Value ?? string.Empty);

    private static string[] Placeholders(string value) =>
        Regex.Matches(value, @"\{\d+\}")
            .Select(match => match.Value)
            .Order()
            .ToArray();

    private static string Read(string relativePath) => File.ReadAllText(Path.Combine(Root, relativePath));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.csproj")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
