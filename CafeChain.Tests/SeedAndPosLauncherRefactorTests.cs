using CafeChain.Application.Services.AppLauncher;

namespace CafeChain.Tests;

public sealed class SeedAndPosLauncherRefactorTests
{
    [Fact]
    public void Store1_seed_uses_business_keys_fixed_dates_and_idempotent_markers()
    {
        var sql = ReadRepoFile("CafeChain/Scripts/20260718_CafeChain_Store1_Complete_Demo_Seed.idempotent.sql");

        Assert.Contains("Part1_SeedDataDrink.sql", sql, StringComparison.Ordinal);
        Assert.Contains("DEMO_PART1_SKU_", sql, StringComparison.Ordinal);
        Assert.Contains("DEMO_SUP_INACTIVE", sql, StringComparison.Ordinal);
        Assert.Contains("IngredientSupplierPriceHistories", sql, StringComparison.Ordinal);
        Assert.Contains("InventoryCostLayers", sql, StringComparison.Ordinal);
        Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IDENTITY_INSERT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SYSUTCDATETIME()", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dashboard_seed_covers_every_table_read_by_dashboard_procedures()
    {
        var sql = ReadRepoFile("CafeChain/Scripts/SeedAll.sql");
        var required = new[]
        {
            "Orders", "OrderDetails", "OrderToppings", "Payments", "OrderRefunds",
            "StaffShifts", "WorkShifts", "PurchaseOrders",
            "PurchaseOrderLines", "BranchReceipts", "BranchReceiptLines",
            "SupplierReceiptIssues", "RestockRequests"
        };

        foreach (var table in required)
            Assert.Contains($"dbo.{table}", sql, StringComparison.Ordinal);

        Assert.Contains("DEMO_DASHBOARD_V13", sql, StringComparison.Ordinal);
        Assert.Contains("CashSessionId,TransactionCode", sql, StringComparison.Ordinal);
        Assert.Contains("NULL,", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("N'APPROVED'", ExtractDashboardBatch(sql), StringComparison.Ordinal);
        Assert.DoesNotContain("N'SENT'", ExtractDashboardBatch(sql), StringComparison.Ordinal);
    }

    [Fact]
    public void Launcher_no_longer_routes_pos_to_legacy_admin_page()
    {
        var launcher = ReadRepoFile("CafeChain/Application/Services/AppLauncher/AppLauncherService.cs");
        var coordinator = ReadRepoFile("CafeChain/Application/Services/AppLauncher/PosLaunchCoordinator.cs");
        var view = ReadRepoFile("CafeChain/Views/AppLauncher/Index.cshtml");

        Assert.DoesNotContain("/Admin/AdminPOS", launcher, StringComparison.Ordinal);
        Assert.Contains("RequiresLaunch", launcher, StringComparison.Ordinal);
        Assert.Contains("SemaphoreSlim", coordinator, StringComparison.Ordinal);
        Assert.Contains("IsFrontendReadyAsync", coordinator, StringComparison.Ordinal);
        Assert.Contains("data-launch-pos-url", view, StringComparison.Ordinal);
    }

    [Fact]
    public void Launcher_no_longer_starts_or_waits_for_print_bridge()
    {
        var coordinator = ReadRepoFile("CafeChain/Application/Services/AppLauncher/PosLaunchCoordinator.cs");
        var options = ReadRepoFile("CafeChain/Application/Options/PosLauncherOptions.cs");
        var configuration = ReadRepoFile("CafeChain/appsettings.json");
        var script = ReadRepoFile("CafeChain/wwwroot/js/AppLauncher/app-launcher.js");

        Assert.DoesNotContain("StartBridge", coordinator, StringComparison.Ordinal);
        Assert.DoesNotContain("IPrintBridgePresenceTracker", coordinator, StringComparison.Ordinal);
        Assert.DoesNotContain("PrintBridgeProject", options, StringComparison.Ordinal);
        Assert.DoesNotContain("PrintBridgeStoreId", configuration, StringComparison.Ordinal);
        Assert.DoesNotContain("CafeChain.PrintBridge", script, StringComparison.Ordinal);
        Assert.Contains("CafeChain.Frontend", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Print_bridge_presence_expires_stale_heartbeat()
    {
        var tracker = new PrintBridgePresenceTracker();
        tracker.MarkConnected(1, "connection-1");

        Assert.True(tracker.IsOnline(1, TimeSpan.FromMinutes(1)));
        Assert.False(tracker.IsOnline(2, TimeSpan.FromMinutes(1)));
        Assert.False(tracker.IsOnline(1, TimeSpan.Zero));
    }

    [Fact]
    public void Print_bridge_heartbeat_cannot_register_an_unauthenticated_connection()
    {
        var tracker = new PrintBridgePresenceTracker();

        tracker.MarkHeartbeat(1, "not-joined");

        Assert.False(tracker.IsOnline(1, TimeSpan.FromMinutes(1)));
    }

    private static string ReadRepoFile(string relativePath)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        return File.ReadAllText(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string ExtractDashboardBatch(string sql)
    {
        var marker = sql.IndexOf("BATCH 13/13 - DASHBOARD", StringComparison.Ordinal);
        return marker < 0 ? string.Empty : sql[marker..];
    }
}
