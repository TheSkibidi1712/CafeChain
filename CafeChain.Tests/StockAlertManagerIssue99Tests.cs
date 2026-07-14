using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Customers;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Permissions;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CafeChain.Tests.POS
{
    /// <summary>Issue #99 — StoreManager confirm/reject StockAlert.</summary>
    public class StockAlertManagerIssue99Tests : IntegrationTestBase
    {
        private const int StoreId = 100;
        private const int OtherStoreId = 101;
        private const int IngredientId = 9901;
        private const int ManagerStaffId = 910;
        private const int ReporterStaffId = 911;
        private const int SalesStaffId = 912;
        private const int SupervisorStaffId = 913;
        private const int AccountantStaffId = 914;
        private const int UnitId = 1;

        [Fact]
        public async Task StoreManager_ConfirmsOpenAlert_WithNote()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedOpenAlertAsync(ctx, StoreId, withReporter: true);
            var service = CreateService(ctx);

            var result = await service.ConfirmAsync(alertId, ManagerStaffId, StoreId, "Đã kiểm tra, đúng thiếu hàng.");

            Assert.True(result.IsSuccess);
            var alert = await ctx.StockAlerts.SingleAsync(a => a.StockAlertId == alertId);
            Assert.Equal(StockAlertStatuses.Confirmed, alert.Status);
            Assert.Equal(ManagerStaffId, alert.ConfirmedByStaffId);
            Assert.NotNull(alert.ConfirmedAt);
            Assert.Equal("Đã kiểm tra, đúng thiếu hàng.", alert.ManagerNote);
            Assert.Equal("Báo thiếu từ quầy.", alert.Note); // reporter note preserved
            Assert.Equal(1, await ctx.StaffNotifications.CountAsync(n =>
                n.RecipientStaffId == ReporterStaffId &&
                n.Type == StaffNotificationTypes.StockAlertConfirmed));
        }

        [Fact]
        public async Task Confirm_WithoutNote_Rejected()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedOpenAlertAsync(ctx, StoreId, withReporter: false);
            var service = CreateService(ctx);

            var result = await service.ConfirmAsync(alertId, ManagerStaffId, StoreId, "  ");

            Assert.False(result.IsSuccess);
            Assert.Equal(StockAlertStatuses.Open, (await ctx.StockAlerts.SingleAsync()).Status);
        }

        [Fact]
        public async Task StoreManager_RejectsOpenAlert_WithReason()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedOpenAlertAsync(ctx, StoreId, withReporter: true);
            var service = CreateService(ctx);

            var result = await service.RejectAsync(alertId, ManagerStaffId, StoreId, "Kho vật lý vẫn còn đủ.");

            Assert.True(result.IsSuccess);
            var alert = await ctx.StockAlerts.SingleAsync(a => a.StockAlertId == alertId);
            Assert.Equal(StockAlertStatuses.Rejected, alert.Status);
            Assert.Equal(ManagerStaffId, alert.RejectedByStaffId);
            Assert.NotNull(alert.RejectedAt);
            Assert.Equal("Kho vật lý vẫn còn đủ.", alert.RejectReason);
            Assert.Equal(1, await ctx.StaffNotifications.CountAsync(n =>
                n.RecipientStaffId == ReporterStaffId &&
                n.Type == StaffNotificationTypes.StockAlertRejected));
        }

        [Fact]
        public async Task Reject_WithoutReason_Rejected()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedOpenAlertAsync(ctx, StoreId, withReporter: false);
            var service = CreateService(ctx);

            var result = await service.RejectAsync(alertId, ManagerStaffId, StoreId, "");

            Assert.False(result.IsSuccess);
            Assert.Equal(StockAlertStatuses.Open, (await ctx.StockAlerts.SingleAsync()).Status);
        }

        [Fact]
        public async Task SalesStaff_CannotConfirm()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedOpenAlertAsync(ctx, StoreId, withReporter: false);
            EnsureStaffWithRole(ctx, SalesStaffId, RoleConstants.SalesStaff, "sales@test.local");
            await ctx.SaveChangesAsync();
            var service = CreateService(ctx);

            var result = await service.ConfirmAsync(alertId, SalesStaffId, StoreId, "Ghi chú đủ dài.");
            Assert.False(result.IsSuccess);
            Assert.Contains("không có quyền", result.Message);
        }

        [Fact]
        public async Task ShiftSupervisor_CannotConfirm()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedOpenAlertAsync(ctx, StoreId, withReporter: false);
            EnsureStaffWithRole(ctx, SupervisorStaffId, RoleConstants.ShiftSupervisor, "ss@test.local");
            await ctx.SaveChangesAsync();
            var service = CreateService(ctx);

            var result = await service.ConfirmAsync(alertId, SupervisorStaffId, StoreId, "Ghi chú đủ dài.");
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task AccountantWarehouse_CannotReject()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedOpenAlertAsync(ctx, StoreId, withReporter: false);
            EnsureStaffWithRole(ctx, AccountantStaffId, RoleConstants.AccountantWarehouse, "aw@test.local");
            await ctx.SaveChangesAsync();
            var service = CreateService(ctx);

            var result = await service.RejectAsync(alertId, AccountantStaffId, StoreId, "Lý do đủ dài.");
            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task OtherStore_CannotAct()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedOpenAlertAsync(ctx, StoreId, withReporter: false);
            var service = CreateService(ctx);

            var result = await service.ConfirmAsync(alertId, ManagerStaffId, OtherStoreId, "Ghi chú xác nhận đủ dài.");

            Assert.False(result.IsSuccess);
            Assert.Contains("cửa hàng", result.Message, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task CannotConfirm_Resolved_Confirmed_OrRejected()
        {
            using var ctx = CreateDbContext();
            var service = CreateService(ctx);

            var resolvedId = await SeedAlertWithStatusAsync(ctx, StockAlertStatuses.Resolved);
            Assert.False((await service.ConfirmAsync(resolvedId, ManagerStaffId, StoreId, "Ghi chú.")).IsSuccess);

            var confirmedId = await SeedAlertWithStatusAsync(ctx, StockAlertStatuses.Confirmed);
            Assert.False((await service.RejectAsync(confirmedId, ManagerStaffId, StoreId, "Lý do.")).IsSuccess);

            var rejectedId = await SeedAlertWithStatusAsync(ctx, StockAlertStatuses.Rejected);
            Assert.False((await service.ConfirmAsync(rejectedId, ManagerStaffId, StoreId, "Ghi chú.")).IsSuccess);
        }

        [Fact]
        public async Task NoReporter_NoNotificationCreated()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedOpenAlertAsync(ctx, StoreId, withReporter: false);
            var service = CreateService(ctx);

            await service.ConfirmAsync(alertId, ManagerStaffId, StoreId, "Xác nhận không có người báo.");

            Assert.Equal(0, await ctx.StaffNotifications.CountAsync());
        }

        [Fact]
        public void AdminDeepLink_MapsToStockAlertDetails()
        {
            var url = CafeChain.Application.Services.Operations.StaffNotificationQueryService.MapAdminTargetUrl(
                StaffNotificationEntityTypes.StockAlert,
                StaffNotificationTypes.StockShortageReport,
                42);
            Assert.Equal("/Admin/AdminStockAlerts/Details/42", url);
        }

        // ---------- helpers ----------

        private static StockAlertManagerService CreateService(CafeChain.Data.AppDbContext ctx) =>
            new(
                ctx,
                new CafeChain.Application.Services.Security.ScopeAuthorizationService(ctx),
                new Mock<ILogger<StockAlertManagerService>>().Object);

        private async Task<int> SeedOpenAlertAsync(
            CafeChain.Data.AppDbContext ctx,
            int storeId,
            bool withReporter)
        {
            EnsureBase(ctx);
            EnsureStaffWithRole(ctx, ManagerStaffId, RoleConstants.StoreManager, "mgr@test.local");
            if (withReporter)
                EnsureStaffWithRole(ctx, ReporterStaffId, RoleConstants.SalesStaff, "rep@test.local");

            var alert = new StockAlert
            {
                StoreId = storeId,
                IngredientId = IngredientId,
                AlertType = StockAlertTypes.LowStock,
                Severity = StockAlertSeverities.Warning,
                Status = StockAlertStatuses.Open,
                Source = StockAlertSources.SalesReport,
                CurrentQtySnapshot = 2,
                ThresholdSnapshot = 10,
                Note = "Báo thiếu từ quầy.",
                ReportedByStaffId = withReporter ? ReporterStaffId : null,
                ReportedAt = withReporter ? System.DateTime.UtcNow : null,
                CreatedAt = System.DateTime.UtcNow,
                UpdatedAt = System.DateTime.UtcNow
            };
            ctx.StockAlerts.Add(alert);
            await ctx.SaveChangesAsync();
            return alert.StockAlertId;
        }

        private async Task<int> SeedAlertWithStatusAsync(CafeChain.Data.AppDbContext ctx, string status)
        {
            EnsureBase(ctx);
            EnsureStaffWithRole(ctx, ManagerStaffId, RoleConstants.StoreManager, "mgr@test.local");

            // Need unique ingredient per alert to avoid unique OPEN filter collisions when OPEN
            var ingredientId = IngredientId + Math.Abs(status.GetHashCode() % 1000) + 50;
            if (!ctx.Ingredients.Any(i => i.IngredientId == ingredientId))
            {
                ctx.Ingredients.Add(new Ingredient
                {
                    IngredientId = ingredientId,
                    Code = $"I{ingredientId}",
                    Name = $"Ing {ingredientId}",
                    BaseUnitId = UnitId,
                    Active = true
                });
            }

            var alert = new StockAlert
            {
                StoreId = StoreId,
                IngredientId = ingredientId,
                AlertType = StockAlertTypes.OutOfStock,
                Severity = StockAlertSeverities.Urgent,
                Status = status,
                Source = StockAlertSources.ManualCheck,
                CurrentQtySnapshot = 0,
                CreatedAt = System.DateTime.UtcNow,
                UpdatedAt = System.DateTime.UtcNow
            };
            ctx.StockAlerts.Add(alert);
            await ctx.SaveChangesAsync();
            return alert.StockAlertId;
        }

        private static void EnsureBase(CafeChain.Data.AppDbContext ctx)
        {
            if (!ctx.Units.Any(u => u.UnitId == UnitId))
            {
                ctx.Units.Add(new Unit
                {
                    UnitId = UnitId,
                    UnitCode = "g",
                    Name = "Gram",
                    Active = true
                });
            }

            if (!ctx.Stores.Any(s => s.StoreId == StoreId))
            {
                ctx.Stores.Add(new Store
                {
                    StoreId = StoreId,
                    Name = "Store 99",
                    Address = "x",
                    Phone = "0",
                    Active = true,
                    CreatedAt = System.DateTime.UtcNow
                });
            }

            if (!ctx.Stores.Any(s => s.StoreId == OtherStoreId))
            {
                ctx.Stores.Add(new Store
                {
                    StoreId = OtherStoreId,
                    Name = "Store 99B",
                    Address = "x",
                    Phone = "0",
                    Active = true,
                    CreatedAt = System.DateTime.UtcNow
                });
            }

            if (!ctx.Ingredients.Any(i => i.IngredientId == IngredientId))
            {
                ctx.Ingredients.Add(new Ingredient
                {
                    IngredientId = IngredientId,
                    Code = "ING99",
                    Name = "Sữa #99",
                    BaseUnitId = UnitId,
                    Active = true
                });
            }
        }

        private static void EnsureStaffWithRole(
            CafeChain.Data.AppDbContext ctx,
            int staffId,
            string roleName,
            string email)
        {
            if (ctx.Staffs.Any(s => s.StaffId == staffId)) return;

            EnsureRole(ctx, roleName);
            // Seeded roles (HasData) live in DB, not Local, until tracked.
            var role = ctx.Roles.Local.FirstOrDefault(r => r.Name == roleName)
                       ?? ctx.Roles.First(r => r.Name == roleName);

            var accountId = 20000 + staffId;
            ctx.Accounts.Add(new Account
            {
                AccountId = accountId,
                Email = email,
                PasswordHash = "x",
                Active = true,
                CreatedAt = System.DateTime.UtcNow
            });
            ctx.AccountRoles.Add(new AccountRole
            {
                AccountId = accountId,
                RoleId = role.RoleId
            });
            ctx.Staffs.Add(new Staff
            {
                StaffId = staffId,
                AccountId = accountId,
                StoreId = StoreId,
                FullName = $"Staff {staffId}",
                Active = true,
                CreatedAt = System.DateTime.UtcNow,
                BaseSalary = 0
            });
        }

        private static void EnsureRole(CafeChain.Data.AppDbContext ctx, string roleName)
        {
            // RoleConfiguration.HasData already seeds standard roles on EnsureCreated.
            if (ctx.Roles.Any(r => r.Name == roleName) || ctx.Roles.Local.Any(r => r.Name == roleName))
                return;

            var nextId = (ctx.Roles.Any() ? ctx.Roles.Max(r => r.RoleId) : 0) + 1
                         + ctx.Roles.Local.Count();
            var id = roleName switch
            {
                RoleConstants.StoreManager => 3,
                RoleConstants.SalesStaff => 4,
                RoleConstants.AccountantWarehouse => 5,
                RoleConstants.ShiftSupervisor => 8,
                _ => nextId + 20
            };
            if (ctx.Roles.Any(r => r.RoleId == id))
                id = nextId + 30;

            ctx.Roles.Add(new Role
            {
                RoleId = id,
                Name = roleName,
                Active = true,
                IsStoreLevel = true,
                CreatedAt = System.DateTime.UtcNow
            });
        }
    }
}
