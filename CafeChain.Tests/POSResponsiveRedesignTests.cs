using System;
using System.IO;
using Xunit;

namespace CafeChain.Tests;

public sealed class POSResponsiveRedesignTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void PosTheme_UsesOwnerLockedColorTokens()
    {
        var css = ReadFrontend("src", "index.css");

        Assert.Contains("--pos-app-bg: #f7f3ee", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--pos-surface: #ffffff", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--pos-primary: #6f4e37", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--pos-primary-hover: #5c3f2b", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--pos-accent: #c67a45", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--pos-success: #2f6f5e", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--pos-warning: #99623b", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--pos-danger: #991b1b", css, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PosShell_HasWideMediumPortraitAndNarrowLayouts()
    {
        var css = ReadFrontend("src", "index.css");

        Assert.Contains("'header header header'", css);
        Assert.Contains("'category catalog cart'", css);
        Assert.Contains("@media (max-width: 1199px)", css);
        Assert.Contains("@media (max-width: 819px)", css);
        Assert.Contains("@media (max-width: 420px)", css);
        Assert.Contains(".pos-mobile-cart-bar", css);
        Assert.Contains(".pos-cart-panel[data-open='true']", css);
        Assert.Contains("env(safe-area-inset-bottom)", css);
        Assert.Contains("100dvh", css);
    }

    [Fact]
    public void PosLayout_ExposesSearchAllCategoryAndAccessibleCartDrawer()
    {
        var source = ReadFrontend("src", "POSLayout.tsx");
        var header = ReadFrontend("src", "components", "pos", "SellingHeader.tsx");

        Assert.Contains("id=\"pos-product-search\"", header);
        Assert.Contains("Tìm món theo tên", header);
        Assert.Contains("Dùng tại quán", header);
        Assert.Contains("Mang đi", header);
        Assert.Contains("NetworkStatusIndicator", header);
        Assert.Contains("PrinterStatusBadge", header);
        Assert.Contains("Tác vụ", header);
        Assert.Contains("<SellingHeader", source);
        Assert.Contains("setSelectedCategory(null)", source);
        Assert.Contains(">Tất cả<", source);
        Assert.Contains("className=\"pos-mobile-cart-bar\"", source);
        Assert.Contains("aria-controls=\"pos-cart-panel\"", source);
        Assert.Contains("data-open={isCartOpen}", source);
        Assert.Contains("aria-live=\"polite\"", source);
        Assert.Contains("pos-touch-target", source);
    }

    [Fact]
    public void SellingRoute_UsesDedicatedHeaderWithoutUnmountingPosStateForResponsiveLayout()
    {
        var app = ReadFrontend("src", "App.tsx");
        var header = ReadFrontend("src", "components", "pos", "SellingHeader.tsx");

        Assert.Contains("const isSellingRoute", app);
        Assert.Contains("{!isSellingRoute && <TopNavbar />}", app);
        Assert.Contains("onOrderTypeChange", header);
        Assert.Contains("onSearchChange", header);
        Assert.Contains("to=\"/history\"", header);
        Assert.Contains("to=\"/shift\"", header);
    }

    [Fact]
    public void PaymentAndModifierSheets_PreserveTouchAndKeyboardContracts()
    {
        var layout = ReadFrontend("src", "POSLayout.tsx");
        var modifier = ReadFrontend("src", "components", "ProductModifierModal.tsx");

        Assert.Contains("aria-label=\"Bàn phím nhập tiền\"", layout);
        Assert.Contains("inputMode=\"numeric\"", layout);
        Assert.Contains("pos-adaptive-dialog", layout);
        Assert.Contains("role=\"dialog\"", modifier);
        Assert.Contains("aria-modal=\"true\"", modifier);
        Assert.Contains("event.key === 'Escape'", modifier);
        Assert.Contains("event.key !== 'Tab'", modifier);
    }

    [Fact]
    public void PosScope_DoesNotReintroduceLegacyOrangePalette()
    {
        var files = new[]
        {
            ReadFrontend("src", "index.css"),
            ReadFrontend("src", "POSLayout.tsx"),
            ReadFrontend("src", "components", "TopNavbar.tsx"),
            ReadFrontend("src", "components", "pos", "SellingHeader.tsx"),
            ReadFrontend("src", "components", "ProductModifierModal.tsx"),
            ReadFrontend("src", "hooks", "usePrinterStatus.ts")
        };

        foreach (var source in files)
        {
            Assert.DoesNotContain("#FF8C00", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("#E67E00", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("#EA580C", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ReadFrontend(params string[] segments) =>
        File.ReadAllText(Path.Combine([RepoRoot, "CafeChain.Frontend", .. segments]));

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "CafeChain.Frontend"))
                && Directory.Exists(Path.Combine(current.FullName, "CafeChain.Tests")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy thư mục gốc CafeChain.");
    }
}
