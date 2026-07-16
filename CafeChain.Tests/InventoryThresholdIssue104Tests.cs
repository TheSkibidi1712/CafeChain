using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.Services.Admin.StoreInventories;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
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
    /// <summary>Issue #104 — Admin configure StoreInventory.MinStockLevel + role gates.</summary>
    public class InventoryThresholdIssue104Tests : IntegrationTestBase
    {
        private const int StoreId = 300;
        private const int OtherStoreId = 301;
        private const int IngredientId = 7701;
        private const int RecipeId = 7702;
        private const int ManagerAccountId = 710;
        private const int ManagerStaffId = 711;
        private const int OtherAccountId = 712;
        private const int OtherStaffId = 713;
        private const int AwAccountId = 714;
        private const int AwStaffId = 715;
        private const int SalesAccountId = 716;
        private const int SalesStaffId = 717;
        private const int SsAccountId = 718;
        private const int SsStaffId = 719;
        private const int AmAccountId = 720;
        private const int AmStaffId = 721;
        private const int BoAccountId = 722;
        private const int BoStaffId = 723;
        private const int UnitId = 1;

        [Fact]
        public async Task StoreManager_UpdatesMinStockLevel_OwnStore_Ingredient()
        {
            using var ctx = CreateDbContext();
            EnsureBase(ctx);
            EnsureStaffWithRole(ctx, ManagerAccountId, ManagerStaffId, StoreId,
                RoleConstants.StoreManager, "mgr104@test.local");
            var invId = await SeedInventoryAsync(ctx, StoreId, IngredientId, null, qty: 5m, reserved: 1m, min: null);
            var service = CreateService(ctx);

            var result = await UpdateAsync(service, ctx, ManagerAccountId, invId, 10m);

            Assert.True(result.IsSuccess);
            Assert.Contains("thành công", result.Message);
            var row = await ctx.StoreInventories.SingleAsync(i => i.StoreInventoryId == invId);
            Assert.Equal(10m, row.MinStockLevel);
            Assert.Equal(5m, row.AvailableQty);
            Assert.Equal(1m, row.ReservedQty);
        }

        [Fact]
        public async Task StoreManager_CannotUpdate_OtherStore()
        {
            using var ctx = CreateDbContext();
            EnsureBase(ctx);
            EnsureStaffWithRole(ctx, ManagerAccountId, ManagerStaffId, StoreId,
                RoleConstants.StoreManager, "mgr104b@test.local");
            EnsureStaffWithRole(ctx, OtherAccountId, OtherStaffId, OtherStoreId,
                RoleConstants.StoreManager, "other104@test.local");
            var invId = await SeedInventoryAsync(ctx, OtherStoreId, IngredientId, null, qty: 3m, reserved: 0m, min: null);
            var service = CreateService(ctx);

            var result = await UpdateAsync(service, ctx, ManagerAccountId, invId, 8m);

            Assert.False(result.IsSuccess);
            Assert.Contains("quyền", result.Message);
            Assert.Null((await ctx.StoreInventories.SingleAsync(i => i.StoreInventoryId == invId)).MinStockLevel);
        }

        [Fact]
        public async Task AccountantWarehouse_CannotUpdate()
        {
            using var ctx = CreateDbContext();
            EnsureBase(ctx);
            EnsureStaffWithRole(ctx, AwAccountId, AwStaffId, StoreId,
                RoleConstants.AccountantWarehouse, "aw104@test.local");
            var invId = await SeedInventoryAsync(ctx, StoreId, IngredientId, null, qty: 5m, reserved: 0m, min: null);
            var service = CreateService(ctx);

            var result = await UpdateAsync(service, ctx, AwAccountId, invId, 10m);

            Assert.False(result.IsSuccess);
            Assert.Equal("Bạn không có quyền cập nhật ngưỡng tồn kho.", result.Message);
            Assert.Null((await ctx.StoreInventories.SingleAsync(i => i.StoreInventoryId == invId)).MinStockLevel);
        }

        [Fact]
        public async Task SalesStaff_CannotUpdate()
        {
            using var ctx = CreateDbContext();
            EnsureBase(ctx);
            EnsureStaffWithRole(ctx, SalesAccountId, SalesStaffId, StoreId,
                RoleConstants.SalesStaff, "sales104@test.local");
            var invId = await SeedInventoryAsync(ctx, StoreId, IngredientId, null, qty: 5m, reserved: 0m, min: null);
            var service = CreateService(ctx);

            var result = await UpdateAsync(service, ctx, SalesAccountId, invId, 10m);

            Assert.False(result.IsSuccess);
            Assert.Contains("quyền cập nhật ngưỡng", result.Message);
        }

        [Fact]
        public async Task ShiftSupervisor_CannotUpdate()
        {
            using var ctx = CreateDbContext();
            EnsureBase(ctx);
            EnsureStaffWithRole(ctx, SsAccountId, SsStaffId, StoreId,
                RoleConstants.ShiftSupervisor, "ss104@test.local");
            var invId = await SeedInventoryAsync(ctx, StoreId, IngredientId, null, qty: 5m, reserved: 0m, min: null);
            var service = CreateService(ctx);

            var result = await UpdateAsync(service, ctx, SsAccountId, invId, 10m);

            Assert.False(result.IsSuccess);
            Assert.Contains("quyền cập nhật ngưỡng", result.Message);
        }

        [Fact]
        public async Task AreaManager_CanUpdate_StoreInScope()
        {
            using var ctx = CreateDbContext();
            EnsureBase(ctx);
            EnsureStaffWithRole(ctx, AmAccountId, AmStaffId, storeId: 0,
                RoleConstants.AreaManager, "am104@test.local");
            // Scope only StoreId — not home store
            AddStoreScope(ctx, AmStaffId, StoreId);
            await ctx.SaveChangesAsync();

            var invId = await SeedInventoryAsync(ctx, StoreId, IngredientId, null, qty: 4m, reserved: 0m, min: null);
            var service = CreateService(ctx);

            var result = await UpdateAsync(service, ctx, AmAccountId, invId, 7m);

            Assert.True(result.IsSuccess);
            Assert.Equal(7m, (await ctx.StoreInventories.SingleAsync(i => i.StoreInventoryId == invId)).MinStockLevel);
        }

        [Fact]
        public async Task AreaManager_CannotUpdate_OutsideAssignedScope()
        {
            using var ctx = CreateDbContext();
            EnsureBase(ctx);
            EnsureStaffWithRole(ctx, AmAccountId, AmStaffId, storeId: 0,
                RoleConstants.AreaManager, "am104b@test.local");
            AddStoreScope(ctx, AmStaffId, StoreId); // only StoreId
            await ctx.SaveChangesAsync();

            var invId = await SeedInventoryAsync(ctx, OtherStoreId, IngredientId, null, qty: 4m, reserved: 0m, min: null);
            var service = CreateService(ctx);

            var result = await UpdateAsync(service, ctx, AmAccountId, invId, 7m);

            Assert.False(result.IsSuccess);
            Assert.Contains("quyền", result.Message);
        }

        [Fact]
        public async Task BusinessOwner_WithCountryScope_CanUpdate()
        {
            using var ctx = CreateDbContext();
            EnsureBase(ctx);
            EnsureStaffWithRole(ctx, BoAccountId, BoStaffId, storeId: 0,
                RoleConstants.BusinessOwner, "bo104@test.local");
            AddCountryScope(ctx, BoStaffId);
            await ctx.SaveChangesAsync();

            var invId = await SeedInventoryAsync(ctx, StoreId, IngredientId, null, qty: 1m, reserved: 0m, min: null);
            var service = CreateService(ctx);

            var result = await UpdateAsync(service, ctx, BoAccountId, invId, 3m);

            Assert.True(result.IsSuccess);
            Assert.Equal(3m, (await ctx.StoreInventories.SingleAsync(i => i.StoreInventoryId == invId)).MinStockLevel);
        }

        [Fact]
        public async Task NegativeMinStockLevel_Rejected()
        {
            using var ctx = CreateDbContext();
            EnsureBase(ctx);
            EnsureStaffWithRole(ctx, ManagerAccountId, ManagerStaffId, StoreId,
                RoleConstants.StoreManager, "mgr104c@test.local");
            var invId = await SeedInventoryAsync(ctx, StoreId, IngredientId, null, qty: 5m, reserved: 0m, min: 2m);
            var service = CreateService(ctx);

            var result = await UpdateAsync(service, ctx, ManagerAccountId, invId, -1m);

            Assert.False(result.IsSuccess);
            Assert.Contains("không được âm", result.Message);
            Assert.Equal(2m, (await ctx.StoreInventories.SingleAsync(i => i.StoreInventoryId == invId)).MinStockLevel);
        }

        [Fact]
        public async Task MissingRowVersion_Rejected()
        {
            using var ctx = CreateDbContext();
            EnsureBase(ctx);
            EnsureStaffWithRole(ctx, ManagerAccountId, ManagerStaffId, StoreId,
                RoleConstants.StoreManager, "mgr104-version@test.local");
            var invId = await SeedInventoryAsync(ctx, StoreId, IngredientId, null, 5m, 0m, 2m);
            var service = CreateService(ctx);

            var result = await service.UpdateMinStockLevelAsync(
                ManagerAccountId, invId, 4m, null);

            Assert.False(result.IsSuccess);
            Assert.Equal("VALIDATION_ROW_VERSION_REQUIRED", result.ErrorCode);
        }

        [Fact]
        public async Task StaleRowVersion_Rejected()
        {
            using var ctx = CreateDbContext();
            EnsureBase(ctx);
            EnsureStaffWithRole(ctx, ManagerAccountId, ManagerStaffId, StoreId,
                RoleConstants.StoreManager, "mgr104-stale@test.local");
            var invId = await SeedInventoryAsync(ctx, StoreId, IngredientId, null, 5m, 0m, 2m);
            var service = CreateService(ctx);

            var result = await service.UpdateMinStockLevelAsync(
                ManagerAccountId,
                invId,
                4m,
                System.Convert.ToBase64String(new byte[] { 9 }));

            Assert.False(result.IsSuccess);
            Assert.Equal("RESOURCE_CHANGED_BY_ANOTHER_USER", result.ErrorCode);
            Assert.Equal(2m, (await ctx.StoreInventories.SingleAsync(i => i.StoreInventoryId == invId)).MinStockLevel);
        }

        [Fact]
        public async Task Null_ClearsThreshold()
        {
            using var ctx = CreateDbContext();
            EnsureBase(ctx);
            EnsureStaffWithRole(ctx, ManagerAccountId, ManagerStaffId, StoreId,
                RoleConstants.StoreManager, "mgr104d@test.local");
            var invId = await SeedInventoryAsync(ctx, StoreId, IngredientId, null, qty: 5m, reserved: 0m, min: 12m);
            var service = CreateService(ctx);

            var result = await UpdateAsync(service, ctx, ManagerAccountId, invId, null);

            Assert.True(result.IsSuccess);
            Assert.Null((await ctx.StoreInventories.SingleAsync(i => i.StoreInventoryId == invId)).MinStockLevel);
        }

        [Fact]
        public async Task RecipeBtp_RowSupported()
        {
            using var ctx = CreateDbContext();
            EnsureBase(ctx);
            EnsureStaffWithRole(ctx, ManagerAccountId, ManagerStaffId, StoreId,
                RoleConstants.StoreManager, "mgr104e@test.local");
            var invId = await SeedInventoryAsync(ctx, StoreId, null, RecipeId, qty: 2m, reserved: 0m, min: null);
            var service = CreateService(ctx);

            var result = await UpdateAsync(service, ctx, ManagerAccountId, invId, 5m);

            Assert.True(result.IsSuccess);
            var row = await ctx.StoreInventories.SingleAsync(i => i.StoreInventoryId == invId);
            Assert.Equal(RecipeId, row.RecipeId);
            Assert.Equal(5m, row.MinStockLevel);
            Assert.Equal(2m, row.AvailableQty);
        }

        [Fact]
        public async Task AfterThresholdSet_ManualCheck_CreatesLowStockAlert()
        {
            using var ctx = CreateDbContext();
            EnsureBase(ctx);
            EnsureStaffWithRole(ctx, ManagerAccountId, ManagerStaffId, StoreId,
                RoleConstants.StoreManager, "mgr104f@test.local");
            var invId = await SeedInventoryAsync(ctx, StoreId, IngredientId, null, qty: 2m, reserved: 0m, min: null);
            var thresholdService = CreateService(ctx);
            Assert.True((await UpdateAsync(thresholdService, ctx, ManagerAccountId, invId, 10m)).IsSuccess);

            var alertService = new StockAlertService(
                ctx, new Mock<ILogger<StockAlertService>>().Object);
            var eval = await alertService.EvaluateStoreInventoryItemAsync(
                invId, StockAlertSources.ManualCheck);

            Assert.True(eval.IsSuccess);
            var alert = await ctx.StockAlerts.SingleOrDefaultAsync(a =>
                a.StoreId == StoreId &&
                a.IngredientId == IngredientId &&
                a.Status == StockAlertStatuses.Open);
            Assert.NotNull(alert);
            Assert.Equal(StockAlertTypes.LowStock, alert!.AlertType);
            Assert.Equal(2m, (await ctx.StoreInventories.SingleAsync(i => i.StoreInventoryId == invId)).AvailableQty);
        }

        [Fact]
        public async Task List_OnlyShowsAccessibleStore()
        {
            using var ctx = CreateDbContext();
            EnsureBase(ctx);
            EnsureStaffWithRole(ctx, ManagerAccountId, ManagerStaffId, StoreId,
                RoleConstants.StoreManager, "mgr104g@test.local");
            await SeedInventoryAsync(ctx, StoreId, IngredientId, null, qty: 1m, reserved: 0m, min: null);
            if (!ctx.Ingredients.Any(i => i.IngredientId == IngredientId + 1))
            {
                ctx.Ingredients.Add(new Ingredient
                {
                    IngredientId = IngredientId + 1,
                    Code = "ING104B",
                    Name = "Other",
                    BaseUnitId = UnitId,
                    Active = true
                });
                await ctx.SaveChangesAsync();
            }

            await SeedInventoryAsync(ctx, OtherStoreId, IngredientId + 1, null, qty: 9m, reserved: 0m, min: null);

            var service = CreateService(ctx);
            var list = await service.ListAsync(ManagerAccountId, StoreId, null, 1, 50);

            Assert.True(list.IsSuccess);
            Assert.All(list.Data!.Items, i => Assert.Equal(StoreId, i.StoreId));
            Assert.DoesNotContain(list.Data.Items, i => i.StoreId == OtherStoreId);
        }

        [Fact]
        public async Task AccountHasEditRole_MatchesAllowList()
        {
            using var ctx = CreateDbContext();
            EnsureBase(ctx);
            EnsureStaffWithRole(ctx, ManagerAccountId, ManagerStaffId, StoreId,
                RoleConstants.StoreManager, "mgr104h@test.local");
            EnsureStaffWithRole(ctx, AwAccountId, AwStaffId, StoreId,
                RoleConstants.AccountantWarehouse, "aw104h@test.local");
            var service = CreateService(ctx);

            Assert.True(await service.AccountHasEditRoleAsync(ManagerAccountId));
            Assert.False(await service.AccountHasEditRoleAsync(AwAccountId));
        }

        // ---------- helpers ----------

        private static InventoryThresholdService CreateService(CafeChain.Data.AppDbContext ctx) =>
            new(ctx, new Mock<ILogger<InventoryThresholdService>>().Object);

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
                    Name = "Store 104",
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
                    Name = "Store 104B",
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
                    Code = "ING104",
                    Name = "Đường #104",
                    BaseUnitId = UnitId,
                    Active = true
                });
            }

            if (!ctx.Recipes.Any(r => r.RecipeId == RecipeId))
            {
                ctx.Recipes.Add(new Recipe
                {
                    RecipeId = RecipeId,
                    RecipeCode = "RCP104",
                    Name = "BTP #104",
                    Active = true,
                    Status = "Active"
                });
            }

            EnsureScopeTypes(ctx);
            ctx.SaveChanges();
        }

        private static void EnsureScopeTypes(CafeChain.Data.AppDbContext ctx)
        {
            // ScopeType: 1 Country, 4 Store (matches AdminStoreInventoryRepository)
            if (!ctx.ScopeTypes.Any(s => s.ScopeTypeId == 1))
            {
                ctx.ScopeTypes.Add(new ScopeType
                {
                    ScopeTypeId = 1,
                    Code = "COUNTRY",
                    Name = "Country"
                });
            }

            if (!ctx.ScopeTypes.Any(s => s.ScopeTypeId == 4))
            {
                ctx.ScopeTypes.Add(new ScopeType
                {
                    ScopeTypeId = 4,
                    Code = "STORE",
                    Name = "Store"
                });
            }
        }

        private static void EnsureStaffWithRole(
            CafeChain.Data.AppDbContext ctx,
            int accountId,
            int staffId,
            int storeId,
            string roleName,
            string email)
        {
            if (ctx.Staffs.Any(s => s.StaffId == staffId)) return;

            EnsureRole(ctx, roleName);
            var role = ctx.Roles.Local.FirstOrDefault(r => r.Name == roleName)
                       ?? ctx.Roles.First(r => r.Name == roleName);

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
                StoreId = storeId,
                FullName = $"Staff {staffId}",
                Active = true,
                CreatedAt = System.DateTime.UtcNow,
                BaseSalary = 0
            });
            ctx.SaveChanges();
        }

        private static void EnsureRole(CafeChain.Data.AppDbContext ctx, string roleName)
        {
            if (ctx.Roles.Any(r => r.Name == roleName) || ctx.Roles.Local.Any(r => r.Name == roleName))
                return;

            var id = roleName switch
            {
                RoleConstants.BusinessOwner => 1,
                RoleConstants.AreaManager => 2,
                RoleConstants.StoreManager => 3,
                RoleConstants.SalesStaff => 4,
                RoleConstants.AccountantWarehouse => 5,
                RoleConstants.SystemAdmin => 6,
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

        private static void AddStoreScope(CafeChain.Data.AppDbContext ctx, int staffId, int storeId)
        {
            EnsureScopeTypes(ctx);
            ctx.StaffScopes.Add(new StaffScope
            {
                StaffId = staffId,
                ScopeTypeId = 4,
                ScopeRefId = storeId
            });
        }

        private static void AddCountryScope(CafeChain.Data.AppDbContext ctx, int staffId)
        {
            EnsureScopeTypes(ctx);
            ctx.StaffScopes.Add(new StaffScope
            {
                StaffId = staffId,
                ScopeTypeId = 1,
                ScopeRefId = 1
            });
        }

        private static async Task<int> SeedInventoryAsync(
            CafeChain.Data.AppDbContext ctx,
            int storeId,
            int? ingredientId,
            int? recipeId,
            decimal qty,
            decimal reserved,
            decimal? min)
        {
            var row = new StoreInventory
            {
                StoreId = storeId,
                IngredientId = ingredientId,
                RecipeId = recipeId,
                AvailableQty = qty,
                ReservedQty = reserved,
                MinStockLevel = min,
                LastUpdated = System.DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            };
            ctx.StoreInventories.Add(row);
            await ctx.SaveChangesAsync();
            return row.StoreInventoryId;
        }

        private static async Task<CafeChain.Application.Results.ServiceResult> UpdateAsync(
            InventoryThresholdService service,
            CafeChain.Data.AppDbContext context,
            int accountId,
            int storeInventoryId,
            decimal? minStockLevel)
        {
            var rowVersion = await context.StoreInventories
                .AsNoTracking()
                .Where(x => x.StoreInventoryId == storeInventoryId)
                .Select(x => x.RowVersion)
                .SingleAsync();

            return await service.UpdateMinStockLevelAsync(
                accountId,
                storeInventoryId,
                minStockLevel,
                System.Convert.ToBase64String(rowVersion));
        }
    }
}
