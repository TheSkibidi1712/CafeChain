namespace CafeChain.Tests;

public sealed class NotificationRealtimeAndHomeSourceTests
{
    [Fact]
    public void Admin_notification_hub_and_client_support_cookie_realtime_with_polling_fallback()
    {
        var hub = Read("CafeChain", "Hubs", "InventoryNotificationHub.cs");
        var layout = Read("CafeChain", "Areas", "Admin", "Views", "Shared", "_AdminLayout.cshtml");
        var client = Read("CafeChain", "wwwroot", "js", "Admin", "Notifications", "inventory-notification-realtime.js");

        Assert.Contains("CookieAuthenticationDefaults.AuthenticationScheme", hub, StringComparison.Ordinal);
        Assert.Contains("JwtBearerDefaults.AuthenticationScheme", hub, StringComparison.Ordinal);
        Assert.Contains("ResolveStoreIdsAsync", hub, StringComparison.Ordinal);
        Assert.Contains("microsoft-signalr/signalr.min.js", layout, StringComparison.Ordinal);
        Assert.Contains("withAutomaticReconnect", client, StringComparison.Ordinal);
        Assert.Contains("setInterval(refresh, 60000)", client, StringComparison.Ordinal);
        Assert.Contains("count > 9 ? \"9+\"", client, StringComparison.Ordinal);
        Assert.Contains("eventId", client, StringComparison.Ordinal);
        Assert.Contains("OperationalOtpNotificationChanged", client, StringComparison.Ordinal);
        Assert.Contains("sessionStorage", client, StringComparison.Ordinal);
        Assert.Contains("notification-", client, StringComparison.Ordinal);
        Assert.Contains("Mở Thông báo", client, StringComparison.Ordinal);
        Assert.DoesNotContain("OperationalOtpIssued", client, StringComparison.Ordinal);
        Assert.DoesNotContain("otpCode", client, StringComparison.OrdinalIgnoreCase);

        var posClient = Read("CafeChain.Frontend", "src", "services", "notificationRealtime.ts");
        Assert.Contains("OperationalOtpNotificationChanged", posClient, StringComparison.Ordinal);
        Assert.DoesNotContain("OperationalOtpIssued", posClient, StringComparison.Ordinal);
        Assert.DoesNotContain("otpCode", posClient, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Home_access_cta_uses_claims_for_staff_customer_and_anonymous()
    {
        var home = Read("CafeChain", "Views", "Home", "Index.cshtml");

        Assert.Contains("isStaffAuthenticated", home, StringComparison.Ordinal);
        Assert.Contains("claim.Type == \"StaffId\"", home, StringComparison.Ordinal);
        Assert.Contains("claim.Type == \"CustomerId\"", home, StringComparison.Ordinal);
        Assert.Contains("\"AppLauncher\"", home, StringComparison.Ordinal);
        Assert.Contains("\"Profile\"", home, StringComparison.Ordinal);
        Assert.Contains("\"Login\"", home, StringComparison.Ordinal);
        Assert.Contains("@accessText", home, StringComparison.Ordinal);
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine([FindRoot(), .. parts]));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "CafeChain")))
            directory = directory.Parent;
        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
