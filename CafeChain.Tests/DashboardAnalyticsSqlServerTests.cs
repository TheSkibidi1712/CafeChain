using System.Text.RegularExpressions;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Interfaces.Admin.Dashboard;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Services.Admin.Dashboard;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Infrastructure.Repositories.Admin.Procurement;
using CafeChain.Infrastrusture.Repositories.Admin.Dashboard;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class DashboardAnalyticsSqlServerTests : IAsyncLifetime
{
    private const string Database = "CafeChain_DashboardV13_Verify_20260721";
    private static string ConnectionString => SqlServerTestConnection.Create(Database);

    public async Task InitializeAsync()
    {
        await using var master = new SqlConnection(SqlServerTestConnection.MasterConnectionString());
        await master.OpenAsync();
        await using (var command = master.CreateCommand())
        {
            command.CommandText = $"IF DB_ID(N'{Database}') IS NULL CREATE DATABASE [{Database}];";
            await command.ExecuteNonQueryAsync();
        }
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Analytics_script_runs_twice_and_all_contracts_exist()
    {
        var script = ReadAnalyticsScript();
        await ExecuteBatchesAsync(script);
        await ExecuteBatchesAsync(script);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT_BIG(1) FROM sys.procedures WHERE name LIKE 'usp[_]%' OR name LIKE 'sp[_]%';";
        Assert.True(Convert.ToInt64(await command.ExecuteScalarAsync()) >= 45L);
        command.CommandText = "SELECT COUNT_BIG(1) FROM sys.procedures WHERE name='sp_Top_Customers';";
        Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task Empty_database_procedures_return_stable_zero_or_empty_results()
    {
        var script = ReadAnalyticsScript();
        await ExecuteBatchesAsync(script);
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "dbo.usp_Dashboard_NetSalesTrend";
        command.CommandType = System.Data.CommandType.StoredProcedure;
        command.Parameters.AddWithValue("@FromDate", new DateTime(2026, 1, 1));
        command.Parameters.AddWithValue("@ToDate", new DateTime(2026, 1, 3));
        command.Parameters.AddWithValue("@StoreIds", "1");
        command.Parameters.AddWithValue("@Granularity", "Day");
        command.Parameters.AddWithValue("@Top", 10);
        await using var reader = await command.ExecuteReaderAsync();
        var count = 0;
        while (await reader.ReadAsync()) count++;
        Assert.Equal(2, count);
    }

    [Theory]
    [InlineData("Hour", "2026-01-01", "2026-01-01", 0)]
    [InlineData("Day", "2026-01-01", "2026-01-03", 2)]
    [InlineData("Week", "2026-01-01", "2026-01-15", 3)]
    [InlineData("Month", "2026-01-01", "2026-03-31", 3)]
    public async Task Net_sales_trend_returns_one_zero_filled_row_per_requested_bucket(
        string granularity,
        string from,
        string to,
        int expectedRows)
    {
        var script = ReadAnalyticsScript();
        await ExecuteBatchesAsync(script);
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "dbo.usp_Dashboard_NetSalesTrend";
        command.CommandType = System.Data.CommandType.StoredProcedure;
        command.Parameters.AddWithValue("@FromDate", DateTime.Parse(from, System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@ToDate", DateTime.Parse(to, System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@StoreIds", "1");
        command.Parameters.AddWithValue("@Granularity", granularity);
        command.Parameters.AddWithValue("@Top", 10);

        var rows = 0;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows++;
            Assert.Equal(0L, Convert.ToInt64(reader.GetValue(reader.GetOrdinal("TotalOrders"))));
            Assert.Equal(0m, Convert.ToDecimal(reader.GetValue(reader.GetOrdinal("NetSales"))));
        }

        Assert.Equal(expectedRows, rows);
    }

    [Fact]
    public async Task SeedAll_v13_runs_twice_and_exercises_all_dashboard_contracts()
    {
        var root = FindRepoRoot();
        var locations = File.ReadAllText(Path.Combine(root, "CafeChain", "Scripts", "SeedDataDiaChi.sql"))
            .Replace("USE CafeChain;", $"USE [{Database}];", StringComparison.OrdinalIgnoreCase);
        var seed = File.ReadAllText(Path.Combine(root, "CafeChain", "Scripts", "SeedAll.sql"))
            .Replace("$(TargetDatabase)", Database, StringComparison.Ordinal)
            .Replace("use CafeChain", $"use [{Database}]", StringComparison.OrdinalIgnoreCase)
            .Replace("use [$(CafeChainDatabase)]", $"use [{Database}]", StringComparison.OrdinalIgnoreCase)
            .Replace("IF UPPER(DB_NAME()) <> N'CAFECHAIN'",
                $"IF UPPER(DB_NAME()) <> N'{Database.ToUpperInvariant()}'", StringComparison.Ordinal);
        var analytics = File.ReadAllText(Path.Combine(root, "CafeChain", "Scripts",
            "20260717_DashboardAnalyticsStoredProcedures.idempotent.sql"))
            .Replace("use CafeChain", $"use [{Database}]", StringComparison.OrdinalIgnoreCase);

        await ExecuteBatchesAsync(locations);
        await ExecuteBatchesAsync(seed);
        await using (var overrideConnection = new SqlConnection(ConnectionString))
        {
            await overrideConnection.OpenAsync();
            await using var overrideCommand = overrideConnection.CreateCommand();
            overrideCommand.CommandText = """
                IF NOT EXISTS(SELECT 1 FROM dbo.AccountPermissionOverrides WHERE Reason=N'RBAC_PRESERVE_TEST')
                INSERT dbo.AccountPermissionOverrides(AccountId,PermissionId,Effect,Reason)
                SELECT TOP(1) a.AccountId,p.PermissionId,2,N'RBAC_PRESERVE_TEST'
                FROM dbo.Accounts a CROSS JOIN dbo.Permissions p
                WHERE p.Code=N'App.AdminDashboard'
                ORDER BY a.AccountId;
                """;
            await overrideCommand.ExecuteNonQueryAsync();
        }
        await ExecuteBatchesAsync(seed);
        await ExecuteBatchesAsync(analytics);
        await ExecuteBatchesAsync(analytics);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using (var alertCommand = AnalyticsCommand(connection, "dbo.usp_Dashboard_OperationalAlerts"))
        {
            alertCommand.Parameters["@StoreIds"].Value = "1,3";
            await using var alertReader = await alertCommand.ExecuteReaderAsync();
            var alerts = new List<(string Type, string Unit, string Message)>();
            while (await alertReader.ReadAsync())
            {
                alerts.Add((
                    Convert.ToString(alertReader["AlertType"])!,
                    Convert.ToString(alertReader["Unit"])!,
                    Convert.ToString(alertReader["Message"])!));
            }

            Assert.NotEmpty(alerts);
            Assert.All(alerts, alert => Assert.DoesNotContain('?', alert.Message));
            Assert.Contains(alerts, alert => alert.Type == "CASH_DISCREPANCY"
                                             && alert.Message.StartsWith("Chênh lệch", StringComparison.Ordinal));
            Assert.Contains(alerts, alert => alert.Type == "LOW_STOCK"
                                             && alert.Message.StartsWith("Tồn dưới ngưỡng", StringComparison.Ordinal));
            Assert.Contains(alerts, alert => alert.Type == "OVERDUE_PO"
                                             && alert.Message.StartsWith("PO quá hạn", StringComparison.Ordinal));
            var supplierAlerts = alerts.Where(alert => alert.Type == "SUPPLIER_ISSUE").ToList();
            Assert.NotEmpty(supplierAlerts);
            Assert.All(supplierAlerts, alert =>
            {
                Assert.Equal("g", alert.Unit, ignoreCase: true);
                Assert.DoesNotContain("INGREDIENT", alert.Unit, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("sự cố bao bì", alert.Message, StringComparison.OrdinalIgnoreCase);
            });
        }
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.Permissions WHERE Code IN (N'StoreMenu.OverridePrice',N'Profitability.UpdatePrice',N'Profitability.UpdateToppingPolicy',N'PreparedItem.ToggleStatus',N'Recipe.Delete',N'PurchaseAdvice.Update',N'PurchaseAdvice.Cancel',N'PurchaseOrder.CloseRemaining',N'SupplierQuality.Create',N'SupplierQuality.Transition',N'InventoryTransfer.RequestReturn',N'InventoryTransfer.ConfirmReturn',N'InventoryTransfer.ResolveDiscrepancy',N'Order.RefundRequest',N'Order.RefundConfirm',N'System.Diagnostics.View',N'System.Cutover.View',N'System.Cutover.Manage',N'System.LegacyConsolidation.View',N'System.LegacyConsolidation.Manage') AND Active=1;", 20L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.RolePermissions rp JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId WHERE p.Code IN(N'Drink.Delete',N'Category.Delete',N'Size.Delete',N'Topping.Delete');", 0L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.Permissions WHERE Code IN(N'Drink.Delete',N'Category.Delete',N'Size.Delete',N'Topping.Delete') AND Active=0;", 4L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.AccountPermissionOverrides WHERE Reason=N'RBAC_PRESERVE_TEST' AND Effect=2;", 1L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.Roles WHERE Name=N'Khách hàng';", 0L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.Accounts WHERE Email=N'khachhang@gmail.com';", 0L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.Customers WHERE CustomerCode=N'CUS000111';", 0L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.PermissionGroups WHERE Code=N'CUSTOMER' AND Active=1;", 1L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.MemberLevels;", 3L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.PointTransactionTypes;", 4L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.Vouchers;", 3L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.WheelConfigs WHERE Name=N'DEMO_COVERAGE_V17_WHEEL';", 0L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.RatingImages WHERE PublicId=N'demo_coverage_v17_rating';", 0L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.RolePermissions rp JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId JOIN dbo.Roles r ON r.RoleId=rp.RoleId WHERE p.Code=N'App.AdminDashboard' AND r.Name IN(N'Chủ doanh nghiệp',N'Quản lý vùng',N'Quản lý chi nhánh',N'Kế toán/kho');", 4L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.RolePermissions rp JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId JOIN dbo.Roles r ON r.RoleId=rp.RoleId WHERE p.Code=N'App.AdminDashboard' AND r.Name=N'Quản trị hệ thống';", 0L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.RolePermissions rp JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId JOIN dbo.Roles r ON r.RoleId=rp.RoleId WHERE r.Name=N'Quản trị hệ thống' AND p.Code NOT LIKE N'System.%';", 0L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.Permissions p JOIN dbo.PermissionGroups g ON g.PermissionGroupId=p.PermissionGroupId WHERE p.Active=1 AND g.Code=N'POS_WORKSHIFT' AND NOT EXISTS(SELECT 1 FROM dbo.RolePermissions rp JOIN dbo.Roles r ON r.RoleId=rp.RoleId WHERE rp.PermissionId=p.PermissionId AND r.Name=N'Quản trị hệ thống');", 14L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.RolePermissions rp JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId JOIN dbo.Roles r ON r.RoleId=rp.RoleId WHERE p.Active=1 AND r.Name=N'Quản trị hệ thống';", 6L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.RolePermissions rp JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId JOIN dbo.Roles r ON r.RoleId=rp.RoleId WHERE p.Code=N'ReorderSuggestion.View' AND r.Name IN(N'Chủ doanh nghiệp',N'Quản lý vùng',N'Quản lý chi nhánh',N'Kế toán/kho');", 4L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.RolePermissions rp JOIN dbo.Permissions p ON p.PermissionId=rp.PermissionId JOIN dbo.Roles r ON r.RoleId=rp.RoleId WHERE p.Code=N'Restock.Create' AND r.Name IN(N'Quản lý chi nhánh',N'Kế toán/kho');", 2L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.Orders WHERE Source=N'DEMO_DASHBOARD_V13';", 6L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.Payments p JOIN dbo.Orders o ON o.OrderId=p.OrderId WHERE o.Source=N'DEMO_DASHBOARD_V13' AND p.CashSessionId IS NOT NULL;", 0L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.WorkShifts WHERE StoreId=1 AND StartTimeUtc IN ('2026-01-14T23:00:00','2026-01-15T05:00:00','2026-01-15T23:00:00','2026-01-17T23:00:00');", 4L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.PreparedItems;", 12L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.PreparedItems WHERE Active=1 AND PreparedItemId BETWEEN 9 AND 11;", 3L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.PreparedItems WHERE Active=0 AND PreparedItemId=12;", 1L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.Recipes WHERE RecipeId BETWEEN 1 AND 148;", 148L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.Recipes WHERE RecipeId=148 AND Active=0 AND Status=N'Archived' AND PreparedItemId=12;", 1L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.RecipeDetails WHERE RecipeDetailId BETWEEN 1 AND 732;", 732L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.StoreInventories WHERE StoreId=1 AND PreparedItemId BETWEEN 1 AND 11;", 11L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.ProductionRuns WHERE ProductionRunId BETWEEN 1 AND 11;", 11L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.Orders WHERE Source=N'DEMO_AI_DASHBOARD_ROLLING_V1';", 30L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.Orders WHERE Source=N'DEMO_AI_DASHBOARD_ROLLING_V1' AND StoreId=1;", 15L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.Orders WHERE Source=N'DEMO_AI_DASHBOARD_ROLLING_V1' AND StoreId=3;", 15L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.Orders WHERE Source=N'DEMO_AI_DASHBOARD_ROLLING_V1' AND OrderStatusId=6;", 7L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.OrderRefunds r JOIN dbo.Orders o ON o.OrderId=r.OrderId WHERE o.Source=N'DEMO_AI_DASHBOARD_ROLLING_V1';", 4L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.OrderDetails od JOIN dbo.Orders o ON o.OrderId=od.OrderId WHERE o.Source=N'DEMO_AI_DASHBOARD_ROLLING_V1';", 30L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.Payments p JOIN dbo.Orders o ON o.OrderId=p.OrderId WHERE o.Source=N'DEMO_AI_DASHBOARD_ROLLING_V1';", 23L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.OrderDetails od LEFT JOIN dbo.Orders o ON o.OrderId=od.OrderId WHERE od.Note=N'AI Dashboard rolling analytics fixture' AND o.OrderId IS NULL;", 0L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.Payments p LEFT JOIN dbo.Orders o ON o.OrderId=p.OrderId WHERE p.TransactionCode LIKE N'DEMO_AI_DASHBOARD_ROLLING_V1_%' AND o.OrderId IS NULL;", 0L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.RestockRequests WHERE Note LIKE N'DEMO_AI_DASHBOARD_ROLLING_V1_RESTOCK_S%';", 2L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.PurchaseOrders WHERE Note=N'DEMO_AI_DASHBOARD_ROLLING_V1';", 2L);
        await AssertScalarAsync(connection,
            "SELECT COUNT_BIG(1) FROM dbo.StoreInventories si JOIN dbo.Ingredients i ON i.IngredientId=si.IngredientId WHERE si.StoreId=1 AND i.Code=N'DEMO_ING_CHIA_SEED' AND si.MinStockLevel-(si.AvailableQty-si.ReservedQty)=10000;", 1L);

        await using (var reorderContext = CreateContext())
        {
            var reorder = new ReorderSuggestionService(
                new ReorderSuggestionRepository(reorderContext),
                new PhysicalUnitConversionService(
                    reorderContext,
                    NullLogger<PhysicalUnitConversionService>.Instance),
                new PurchaseOrderQuantityProvider(reorderContext),
                Mock.Of<IScopeAuthorizationService>(),
                Mock.Of<IAIService>());

            var calculated = await reorder.CalculateForStoreAsync(1, analysisWindowDays: 30);

            Assert.True(calculated.IsSuccess, calculated.Message);
            var chia = Assert.Single(calculated.Data!.Items.Where(x =>
                x.IngredientCode == "DEMO_ING_CHIA_SEED"));
            Assert.Equal(ReorderRecommendationLevels.Urgent, chia.SuggestionStatus);
            Assert.True(chia.AverageDailyConsumption > 0m);
            Assert.NotNull(chia.IngredientSupplierId);
            Assert.NotNull(chia.PackageBaseQuantity);
            Assert.True(chia.FinalSuggestedQuantity > 0m);
            Assert.True(chia.EstimatedCost > 0m);
            Assert.True(chia.CanConfirm);

            var staffId = await reorderContext.Staffs
                .Where(x => x.StoreId == 1 && x.Active)
                .OrderBy(x => x.StaffId)
                .Select(x => x.StaffId)
                .FirstAsync();
            var scope = new Mock<IScopeAuthorizationService>();
            scope.Setup(x => x.GetAllowedStoresAsync(staffId))
                .ReturnsAsync([new Store { StoreId = 1, Active = true }]);
            var reorderAuthorization = new Mock<IReorderSuggestionAuthorizationService>();
            reorderAuthorization.Setup(x => x.CanViewAsync(
                    It.IsAny<AdminActorContext>(), 1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            var dashboardAuthorization = new Mock<IDashboardAuthorizationService>();
            dashboardAuthorization.Setup(x => x.AuthorizeWidgetsAsync(
                    It.IsAny<AdminActorContext>(), It.IsAny<IReadOnlyCollection<DashboardAnalyticsWidget>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DashboardAuthorizationDto
                {
                    AllowedWidgets = Enum.GetValues<DashboardAnalyticsWidget>(),
                    AllowedSections = Enum.GetValues<DashboardSection>()
                });
            var dashboard = new DashboardService(
                new DashboardRepository(
                    reorderContext,
                    NullLogger<DashboardRepository>.Instance),
                scope.Object,
                reorderSuggestions: reorder,
                reorderAuthorization: reorderAuthorization.Object,
                authorization: dashboardAuthorization.Object);

            var widgets = await dashboard.GetAnalyticsBatchAsync(
                new AdminActorContext { StaffId = staffId, StoreId = 1 },
                [DashboardAnalyticsWidget.InventoryReorderSuggestions],
                new DashboardAnalyticsFilter
                {
                    FromDate = DateTime.Today.AddDays(-30),
                    ToDate = DateTime.Today,
                    StoreId = 1,
                    Granularity = "Day",
                    Top = 10
                });
            var reorderRows = Assert.IsAssignableFrom<IReadOnlyList<InventoryReorderRow>>(
                widgets.Widgets[DashboardAnalyticsWidget.InventoryReorderSuggestions].Rows);
            Assert.Contains(reorderRows, x =>
                x.IngredientCode == "DEMO_ING_CHIA_SEED"
                && x.SuggestionStatus == ReorderRecommendationLevels.Urgent
                && x.FinalSuggestedQuantity > 0m);
        }

        await using (var command = AnalyticsCommand(connection, "dbo.usp_Workforce_ShiftStatus"))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            var statuses = new HashSet<string>(StringComparer.Ordinal);
            var sawCustomTime = false;
            var sawOvernight = false;
            while (await reader.ReadAsync())
            {
                statuses.Add(reader.GetString(reader.GetOrdinal("StatusCode")));
                var start = reader.GetDateTime(reader.GetOrdinal("PlannedStartAt"));
                var end = reader.GetDateTime(reader.GetOrdinal("PlannedEndAt"));
                sawCustomTime |= start == new DateTime(2026, 1, 15, 13, 0, 0)
                                 && end == new DateTime(2026, 1, 15, 17, 30, 0);
                sawOvernight |= reader.GetBoolean(reader.GetOrdinal("IsOvernight"))
                                && end.Date > start.Date;
            }
            Assert.All(statuses, status => Assert.Contains(status, new[] { "SCHEDULED", "CANCELLED" }));
            Assert.Contains("SCHEDULED", statuses);
            Assert.Contains("CANCELLED", statuses);
            Assert.True(sawCustomTime);
            Assert.True(sawOvernight);
        }

        await using (var command = AnalyticsCommand(connection, "dbo.usp_Workforce_StaffPerformance"))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            var found = false;
            while (await reader.ReadAsync())
            {
                if (reader.GetInt32(reader.GetOrdinal("StaffId")) != 4) continue;
                found = true;
                Assert.Equal(4L, Convert.ToInt64(reader["WorkShiftCount"]));
                Assert.Equal(3L, Convert.ToInt64(reader["TotalOrders"]));
                Assert.Equal(157000m, Convert.ToDecimal(reader["NetSales"]));
            }
            Assert.True(found);
        }

        await using (var command = AnalyticsCommand(connection, "dbo.usp_Workforce_HourlyDemand"))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            var staffing = new Dictionary<int, long>();
            while (await reader.ReadAsync())
                staffing[Convert.ToInt32(reader["HourOfDay"])] = Convert.ToInt64(reader["ScheduledStaffCount"]);
            Assert.Equal(1L, staffing[1]);
            // Two scheduled morning rows exist after the consolidated seed; the cancelled row must not become a third.
            Assert.Equal(2L, staffing[6]);
            Assert.Equal(1L, staffing[23]);
        }

        await using (var command = AnalyticsCommand(connection, "dbo.usp_Dashboard_PaymentMethodMix"))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            var amounts = new Dictionary<string, decimal>(StringComparer.Ordinal);
            while (await reader.ReadAsync())
                amounts[Convert.ToString(reader["PaymentMethodCode"])!] = Convert.ToDecimal(reader["Amount"]);
            Assert.Equal(33000m, amounts["CASH"]);
            Assert.Equal(50000m, amounts["BANK"]);
            Assert.Equal(74000m, amounts["MOMO"]);
        }

        await using (var command = AnalyticsCommand(connection, "dbo.usp_Product_TopProducts"))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            var bacXiuSold = 0;
            var sawPartialCogs = false;
            while (await reader.ReadAsync())
            {
                if (Convert.ToString(reader["DrinkName"]) == "Bạc xỉu")
                    bacXiuSold = Convert.ToInt32(reader["TotalSold"]);
                sawPartialCogs |= Convert.ToString(reader["DataStatus"]) == "PARTIAL_COGS";
            }
            Assert.Equal(1, bacXiuSold);
            Assert.True(sawPartialCogs);
        }

        await using (var command = AnalyticsCommand(connection, "dbo.usp_Workforce_ShiftStatus"))
        {
            command.Parameters["@StoreIds"].Value = "999";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.False(await reader.ReadAsync());
        }

        foreach (var procedure in CanonicalProcedures)
        {
            await using var command = AnalyticsCommand(connection, $"dbo.{procedure}");
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) { }
        }
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlServer(ConnectionString).Options);

    private static async Task ExecuteBatchesAsync(string script)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                     .Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = batch;
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static string ReadAnalyticsScript() =>
        File.ReadAllText(Path.Combine(
            FindRepoRoot(), "CafeChain", "Scripts",
            "20260717_DashboardAnalyticsStoredProcedures.idempotent.sql"))
        .Replace("use CafeChain", $"use [{Database}]", StringComparison.OrdinalIgnoreCase);

    private static SqlCommand AnalyticsCommand(SqlConnection connection, string procedure)
    {
        var command = connection.CreateCommand();
        command.CommandText = procedure;
        command.CommandType = System.Data.CommandType.StoredProcedure;
        command.Parameters.AddWithValue("@FromDate", new DateTime(2026, 1, 1));
        command.Parameters.AddWithValue("@ToDate", new DateTime(2026, 1, 31));
        command.Parameters.AddWithValue("@StoreIds", "1");
        command.Parameters.AddWithValue("@Granularity", "Day");
        command.Parameters.AddWithValue("@Top", 20);
        return command;
    }

    private static async Task AssertScalarAsync(SqlConnection connection, string sql, long expected)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        Assert.Equal(expected, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    private static readonly string[] CanonicalProcedures =
    [
        "usp_Dashboard_NetSalesTrend", "usp_Dashboard_StoreRanking", "usp_Dashboard_PaymentMethodMix",
        "usp_Dashboard_OrderHeatmap", "usp_Dashboard_OperationalAlerts",
        "usp_Operations_WorkShiftCashDiscrepancy", "usp_Operations_WorkShiftSales", "usp_Operations_WorkShiftPaymentMix",
        "usp_Operations_OfflineReconciliationExceptions", "usp_Operations_HourlyOrders",
        "usp_Operations_WorkShiftTopDiscrepancies", "usp_Operations_WorkShiftKpis",
        "usp_Inventory_ShortageRisk", "usp_Inventory_MovementByType", "usp_Inventory_ThresholdRisk",
        "usp_Inventory_WasteByStoreIngredient", "usp_Inventory_FifoLayerAge",
        "usp_Procurement_PurchaseOrderPipeline", "usp_Procurement_OverduePurchaseOrders", "usp_Procurement_SupplierQuality",
        "usp_Procurement_PurchasePriceTrend", "usp_Procurement_SpendBreakdown", "usp_Procurement_SupplierIssueMix",
        "usp_Product_TopProducts", "usp_Product_VolumeMarginMatrix", "usp_Product_SizeMargin",
        "usp_Product_TopToppings", "usp_Product_BomHealth", "usp_Product_HighConsumptionLowEfficiency",
        "usp_Product_LowVolumeProducts", "usp_Product_LowMarginProducts",
        "usp_Workforce_ShiftStatus", "usp_Workforce_HourlyDemand", "usp_Workforce_StaffPerformance"
    ];

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "CafeChain")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
