using System;
using System.IO;
using Xunit;

namespace CafeChain.Tests;

public sealed class POSLayoutModesIssue270Tests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void LayoutPreference_IsTypedPersistedAndDefaultsSafelyToAuto()
    {
        var hook = ReadFrontend("src", "hooks", "usePosLayoutMode.ts");
        var header = ReadFrontend("src", "components", "pos", "SellingHeader.tsx");

        Assert.Contains("'cafechain.pos.layoutPreference'", hook);
        Assert.Contains("'auto' | 'desktop' | 'tablet'", hook);
        Assert.Contains("return isLayoutPreference(storedPreference) ? storedPreference : 'auto'", hook);
        Assert.Contains("window.localStorage.setItem(POS_LAYOUT_PREFERENCE_KEY, preference)", hook);
        Assert.Contains("['auto', 'Tự động']", header);
        Assert.Contains("['desktop', 'Máy tính']", header);
        Assert.Contains("['tablet', 'Máy tính bảng']", header);
        Assert.Contains("aria-pressed={layoutPreference === value}", header);
    }

    [Fact]
    public void AutoLayout_UsesViewportOrientationPointerAndHoverSignals()
    {
        var hook = ReadFrontend("src", "hooks", "usePosLayoutMode.ts");

        Assert.Contains("signals.width <= 1180", hook);
        Assert.Contains("signals.hasCoarsePointer && !signals.hasHover", hook);
        Assert.Contains("signals.orientation === 'portrait'", hook);
        Assert.Contains("window.addEventListener('resize', updateLayout)", hook);
        Assert.Contains("window.addEventListener('orientationchange', updateLayout)", hook);
        Assert.Contains("query.addEventListener('change', updateLayout)", hook);
    }

    [Fact]
    public void PosUsesOneRouteAndOneStateTreeForAllLayoutModes()
    {
        var app = ReadFrontend("src", "App.tsx");
        var layout = ReadFrontend("src", "POSLayout.tsx");

        Assert.Contains("<Route path=\"order\" element={<POSLayout />} />", app);
        Assert.Contains("data-pos-layout={resolvedLayout}", layout);
        Assert.Contains("data-pos-layout-preference={layoutPreference}", layout);
        Assert.Contains("data-pos-orientation={layoutOrientation}", layout);
        Assert.Contains("const [cart, setCart]", layout);
        Assert.Contains("const [pendingPayment, setPendingPayment]", layout);
        Assert.Contains("<PaymentWorkspace", layout);
        Assert.Contains("<ProductModifierModal", layout);
        Assert.DoesNotContain("DesktopPOSLayout", layout);
        Assert.DoesNotContain("TabletPOSLayout", layout);
    }

    [Fact]
    public void DesktopAndTabletModesExposeRequiredGridAndCartCompositions()
    {
        var css = ReadFrontend("src", "index.css");

        Assert.Contains(".pos-shell[data-pos-layout='desktop']", css);
        Assert.Contains("grid-template-columns: repeat(5", css);
        Assert.Contains("grid-template-columns: repeat(6", css);
        Assert.Contains("grid-template-columns: repeat(7", css);
        Assert.Contains(".pos-shell[data-pos-layout='tablet'][data-pos-orientation='landscape']", css);
        Assert.Contains("clamp(350px, 38vw, 420px)", css);
        Assert.Contains(".pos-shell[data-pos-layout='tablet'][data-pos-orientation='portrait']", css);
        Assert.Contains(".pos-mobile-cart-bar", css);
        Assert.Contains(".pos-cart-panel[data-open='true']", css);
        Assert.Contains("repeat(2, minmax(0, 1fr))", css);
        Assert.Contains("repeat(3, minmax(0, 1fr))", css);
    }

    [Fact]
    public void PortraitCartDrawer_IsKeyboardAccessibleAndLayoutSwitchLocksDuringPayment()
    {
        var layout = ReadFrontend("src", "POSLayout.tsx");
        var header = ReadFrontend("src", "components", "pos", "SellingHeader.tsx");

        Assert.Contains("role={resolvedLayout === 'tablet' && layoutOrientation === 'portrait' ? 'dialog'", layout);
        Assert.Contains("aria-modal={resolvedLayout === 'tablet' && layoutOrientation === 'portrait'", layout);
        Assert.Contains("event.key === 'Escape'", layout);
        Assert.Contains("event.key !== 'Tab'", layout);
        Assert.Contains("previousFocus?.focus()", layout);
        Assert.Contains("isLayoutSwitchLocked={isCartLocked}", layout);
        Assert.Contains("disabled={isLayoutSwitchLocked}", header);
    }

    [Fact]
    public void DevelopmentPrinterSimulator_IsCollapsedWithoutChangingProductionGuard()
    {
        var simulator = ReadFrontend("src", "components", "dev", "PrinterStatusSimulator.tsx");

        Assert.Contains("if (!import.meta.env.DEV)", simulator);
        Assert.Contains("<details className=\"pos-printer-simulator", simulator);
        Assert.Contains("<summary", simulator);
        Assert.Contains("mock-printer-status", simulator);
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
