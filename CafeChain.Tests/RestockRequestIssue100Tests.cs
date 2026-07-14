using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
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
    /// <summary>Issue #100 — RestockRequest from CONFIRMED StockAlert.</summary>
    public class RestockRequestIssue100Tests : IntegrationTestBase
    {
        private const int StoreId = 200;
        private const int OtherStoreId = 201;
        private const int IngredientId = 8801;
        private const int RecipeId = 8802;
        private const int PreparedItemId = 8803;
        private const int ManagerStaffId = 810;
        private const int SalesStaffId = 811;
        private const int SupervisorStaffId = 812;
        private const int AccountantStaffId = 813;
        private const int UnitId = 1;

        [Fact]
        public async Task StoreManager_CreatesFromConfirmed_Ingredient()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedAlertAsync(ctx, StockAlertStatuses.Confirmed, ingredient: true, withAw: true);
            var service = CreateService(ctx);

            var result = await service.CreateFromConfirmedAlertAsync(
                alertId, ManagerStaffId, StoreId, 25.5m, "Cần gấp", "HIGH");

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.True(result.Data!.NotifiedAccountantWarehouse);

            var req = await ctx.RestockRequests.SingleAsync();
            Assert.Equal(RestockRequestStatuses.Submitted, req.Status);
            Assert.Equal(IngredientId, req.IngredientId);
            Assert.Null(req.RecipeId);
            Assert.Equal(25.5m, req.RequestedQuantity);
            Assert.Equal(RestockRequestPriorities.High, req.Priority);
            Assert.Equal(ManagerStaffId, req.CreatedByStaffId);
            Assert.Equal(alertId, req.StockAlertId);
            // Suggested: threshold 10 - current 2 = 8
            Assert.Equal(8m, req.SuggestedQuantity);

            var alert = await ctx.StockAlerts.SingleAsync(a => a.StockAlertId == alertId);
            Assert.Equal(StockAlertStatuses.Confirmed, alert.Status);

            Assert.Equal(1, await ctx.StaffNotifications.CountAsync(n =>
                n.RecipientStaffId == AccountantStaffId &&
                n.Type == StaffNotificationTypes.RestockRequestSubmitted &&
                n.EntityType == StaffNotificationEntityTypes.RestockRequest &&
                n.EntityId == req.RestockRequestId));
        }

        [Fact]
        public async Task StoreManager_CreatesFromConfirmed_RecipeBtp()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedAlertAsync(ctx, StockAlertStatuses.Confirmed, ingredient: false, withAw: false);
            var service = CreateService(ctx);

            var result = await service.CreateFromConfirmedAlertAsync(
                alertId, ManagerStaffId, StoreId, 10m, null, null);

            Assert.True(result.IsSuccess);
            var req = await ctx.RestockRequests.SingleAsync();
            Assert.Equal(RecipeId, req.RecipeId);
            Assert.Equal(PreparedItemId, req.PreparedItemId);
            Assert.Null(req.IngredientId);
            Assert.Equal(RestockRequestPriorities.Urgent, req.Priority); // OUT_OF_STOCK default
        }

        [Theory]
        [InlineData(StockAlertStatuses.Open)]
        [InlineData(StockAlertStatuses.Rejected)]
        [InlineData(StockAlertStatuses.Resolved)]
        public async Task CannotCreate_FromNonConfirmed(string status)
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedAlertAsync(ctx, status, ingredient: true, withAw: false);
            var service = CreateService(ctx);

            var result = await service.CreateFromConfirmedAlertAsync(
                alertId, ManagerStaffId, StoreId, 5m, null, "NORMAL");

            Assert.False(result.IsSuccess);
            Assert.Equal(0, await ctx.RestockRequests.CountAsync());
        }

        [Fact]
        public async Task OtherStore_CannotCreate()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedAlertAsync(ctx, StockAlertStatuses.Confirmed, ingredient: true, withAw: false);
            var service = CreateService(ctx);

            var result = await service.CreateFromConfirmedAlertAsync(
                alertId, ManagerStaffId, OtherStoreId, 5m, null, "NORMAL");

            Assert.False(result.IsSuccess);
            Assert.Contains("cửa hàng", result.Message, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SalesStaff_CannotCreate()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedAlertAsync(ctx, StockAlertStatuses.Confirmed, ingredient: true, withAw: false);
            EnsureStaffWithRole(ctx, SalesStaffId, RoleConstants.SalesStaff, "sales100@test.local");
            await ctx.SaveChangesAsync();
            var service = CreateService(ctx);

            var result = await service.CreateFromConfirmedAlertAsync(
                alertId, SalesStaffId, StoreId, 5m, null, "NORMAL");

            Assert.False(result.IsSuccess);
            Assert.Contains("không có quyền", result.Message);
        }

        [Fact]
        public async Task ShiftSupervisor_CannotCreate()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedAlertAsync(ctx, StockAlertStatuses.Confirmed, ingredient: true, withAw: false);
            EnsureStaffWithRole(ctx, SupervisorStaffId, RoleConstants.ShiftSupervisor, "ss100@test.local");
            await ctx.SaveChangesAsync();
            var service = CreateService(ctx);

            var result = await service.CreateFromConfirmedAlertAsync(
                alertId, SupervisorStaffId, StoreId, 5m, null, "NORMAL");

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task AccountantWarehouse_CannotCreate()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedAlertAsync(ctx, StockAlertStatuses.Confirmed, ingredient: true, withAw: true);
            var service = CreateService(ctx);

            var result = await service.CreateFromConfirmedAlertAsync(
                alertId, AccountantStaffId, StoreId, 5m, null, "NORMAL");

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task RequestedQuantity_ZeroOrNegative_Rejected()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedAlertAsync(ctx, StockAlertStatuses.Confirmed, ingredient: true, withAw: false);
            var service = CreateService(ctx);

            Assert.False((await service.CreateFromConfirmedAlertAsync(
                alertId, ManagerStaffId, StoreId, 0m, null, "NORMAL")).IsSuccess);
            Assert.False((await service.CreateFromConfirmedAlertAsync(
                alertId, ManagerStaffId, StoreId, -1m, null, "NORMAL")).IsSuccess);
        }

        [Fact]
        public async Task DuplicateSubmitted_Rejected()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedAlertAsync(ctx, StockAlertStatuses.Confirmed, ingredient: true, withAw: false);
            var service = CreateService(ctx);

            Assert.True((await service.CreateFromConfirmedAlertAsync(
                alertId, ManagerStaffId, StoreId, 5m, null, "NORMAL")).IsSuccess);

            var second = await service.CreateFromConfirmedAlertAsync(
                alertId, ManagerStaffId, StoreId, 8m, null, "HIGH");

            Assert.True(second.IsSuccess);
            Assert.True(second.Data!.AlreadyExisted);
            Assert.Equal(1, await ctx.RestockRequests.CountAsync());
        }

        [Fact]
        public async Task ProcessingRequest_IsStillActive_AndBlocksDuplicateCreation()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedAlertAsync(ctx, StockAlertStatuses.Confirmed, ingredient: true, withAw: false);
            var service = CreateService(ctx);

            Assert.True((await service.CreateFromConfirmedAlertAsync(
                alertId, ManagerStaffId, StoreId, 5m, null, "NORMAL")).IsSuccess);

            var existing = await ctx.RestockRequests.SingleAsync();
            existing.Status = RestockRequestStatuses.Processing;
            await ctx.SaveChangesAsync();

            var second = await service.CreateFromConfirmedAlertAsync(
                alertId, ManagerStaffId, StoreId, 8m, null, "HIGH");

            Assert.True(second.IsSuccess);
            Assert.True(second.Data!.AlreadyExisted);
            Assert.Equal(1, await ctx.RestockRequests.CountAsync());

            var open = await service.GetOpenByAlertAsync(alertId, StoreId);
            Assert.True(open.IsSuccess);
            Assert.Equal(RestockRequestStatuses.Processing, open.Data!.Status);
        }

        [Fact]
        public async Task NoAccountantWarehouse_StillCreates_WithWarning()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedAlertAsync(ctx, StockAlertStatuses.Confirmed, ingredient: true, withAw: false);
            var service = CreateService(ctx);

            var result = await service.CreateFromConfirmedAlertAsync(
                alertId, ManagerStaffId, StoreId, 12m, "note", "NORMAL");

            Assert.True(result.IsSuccess);
            Assert.Equal(
                result.Data!.NotifiedAccountantWarehouse,
                await ctx.StaffNotifications.AnyAsync(n => n.Type == StaffNotificationTypes.RestockRequestSubmitted));
            Assert.Equal(1, await ctx.RestockRequests.CountAsync());
        }

        [Fact]
        public async Task ListAndDetail_StoreScoped()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedAlertAsync(ctx, StockAlertStatuses.Confirmed, ingredient: true, withAw: false);
            var service = CreateService(ctx);
            await service.CreateFromConfirmedAlertAsync(alertId, ManagerStaffId, StoreId, 3m, null, "NORMAL");

            var list = await service.ListForStoreAsync(StoreId, RestockRequestStatuses.Submitted, 1, 20);
            Assert.True(list.IsSuccess);
            Assert.Equal(1, list.Data!.Total);

            var otherList = await service.ListForStoreAsync(OtherStoreId, null, 1, 20);
            Assert.True(otherList.IsSuccess);
            Assert.Equal(0, otherList.Data!.Total);

            var id = list.Data.Items[0].RestockRequestId;
            var detail = await service.GetDetailAsync(id, StoreId);
            Assert.True(detail.IsSuccess);

            var wrongStore = await service.GetDetailAsync(id, OtherStoreId);
            Assert.False(wrongStore.IsSuccess);
        }

        [Fact]
        public void AdminDeepLink_MapsToRestockRequestDetails()
        {
            var url = CafeChain.Application.Services.Operations.StaffNotificationQueryService.MapAdminTargetUrl(
                StaffNotificationEntityTypes.RestockRequest,
                StaffNotificationTypes.RestockRequestSubmitted,
                77);
            Assert.Equal("/Admin/AdminRestockRequests/Details/77", url);
        }

        // ---------- helpers ----------

        private static RestockRequestService CreateService(CafeChain.Data.AppDbContext ctx) =>
            new(
                ctx,
                new CafeChain.Application.Services.Security.ScopeAuthorizationService(ctx),
                new Mock<ILogger<RestockRequestService>>().Object);

        private async Task<int> SeedAlertAsync(
            CafeChain.Data.AppDbContext ctx,
            string status,
            bool ingredient,
            bool withAw)
        {
            EnsureBase(ctx);
            EnsureStaffWithRole(ctx, ManagerStaffId, RoleConstants.StoreManager, "mgr100@test.local");
            if (withAw)
                EnsureStaffWithRole(ctx, AccountantStaffId, RoleConstants.AccountantWarehouse, "aw100@test.local");

            var alert = new StockAlert
            {
                StoreId = StoreId,
                IngredientId = ingredient ? IngredientId : null,
                RecipeId = ingredient ? null : RecipeId,
                PreparedItemId = ingredient ? null : PreparedItemId,
                AlertType = ingredient ? StockAlertTypes.LowStock : StockAlertTypes.OutOfStock,
                Severity = ingredient ? StockAlertSeverities.Warning : StockAlertSeverities.Urgent,
                Status = status,
                Source = StockAlertSources.ManualCheck,
                CurrentQtySnapshot = 2,
                ThresholdSnapshot = 10,
                ManagerNote = status == StockAlertStatuses.Confirmed ? "Đã xác nhận" : null,
                ConfirmedByStaffId = status == StockAlertStatuses.Confirmed ? ManagerStaffId : null,
                ConfirmedAt = status == StockAlertStatuses.Confirmed ? System.DateTime.UtcNow : null,
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
                    Name = "Store 100",
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
                    Name = "Store 100B",
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
                    Code = "ING100",
                    Name = "Sữa #100",
                    BaseUnitId = UnitId,
                    Active = true
                });
            }

            if (!ctx.Recipes.Any(r => r.RecipeId == RecipeId))
            {
                if (!ctx.PreparedItems.Any(p => p.PreparedItemId == PreparedItemId)
                    && !ctx.PreparedItems.Local.Any(p => p.PreparedItemId == PreparedItemId))
                {
                    ctx.PreparedItems.Add(new PreparedItem
                    {
                        PreparedItemId = PreparedItemId,
                        Code = "PI100",
                        Name = "BTP #100",
                        BaseUnitId = UnitId,
                        Active = true
                    });
                }
                ctx.Recipes.Add(new Recipe
                {
                    RecipeId = RecipeId,
                    RecipeCode = "RCP100",
                    Name = "BTP #100",
                    PreparedItemId = PreparedItemId,
                    OutputQuantity = 1,
                    OutputUnitId = UnitId,
                    Active = true,
                    Status = "Active"
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

            if (!ctx.Roles.Any(r => r.Name == roleName) && !ctx.Roles.Local.Any(r => r.Name == roleName))
            {
                var id = roleName switch
                {
                    RoleConstants.StoreManager => 3,
                    RoleConstants.SalesStaff => 4,
                    RoleConstants.AccountantWarehouse => 5,
                    RoleConstants.ShiftSupervisor => 8,
                    _ => 90
                };
                if (ctx.Roles.Any(r => r.RoleId == id))
                    id = (ctx.Roles.Any() ? ctx.Roles.Max(r => r.RoleId) : 0) + 50;

                ctx.Roles.Add(new Role
                {
                    RoleId = id,
                    Name = roleName,
                    Active = true,
                    IsStoreLevel = true,
                    CreatedAt = System.DateTime.UtcNow
                });
            }

            var role = ctx.Roles.Local.FirstOrDefault(r => r.Name == roleName)
                       ?? ctx.Roles.First(r => r.Name == roleName);

            var accountId = 30000 + staffId;
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
    }
}
