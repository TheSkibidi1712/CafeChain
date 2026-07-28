using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Unit;
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
                alertId, ManagerStaffId, StoreId, 8m, "Cần gấp", "HIGH");

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.False(result.Data!.NotifiedAccountantWarehouse);

            var req = await ctx.RestockRequests.SingleAsync();
            Assert.Equal(RestockRequestStatuses.Draft, req.Status);
            Assert.Equal(IngredientId, req.IngredientId);
            Assert.Null(req.RecipeId);
            Assert.Equal(8m, req.RequestedQuantity);
            Assert.Equal(RestockRequestPriorities.High, req.Priority);
            Assert.Equal(ManagerStaffId, req.CreatedByStaffId);
            Assert.Equal(alertId, req.StockAlertId);
            // Suggested: threshold 10 - current 2 = 8
            Assert.Equal(8m, req.SuggestedQuantity);

            var alert = await ctx.StockAlerts.SingleAsync(a => a.StockAlertId == alertId);
            Assert.Equal(StockAlertStatuses.Confirmed, alert.Status);

            Assert.Empty(ctx.StaffNotifications);
        }

        [Fact]
        public async Task StockAlert_RequestUsesProcurementQuantityAndKeepsBaseSnapshot()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedAlertAsync(
                ctx,
                StockAlertStatuses.Confirmed,
                ingredient: true,
                withAw: false);
            var kilogram = await ctx.Units.SingleAsync(x => x.UnitCode == ProcurementUnitCodes.Kilogram);
            var service = CreateService(ctx);

            var result = await service.CreateFromConfirmedAlertProcurementAsync(
                alertId,
                ManagerStaffId,
                StoreId,
                0.008m,
                kilogram.UnitId,
                "Bổ sung theo cảnh báo",
                "HIGH");

            Assert.True(result.IsSuccess, result.Message);
            var request = await ctx.RestockRequests.SingleAsync();
            Assert.Equal(RestockRequestSourceTypes.StockAlert, request.SourceType);
            Assert.Equal(alertId.ToString(), request.SourceReferenceId);
            Assert.Equal(StoreId, request.CreatedForStoreId);
            Assert.Equal(0.008m, request.RequestedProcurementQuantity);
            Assert.Equal(kilogram.UnitId, request.ProcurementUnitId);
            Assert.Equal(8m, request.RequestedQuantity);
            Assert.Equal(0.010m, request.TargetStockProcurementQuantity);
        }

        [Fact]
        public async Task StoreManager_CreatesFromConfirmed_RecipeBtp()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedAlertAsync(ctx, StockAlertStatuses.Confirmed, ingredient: false, withAw: false);
            var service = CreateService(ctx);

            var result = await service.CreateFromConfirmedAlertAsync(
                alertId, ManagerStaffId, StoreId, 8m, null, null);

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
        public async Task AboveThreshold_ConfirmedAlert_WithZeroSuggestion_IsRejected()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedAlertAsync(ctx, StockAlertStatuses.Confirmed, ingredient: true, withAw: false);
            var alert = await ctx.StockAlerts.SingleAsync(x => x.StockAlertId == alertId);
            alert.CurrentQtySnapshot = 12m;
            alert.ThresholdSnapshot = 10m;
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).CreateFromConfirmedAlertAsync(
                alertId, ManagerStaffId, StoreId, 1m, null, "NORMAL");

            Assert.False(result.IsSuccess);
            Assert.Contains("không còn nhu cầu", result.Message, System.StringComparison.OrdinalIgnoreCase);
            Assert.Empty(await ctx.RestockRequests.ToListAsync());
        }

        [Fact]
        public async Task RequestedQuantity_CannotExceedVerifiedSuggestion()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedAlertAsync(ctx, StockAlertStatuses.Confirmed, ingredient: true, withAw: false);

            var result = await CreateService(ctx).CreateFromConfirmedAlertAsync(
                alertId, ManagerStaffId, StoreId, 8.001m, null, "NORMAL");

            Assert.False(result.IsSuccess);
            Assert.Contains("không được vượt quá", result.Message, System.StringComparison.OrdinalIgnoreCase);
            Assert.Empty(await ctx.RestockRequests.ToListAsync());
        }

        [Fact]
        public async Task ManualReview_UsesDecisionTargetAsVerifiedSuggestion()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedAlertAsync(ctx, StockAlertStatuses.Confirmed, ingredient: true, withAw: false);
            var alert = await ctx.StockAlerts.SingleAsync(x => x.StockAlertId == alertId);
            alert.AlertType = StockAlertTypes.ManualReview;
            alert.Severity = StockAlertSeverities.Review;
            alert.CurrentQtySnapshot = 12m;
            alert.ThresholdSnapshot = 20m;
            await ctx.SaveChangesAsync();

            var result = await CreateService(ctx).CreateFromConfirmedAlertAsync(
                alertId, ManagerStaffId, StoreId, 8m, "Đã xác minh sự kiện", "HIGH");

            Assert.True(result.IsSuccess, result.Message);
            var request = await ctx.RestockRequests.SingleAsync();
            Assert.Equal(8m, request.SuggestedQuantity);
            Assert.Equal(12m, request.SuggestionAvailableSnapshot);
            Assert.Null(request.SuggestionMinLevelSnapshot);
            Assert.Contains("ngoài ngưỡng", request.SuggestionReason);
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
                alertId, ManagerStaffId, StoreId, 8m, "note", "NORMAL");

            Assert.True(result.IsSuccess);
            Assert.Equal(
                result.Data!.NotifiedAccountantWarehouse,
                await ctx.StaffNotifications.AnyAsync(n => n.Type == StaffNotificationTypes.RestockRequestSubmitted));
            Assert.Equal(1, await ctx.RestockRequests.CountAsync());
        }

        [Fact]
        public async Task ManualProcurementDemand_StoresSourceAndProcurementUom()
        {
            using var ctx = CreateDbContext();
            EnsureBase(ctx);
            var manualStaffId = (await ctx.Staffs
                .Select(x => (int?)x.StaffId)
                .MaxAsync() ?? 90000) + 1000;
            EnsureStaffWithRole(ctx, manualStaffId, RoleConstants.StoreManager, "manual-procurement@test.local");
            var sourcingStaffId = manualStaffId + 1;
            EnsureStaffWithRole(ctx, sourcingStaffId, RoleConstants.AccountantWarehouse, "manual-sourcing@test.local");
            ctx.StaffScopes.Add(new StaffScope
            {
                StaffId = sourcingStaffId,
                ScopeTypeId = (int)CafeChain.Application.Interfaces.Security.ScopeLevel.Store,
                ScopeRefId = StoreId
            });
            await ctx.SaveChangesAsync();
            var kg = await ctx.Units.FirstOrDefaultAsync(x => x.UnitCode == "kg");
            if (kg == null)
            {
                kg = new Unit { UnitCode = "kg", Name = "kg", Active = true };
                ctx.Units.Add(kg);
                await ctx.SaveChangesAsync();
            }
            var seededStaff = await ctx.Staffs
                .Include(x => x.Account)
                .ThenInclude(x => x.AccountRoles)
                .ThenInclude(x => x.Role)
                .SingleAsync(x => x.StaffId == manualStaffId);
            Assert.Equal(StoreId, seededStaff.StoreId);
            Assert.Contains(
                seededStaff.Account.AccountRoles.Select(x => x.Role.Name),
                x => x == RoleConstants.StoreManager);

            var conversion = new Mock<IUnitConversionService>();
            conversion.Setup(x => x.ConvertAsync(
                    IngredientId,
                    1.25m,
                    kg.UnitId,
                    It.IsAny<int?>()))
                .ReturnsAsync(ServiceResult<decimal>.Success(1250m));
            var service = new RestockRequestService(
                ctx,
                new CafeChain.Application.Services.Security.ScopeAuthorizationService(ctx),
                new Mock<ILogger<RestockRequestService>>().Object,
                conversion.Object);

            var result = await service.CreateManualAsync(
                new CreateProcurementDemandRequest
                {
                    StoreId = StoreId,
                    IngredientId = IngredientId,
                    RequestedProcurementQuantity = 1.25m,
                    ProcurementUnitId = kg.UnitId,
                    SourceReferenceId = "MANUAL-100-1",
                    NeedByDate = DateTime.UtcNow.AddDays(2),
                    Note = "Bổ sung hạt cà phê"
                },
                manualStaffId);

            Assert.True(result.IsSuccess, result.Message);
            var request = await ctx.RestockRequests.SingleAsync();
            Assert.Equal(RestockRequestSourceTypes.ManualByStore, request.SourceType);
            Assert.Equal(1.25m, request.RequestedProcurementQuantity);
            Assert.Equal(kg.UnitId, request.ProcurementUnitId);
            Assert.Equal(1250m, request.RequestedQuantity);

            var sourcing = await service.SetSourcingDecisionAsync(
                new SourcingDecisionRequest
                {
                    RestockRequestId = request.RestockRequestId,
                    DecisionType = RestockSourcingDecisionTypes.Purchase,
                    ProcurementQuantity = 1.25m,
                    ProcurementUnitId = kg.UnitId,
                    Reason = "Mua theo kế hoạch bổ sung"
                },
                sourcingStaffId);

            Assert.True(sourcing.IsSuccess, sourcing.Message);
            Assert.Equal(RestockSourcingAllocationStatuses.PendingPurchaseAdvice, sourcing.Data!.Status);
            Assert.Equal(
                RestockSourcingStatuses.FullyAllocated,
                (await ctx.RestockRequests.SingleAsync(x => x.RestockRequestId == request.RestockRequestId))
                    .SourcingStatus);
        }

        [Fact]
        public async Task CentralPlanner_RequiresStaffScopeForTargetStore()
        {
            using var ctx = CreateDbContext();
            EnsureBase(ctx);
            var plannerStaffId = (await ctx.Staffs
                .Select(x => (int?)x.StaffId)
                .MaxAsync() ?? 90000) + 2000;
            EnsureStaffWithRole(
                ctx,
                plannerStaffId,
                RoleConstants.AccountantWarehouse,
                "central-planner-scope@test.local");
            var kg = await ctx.Units.FirstOrDefaultAsync(x => x.UnitCode == "kg");
            if (kg == null)
            {
                kg = new Unit { UnitCode = "kg", Name = "kg", Active = true };
                ctx.Units.Add(kg);
            }
            await ctx.SaveChangesAsync();

            var conversion = new Mock<IUnitConversionService>();
            conversion.Setup(x => x.ConvertAsync(
                    IngredientId,
                    2m,
                    kg.UnitId,
                    It.IsAny<int?>()))
                .ReturnsAsync(ServiceResult<decimal>.Success(2000m));
            var service = new RestockRequestService(
                ctx,
                new CafeChain.Application.Services.Security.ScopeAuthorizationService(ctx),
                new Mock<ILogger<RestockRequestService>>().Object,
                conversion.Object);
            var request = new CreateProcurementDemandRequest
            {
                StoreId = StoreId,
                IngredientId = IngredientId,
                RequestedProcurementQuantity = 2m,
                ProcurementUnitId = kg.UnitId,
                SourceReferenceId = "CENTRAL-SCOPE-100-1"
            };

            var withoutScope = await service.CreateCentralPlannerAsync(request, plannerStaffId);
            Assert.False(withoutScope.IsSuccess);
            Assert.Contains("không có quyền", withoutScope.Message, StringComparison.OrdinalIgnoreCase);

            ctx.StaffScopes.Add(new StaffScope
            {
                StaffId = plannerStaffId,
                ScopeTypeId = (int)CafeChain.Application.Interfaces.Security.ScopeLevel.Store,
                ScopeRefId = StoreId
            });
            await ctx.SaveChangesAsync();

            var withScope = await service.CreateCentralPlannerAsync(request, plannerStaffId);
            Assert.True(withScope.IsSuccess, withScope.Message);
            Assert.NotNull(withScope.Data);
            Assert.Equal(2000m, (await ctx.RestockRequests
                .SingleAsync(x => x.RestockRequestId == withScope.Data!.RestockRequestId))
                .RequestedQuantity);
        }

        [Fact]
        public async Task ListAndDetail_StoreScoped()
        {
            using var ctx = CreateDbContext();
            var alertId = await SeedAlertAsync(ctx, StockAlertStatuses.Confirmed, ingredient: true, withAw: false);
            var service = CreateService(ctx);
            await service.CreateFromConfirmedAlertAsync(alertId, ManagerStaffId, StoreId, 3m, null, "NORMAL");

            var list = await service.ListForStoreAsync(StoreId, RestockRequestStatuses.Draft, 1, 20);
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
                    Type = UnitType.KhoiLuong,
                    Active = true
                });
            }
            else
            {
                ctx.Units.Single(u => u.UnitId == UnitId).Type = UnitType.KhoiLuong;
            }

            if (!ctx.Units.Any(u => u.UnitCode == ProcurementUnitCodes.Kilogram)
                && !ctx.Units.Local.Any(u => u.UnitCode == ProcurementUnitCodes.Kilogram))
            {
                ctx.Units.Add(new Unit
                {
                    UnitId = 9100,
                    UnitCode = ProcurementUnitCodes.Kilogram,
                    Name = "Kilogram",
                    Type = UnitType.KhoiLuong,
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
            });
        }
    }
}
