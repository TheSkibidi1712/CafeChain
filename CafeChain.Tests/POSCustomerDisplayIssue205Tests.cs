using System;
using System.IO;
using Xunit;

namespace CafeChain.Tests;

public sealed class POSCustomerDisplayIssue205Tests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void MessageContract_IsVersionedOrderedShiftScopedAndShortLived()
    {
        var service = ReadFrontend("src", "services", "customerDisplay.ts");

        Assert.Contains("schemaVersion: 1", service);
        Assert.Contains("CustomerDisplayMessage = CustomerDisplaySnapshot", service);
        Assert.Contains("messageId: string", service);
        Assert.Contains("sessionId: string", service);
        Assert.Contains("sequence: number", service);
        Assert.Contains("validUntil: number", service);
        Assert.Contains("CUSTOMER_DISPLAY_SNAPSHOT_TTL_MS = 5 * 60 * 1000", service);
        Assert.Contains("candidate.sequence > current.sequence", service);
        Assert.Contains("snapshot.workShiftId === expectedWorkShiftId", service);
        Assert.Contains("if (expectedWorkShiftId === null) return null", service);
        Assert.Contains("readStoredSequence(sessionId) + 1", service);
    }

    [Fact]
    public void Transport_UsesBroadcastChannelWithSafeStorageFallback()
    {
        var service = ReadFrontend("src", "services", "customerDisplay.ts");

        Assert.Contains("new BroadcastChannel(CHANNEL_NAME)", service);
        Assert.Contains("localStorage.setItem(SNAPSHOT_STORAGE_KEY", service);
        Assert.Contains("window.addEventListener('storage'", service);
        Assert.Contains("isFreshCustomerDisplaySnapshot", service);
        Assert.Contains("isNewerCustomerDisplaySnapshot", service);
        Assert.DoesNotContain("pos_jwt_token", service, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", service, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("document.cookie", service, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("checksum", service, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("staffName", service, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("customerName", service, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WindowCoordinator_ReusesPerShiftAndProvidesManualFallback()
    {
        var windowService = ReadFrontend("src", "services", "customerDisplayWindow.ts");

        Assert.Contains("const displayWindows = new Map<number, Window>()", windowService);
        Assert.Contains("existingWindow && !existingWindow.closed", windowService);
        Assert.Contains("if (existingWindow?.closed) displayWindows.delete(workShiftId)", windowService);
        Assert.Contains("cafechain-customer-display-${workShiftId}", windowService);
        Assert.Contains("url.searchParams.set('workShiftId'", windowService);
        Assert.Contains("if (!displayWindow)", windowService);
        Assert.Contains("manualUrl", windowService);
        Assert.Contains("getScreenDetails", windowService);
        Assert.Contains("moveTo", windowService);
        Assert.Contains("resizeTo", windowService);
    }

    [Fact]
    public void CustomerDisplay_RendersSafeOperationalStatesAndGuardsOldTimers()
    {
        var page = ReadFrontend("src", "pages", "CustomerDisplay.tsx");
        var layout = ReadFrontend("src", "POSLayout.tsx");

        Assert.Contains("readExpectedWorkShiftId", page);
        Assert.Contains("current?.messageId === successMessageId ? null : current", page);
        Assert.Contains("requestFullscreen", page);
        Assert.Contains("snapshot?.state === 'offline'", page);
        Assert.Contains("snapshot?.state === 'success'", page);
        Assert.Contains("snapshot?.state === 'cancelled'", page);
        Assert.Contains("snapshot?.state === 'expired' || isQrExpired", page);
        Assert.Contains("snapshot?.state === 'cart'", page);
        Assert.Contains("snapshot?.state === 'vietqr'", page);
        Assert.Contains("<VietQrCode", page);
        Assert.Contains("value={snapshot.qrCode}", page);
        Assert.DoesNotContain("<iframe", page, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("customerDisplayGuardRef.current !== guardId", layout);
        Assert.Contains("current?.guardId === guardId ? null : current", layout);
    }

    [Fact]
    public void PosPublishesOnlyAllowlistedCartAndCustomerFacingQrFields()
    {
        var layout = ReadFrontend("src", "POSLayout.tsx");
        var service = ReadFrontend("src", "services", "customerDisplay.ts");

        Assert.Contains("publishCustomerDisplay", layout);
        Assert.Contains("name: item.name", layout);
        Assert.Contains("quantity: item.quantity", layout);
        Assert.Contains("lineTotal: item.price * item.quantity", layout);
        Assert.Contains("optionSummary: item.optionSummary", layout);
        Assert.Contains("qrCode: pendingPayment.qrCode ?? undefined", layout);
        Assert.Contains("orderId: pendingPayment.orderId", layout);
        Assert.Contains("expiresAt: pendingPayment.expiresAt", layout);
        Assert.DoesNotContain("checkoutUrl?: string", service);
        Assert.DoesNotContain("pos_jwt_token", layout, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("document.cookie", layout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VietQr_IsGeneratedLocallyAndCheckoutUrlIsOnlyAnExplicitPayOsLink()
    {
        var renderer = ReadFrontend("src", "components", "pos", "VietQrCode.tsx");
        var payment = ReadFrontend("src", "components", "pos", "payment", "PaymentWorkspace.tsx");

        Assert.Contains("QRCode.toDataURL(value", renderer);
        Assert.Contains("data-vietqr-ready=\"true\"", renderer);
        Assert.Contains("<VietQrCode", payment);
        Assert.Contains("value={pendingPayment.qrCode}", payment);
        Assert.Contains("href={pendingPayment.checkoutUrl}", payment);
        Assert.Contains("Mở trang PayOS", payment);
        Assert.DoesNotContain("<iframe", payment, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VietQrPrint_RequiresTheSharedRenderedQrBeforePrinting()
    {
        var print = ReadFrontend("src", "services", "vietQrPrint.ts");
        var payment = ReadFrontend("src", "components", "pos", "payment", "PaymentWorkspace.tsx");

        Assert.Contains("[data-vietqr-ready=\"true\"]", print);
        Assert.Contains("Mã VietQR chưa sẵn sàng để in", print);
        Assert.Contains("window.print()", print);
        Assert.Contains("pos-vietqr-print-details", payment);
        Assert.Contains("pendingPayment.vietQrAmount", payment);
        Assert.Contains("pendingPayment.orderId", payment);
    }

    [Fact]
    public void PosClearsStaleQrForTerminalStatesAndWorkShiftChanges()
    {
        var layout = ReadFrontend("src", "POSLayout.tsx");

        Assert.Contains("state: 'cancelled'", layout);
        Assert.Contains("state: 'expired'", layout);
        Assert.Contains("state: 'success'", layout);
        Assert.Contains("checkoutUrl: undefined", layout);
        Assert.Contains("qrCode: null", layout);
        Assert.Contains("previousCustomerDisplayShiftRef", layout);
        Assert.Contains("previousShiftId !== customerDisplayShiftId", layout);
        Assert.Contains("workShiftId: previousShiftId", layout);
        Assert.Contains("state: 'idle'", layout);
    }

    [Fact]
    public void FrontendDeclaresOnlyTheApprovedQrDependencies()
    {
        var packageJson = File.ReadAllText(Path.Combine(RepoRoot, "CafeChain.Frontend", "package.json"));

        Assert.Contains("\"qrcode\": \"^1.5.4\"", packageJson);
        Assert.Contains("\"@types/qrcode\": \"^1.5.6\"", packageJson);
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
