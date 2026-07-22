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
        Assert.Contains("path=\"/pos/customer-display\"", app);
        Assert.True(
            app.IndexOf("path=\"/pos/customer-display\"", StringComparison.Ordinal)
            < app.IndexOf("path=\"/\" element={<RootLayout", StringComparison.Ordinal),
            "Customer display phải nằm ngoài RootLayout để không render navigation hoặc công cụ nội bộ.");
    }

    [Fact]
    public void PaymentAndModifierSheets_PreserveTouchAndKeyboardContracts()
    {
        var payment = ReadFrontend("src", "components", "pos", "payment", "PaymentWorkspace.tsx");
        var modifier = ReadFrontend("src", "components", "ProductModifierModal.tsx");

        Assert.Contains("aria-label=\"Bàn phím nhập tiền\"", payment);
        Assert.Contains("inputMode=\"numeric\"", payment);
        Assert.Contains("pos-payment-workspace", payment);
        Assert.Contains("event.key === 'Escape'", payment);
        Assert.Contains("event.key !== 'Tab'", payment);
        Assert.Contains("role=\"dialog\"", modifier);
        Assert.Contains("aria-modal=\"true\"", modifier);
        Assert.Contains("event.key === 'Escape'", modifier);
        Assert.Contains("event.key !== 'Tab'", modifier);
    }

    [Fact]
    public void PaymentWorkspace_UnifiesCashVietQrAndSplitWithoutManualQrConfirmation()
    {
        var layout = ReadFrontend("src", "POSLayout.tsx");
        var payment = ReadFrontend("src", "components", "pos", "payment", "PaymentWorkspace.tsx");
        var qrPrint = ReadFrontend("src", "services", "vietQrPrint.ts");
        var css = ReadFrontend("src", "index.css");

        Assert.Contains("<PaymentWorkspace", layout);
        Assert.Contains("cash: 'Tiền mặt'", payment);
        Assert.Contains("vietqr: 'VietQR'", payment);
        Assert.Contains("split: 'Thanh toán kết hợp'", payment);
        Assert.Contains("Tiền khách đưa", payment);
        Assert.Contains("Tiền thừa", payment);
        Assert.Contains("Ghi nhận tiền mặt tạm", payment);
        Assert.Contains("Thu phần còn lại bằng VietQR", payment);
        Assert.Contains("Thu phần còn lại bằng tiền mặt", payment);
        Assert.Contains("Đổi sang tiền mặt", payment);
        Assert.Contains("Hủy giao dịch", payment);
        Assert.Contains("Mở trang PayOS", payment);
        Assert.Contains("In mã QR", payment);
        Assert.Contains("Không thể đóng khi giao dịch đang chờ xử lý", payment);
        Assert.Contains("pos-vietqr-print-host", payment);
        Assert.Contains("<VietQrCode", payment);
        Assert.Contains("value={pendingPayment.qrCode}", payment);
        Assert.DoesNotContain("<iframe", payment, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("printVietQrSlip", payment);
        Assert.Contains(".pos-vietqr-print-host", qrPrint);
        Assert.Contains("[data-vietqr-ready=\"true\"]", qrPrint);
        Assert.Contains("window.print()", qrPrint);
        Assert.Contains("Không có nút xác nhận thủ công", payment);
        Assert.DoesNotContain("Tôi đã thanh toán", payment);
        Assert.Contains("CASH_DENOMINATION_STEP = 1000", layout);
        Assert.Contains("validateCashVnd", layout);
        Assert.Contains(".pos-payment-workspace", css);
    }

    [Fact]
    public void ProductOptionsAndCart_UseSingleTouchSheetWithEditableLineState()
    {
        var layout = ReadFrontend("src", "POSLayout.tsx");
        var modifier = ReadFrontend("src", "components", "ProductModifierModal.tsx");
        var cartLine = ReadFrontend("src", "components", "pos", "CartLine.tsx");
        var css = ReadFrontend("src", "index.css");

        Assert.Contains("pos-option-sheet-backdrop", modifier);
        Assert.Contains("pos-option-sheet", modifier);
        Assert.Contains("Ghi chú cho quầy pha chế", modifier);
        Assert.Contains("Cập nhật món", modifier);
        Assert.Contains("quantity", modifier);
        Assert.Contains("aria-pressed={isSelected}", modifier);
        Assert.DoesNotContain("type=\"checkbox\"", modifier);
        Assert.Contains("<CartLine", layout);
        Assert.Contains("requiresProductOptions", layout);
        Assert.Contains("handleProductSelection", layout);
        Assert.Contains("editCartLine", layout);
        Assert.Contains("editingCartId", layout);
        Assert.Contains("Tác vụ", cartLine);
        Assert.Contains("Sửa món", cartLine);
        Assert.Contains("Xóa món", cartLine);
        Assert.Contains(".pos-option-sheet", css);
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
