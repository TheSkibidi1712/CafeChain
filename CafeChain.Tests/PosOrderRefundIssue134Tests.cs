using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Admin.Production;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.POS;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Configuration;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Refunds;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests
{
    /// <summary>Issue #134 — full-order cash refund inventory + compensating COGS layers.</summary>
    public sealed class PosOrderRefundIssue134Tests : IntegrationTestBase
    {
        private const int StoreId = 13401;
        private const int OtherStoreId = 13402;
        private const int StaffId = 13410;
        private const int UnitGram = 1;
        private const int UnitMl = 3;
        private const int DrinkId = 13420;
        private const int SizeId = 13421;
        private const int IngredientId = 13430;
        private const int PreparedItemId = 13440;
        private const int ChildRecipeId = 13450;
        private const int ParentRecipeId = 13451;
        private const int OrderId = 13490;

        private static readonly string[] ManagerRoles = { RoleConstants.StoreManager };
        private static readonly string[] SalesRoles = { RoleConstants.SalesStaff };
        private static readonly string[] BoRoles = { RoleConstants.BusinessOwner };

        [Fact]
        public async Task POS_Refund_Request_DoesNotMutateInventoryOrPayment()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true);
            var invBefore = await QtyIng(ctx);
            var svc = CreateRefundSvc(ctx);

            var req = await svc.RequestFullRefundAsync(Req(), StaffId, StoreId, ManagerRoles);
            Assert.True(req.IsSuccess, req.Message);
            Assert.Equal(invBefore, await QtyIng(ctx));
            Assert.Equal(SystemConstants.PaymentStatuses.Paid,
                (await ctx.Orders.SingleAsync(o => o.OrderId == OrderId)).PaymentStatusId);
            Assert.Equal(OrderRefundStatus.Requested,
                (await ctx.OrderRefunds.SingleAsync()).Status);
        }

        [Fact]
        public async Task POS_Refund_FullCashOrder_RestoresInventoryQuantity()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true);
            var afterSale = await QtyIng(ctx);
            await RequestAndConfirmAsync(ctx);
            Assert.Equal(afterSale + 50m, await QtyIng(ctx));
        }

        [Fact]
        public async Task POS_Refund_FullCashOrder_CreatesSalesReturnTransactions()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true);
            await RequestAndConfirmAsync(ctx);
            Assert.True(await ctx.InventoryTransactions.AnyAsync(t =>
                t.ReferenceOrderId == OrderId && t.Type == InventoryTransactionTypeEnum.SALES_RETURN));
        }

        [Fact]
        public async Task POS_Refund_CreatesCompensatingIngredientLayers()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true, layerCost: 12m);
            await RequestAndConfirmAsync(ctx);
            var refund = await ctx.OrderRefunds.SingleAsync();
            var layer = await ctx.InventoryCostLayers.SingleAsync(x => x.SourceOrderRefundId == refund.OrderRefundId);
            Assert.Equal(IngredientId, layer.IngredientId);
            Assert.Equal(50m, layer.RemainingQuantity);
            Assert.Equal(12m, layer.UnitCost);
        }

        [Fact]
        public async Task POS_Refund_CreatesCompensatingPreparedItemLayers()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithPreparedItemSaleAsync(ctx);
            await RequestAndConfirmAsync(ctx);
            var refund = await ctx.OrderRefunds.SingleAsync();
            var layer = await ctx.InventoryCostLayers.SingleAsync(x => x.SourceOrderRefundId == refund.OrderRefundId);
            Assert.Equal(PreparedItemId, layer.PreparedItemId);
            Assert.Equal(100m, layer.Quantity);
            Assert.Equal(20m, layer.UnitCost);
        }

        [Fact]
        public async Task POS_Refund_UsesOriginalSalesAllocationUnitCost()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true, layerCost: 7.5m);
            await RequestAndConfirmAsync(ctx);
            var rev = await ctx.RefundCostReversals.SingleAsync();
            Assert.Equal(7.5m, rev.UnitCost);
            Assert.Equal(7.5m * 50m, rev.TotalCost);
        }

        [Fact]
        public async Task POS_Refund_DoesNotUseCurrentSupplierOrProductionPrice()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true, layerCost: 10m);
            // poison remaining original layer
            var orig = await ctx.InventoryCostLayers.FirstAsync(x => x.SourceOrderRefundId == null);
            orig.UnitCost = 999m;
            await ctx.SaveChangesAsync();
            await RequestAndConfirmAsync(ctx);
            var returnLayer = await ctx.InventoryCostLayers.SingleAsync(x => x.SourceOrderRefundId != null);
            Assert.Equal(10m, returnLayer.UnitCost);
        }

        [Fact]
        public async Task POS_Refund_CreatesDurableCostReversals()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true);
            await RequestAndConfirmAsync(ctx);
            Assert.Equal(1, await ctx.RefundCostReversals.CountAsync());
            var rev = await ctx.RefundCostReversals.SingleAsync();
            Assert.True(rev.SalesCostAllocationId > 0);
            Assert.True(rev.ReturnInventoryCostLayerId > 0);
        }

        [Fact]
        public async Task POS_Refund_IncompleteOriginalCogs_CreatesRefundCostGaps()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true, layerQty: 10m); // incomplete
            await RequestAndConfirmAsync(ctx);
            Assert.True(await ctx.RefundCostGaps.AnyAsync());
            Assert.Equal(SalesCostStatus.Incomplete, (await ctx.OrderRefunds.SingleAsync()).CostStatus);
        }

        [Fact]
        public async Task POS_Refund_IncompleteOriginalCogs_DoesNotCreateZeroCostLayer()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true, layerQty: 10m);
            await RequestAndConfirmAsync(ctx);
            Assert.False(await ctx.InventoryCostLayers.AnyAsync(x =>
                x.SourceOrderRefundId != null && x.UnitCost <= 0));
        }

        [Fact]
        public async Task POS_Refund_Replay_DoesNotRestoreInventoryTwice()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true);
            var afterSale = await QtyIng(ctx);
            var svc = CreateRefundSvc(ctx);
            var req = await svc.RequestFullRefundAsync(Req(), StaffId, StoreId, ManagerRoles);
            var c1 = await svc.ConfirmCashRefundAsync(Confirm(req.Data!.OrderRefundId), StaffId, StoreId, ManagerRoles);
            Assert.True(c1.IsSuccess, c1.Message);
            var mid = await QtyIng(ctx);
            var c2 = await svc.ConfirmCashRefundAsync(Confirm(req.Data.OrderRefundId), StaffId, StoreId, ManagerRoles);
            Assert.True(c2.IsSuccess);
            Assert.True(c2.Data!.WasReplay);
            Assert.Equal(mid, await QtyIng(ctx));
            Assert.Equal(afterSale + 50m, await QtyIng(ctx));
        }

        [Fact]
        public async Task POS_Refund_Replay_DoesNotDuplicateReturnLayers()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true);
            var svc = CreateRefundSvc(ctx);
            var req = await svc.RequestFullRefundAsync(Req(), StaffId, StoreId, ManagerRoles);
            await svc.ConfirmCashRefundAsync(Confirm(req.Data!.OrderRefundId), StaffId, StoreId, ManagerRoles);
            await svc.ConfirmCashRefundAsync(Confirm(req.Data.OrderRefundId), StaffId, StoreId, ManagerRoles);
            Assert.Equal(1, await ctx.InventoryCostLayers.CountAsync(x => x.SourceOrderRefundId != null));
            Assert.Equal(1, await ctx.RefundCostReversals.CountAsync());
        }

        [Fact]
        public async Task POS_Refund_Replay_ReturnsStoredSnapshot()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true, layerCost: 11m);
            var svc = CreateRefundSvc(ctx);
            var req = await svc.RequestFullRefundAsync(Req(), StaffId, StoreId, ManagerRoles);
            var first = await svc.ConfirmCashRefundAsync(Confirm(req.Data!.OrderRefundId), StaffId, StoreId, ManagerRoles);
            var second = await svc.ConfirmCashRefundAsync(Confirm(req.Data.OrderRefundId), StaffId, StoreId, ManagerRoles);
            Assert.Equal(first.Data!.ReversedCogs, second.Data!.ReversedCogs);
            Assert.Equal(first.Data.RefundAmount, second.Data.RefundAmount);
        }

        [Fact]
        public async Task POS_Refund_PreservesOriginalOrderCogsAndPrices()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true, layerCost: 10m);
            var orderBefore = await ctx.Orders.AsNoTracking().SingleAsync(o => o.OrderId == OrderId);
            var detailBefore = await ctx.OrderDetails.AsNoTracking().SingleAsync(d => d.OrderId == OrderId);
            var cogsBefore = orderBefore.TotalCogs;
            var priceBefore = detailBefore.Price;
            await RequestAndConfirmAsync(ctx);
            var orderAfter = await ctx.Orders.AsNoTracking().SingleAsync(o => o.OrderId == OrderId);
            var detailAfter = await ctx.OrderDetails.AsNoTracking().SingleAsync(d => d.OrderId == OrderId);
            Assert.Equal(cogsBefore, orderAfter.TotalCogs);
            Assert.Equal(priceBefore, detailAfter.Price);
            Assert.Equal(SystemConstants.OrderStatuses.Completed, orderAfter.OrderStatusId);
        }

        [Fact]
        public async Task POS_Refund_SetsPaymentRefunded_AndKeepsOrderCompleted()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true);
            await RequestAndConfirmAsync(ctx);
            var order = await ctx.Orders.SingleAsync(o => o.OrderId == OrderId);
            Assert.Equal(SystemConstants.OrderStatuses.Completed, order.OrderStatusId);
            Assert.Equal(SystemConstants.PaymentStatuses.Refunded, order.PaymentStatusId);
            Assert.All(await ctx.Payments.Where(p => p.OrderId == OrderId).ToListAsync(),
                p => Assert.Equal(SystemConstants.PaymentStatuses.Refunded, p.PaymentStatusId));
        }

        [Fact]
        public async Task POS_Refund_NoOriginalDeduction_DoesNotIncreaseInventory()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: false);
            var before = await QtyIng(ctx);
            await RequestAndConfirmAsync(ctx);
            Assert.Equal(before, await QtyIng(ctx));
            Assert.Equal(RefundInventoryReversalStatus.NoOriginalDeduction,
                (await ctx.OrderRefunds.SingleAsync()).InventoryReversalStatus);
            Assert.Equal(0, await ctx.InventoryTransactions.CountAsync(t =>
                t.Type == InventoryTransactionTypeEnum.SALES_RETURN));
        }

        [Fact]
        public async Task POS_Refund_WinningBeforeRepair_PreventsLaterDeduction()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: false);
            await RequestAndConfirmAsync(ctx);
            var deduct = await CreateDeductSvc(ctx).DeductStockForCommittedOrderAsync(
                Sold(), StoreId, OrderId);
            Assert.False(deduct.IsSuccess);
            Assert.Equal(0, await ctx.InventoryTransactions.CountAsync(t =>
                t.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION && t.ReferenceOrderId == OrderId));
        }

        [Fact]
        public async Task POS_Refund_DeductionWinningFirst_IsReversedOnce()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true);
            await RequestAndConfirmAsync(ctx);
            Assert.Equal(1, await ctx.InventoryTransactions.CountAsync(t =>
                t.Type == InventoryTransactionTypeEnum.SALES_RETURN && t.ReferenceOrderId == OrderId));
        }

        [Fact]
        public async Task POS_Refund_ToppingPreparedItem_RestoresCanonicalIdentity()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithPreparedItemSaleAsync(ctx);
            var before = await ctx.StoreInventories.Where(x => x.PreparedItemId == PreparedItemId)
                .Select(x => x.AvailableQty).SingleAsync();
            await RequestAndConfirmAsync(ctx);
            var after = await ctx.StoreInventories.Where(x => x.PreparedItemId == PreparedItemId)
                .Select(x => x.AvailableQty).SingleAsync();
            Assert.Equal(before + 100m, after);
            Assert.True(await ctx.InventoryCostLayers.AnyAsync(x =>
                x.PreparedItemId == PreparedItemId && x.SourceOrderRefundId != null));
        }

        [Fact]
        public async Task POS_Refund_LoyaltyOrVoucherOrder_IsRejectedUntilSupported()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true);
            var order = await ctx.Orders.SingleAsync(o => o.OrderId == OrderId);
            order.PointsUsed = 10;
            order.PointDiscount = 10000m;
            await ctx.SaveChangesAsync();
            var r = await CreateRefundSvc(ctx).RequestFullRefundAsync(Req(), StaffId, StoreId, ManagerRoles);
            Assert.False(r.IsSuccess);
            Assert.Equal(OrderRefundFailureCodes.LoyaltyReversalNotSupported, r.ErrorCode);
        }

        [Fact]
        public async Task POS_Refund_PayOSOrder_IsRejectedUntilProviderSupport()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true);
            var pay = await ctx.Payments.SingleAsync(p => p.OrderId == OrderId);
            pay.PaymentMethodId = 2;
            await ctx.SaveChangesAsync();
            var r = await CreateRefundSvc(ctx).RequestFullRefundAsync(Req(), StaffId, StoreId, ManagerRoles);
            Assert.False(r.IsSuccess);
            Assert.Equal(OrderRefundFailureCodes.PaymentProviderNotSupported, r.ErrorCode);
        }

        [Fact]
        public async Task POS_Refund_PartialAmount_IsRejected()
        {
            // Full amount only — request always uses order.Total; confirm rejects mismatch via stored amount.
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true);
            var svc = CreateRefundSvc(ctx);
            var req = await svc.RequestFullRefundAsync(Req(), StaffId, StoreId, ManagerRoles);
            Assert.True(req.IsSuccess);
            var refund = await ctx.OrderRefunds.SingleAsync();
            refund.RefundAmount = refund.RefundAmount - 1m;
            await ctx.SaveChangesAsync();
            var conf = await svc.ConfirmCashRefundAsync(Confirm(refund.OrderRefundId), StaffId, StoreId, ManagerRoles);
            Assert.False(conf.IsSuccess);
            Assert.Equal(OrderRefundFailureCodes.PartialAmountRejected, conf.ErrorCode);
        }

        [Fact]
        public async Task POS_Refund_UnauthorizedRole_IsRejected()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true);
            var r = await CreateRefundSvc(ctx).RequestFullRefundAsync(Req(), StaffId, StoreId, SalesRoles);
            Assert.False(r.IsSuccess);
            Assert.Equal(OrderRefundFailureCodes.RoleUnauthorized, r.ErrorCode);
        }

        [Fact]
        public async Task POS_Refund_WrongStoreActor_IsRejected()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true);
            var r = await CreateRefundSvc(ctx).RequestFullRefundAsync(Req(), StaffId, OtherStoreId, ManagerRoles);
            Assert.False(r.IsSuccess);
            Assert.Equal(OrderRefundFailureCodes.StoreUnauthorized, r.ErrorCode);
        }

        [Fact]
        public async Task POS_Refund_Failure_RollsBackAllArtifacts()
        {
            using var ctx = CreateDbContext();
            await SeedCashOrderWithIngredientSaleAsync(ctx, deduct: true);
            var before = await QtyIng(ctx);
            var svc = CreateRefundSvc(ctx);
            // Confirm without request
            var conf = await svc.ConfirmCashRefundAsync(Confirm(99999), StaffId, StoreId, ManagerRoles);
            Assert.False(conf.IsSuccess);
            Assert.Equal(before, await QtyIng(ctx));
            Assert.Equal(0, await ctx.RefundCostReversals.CountAsync());
            Assert.Equal(SystemConstants.PaymentStatuses.Paid,
                (await ctx.Orders.SingleAsync(o => o.OrderId == OrderId)).PaymentStatusId);
        }

        // ---------- helpers ----------

        private static RequestFullOrderRefundDto Req(Guid? key = null) => new()
        {
            OrderId = OrderId,
            RefundKey = key ?? Guid.NewGuid(),
            Reason = "Khách đổi ý"
        };

        private static ConfirmCashRefundDto Confirm(int id) => new()
        {
            OrderRefundId = id,
            CashReturnedToCustomer = true,
            Reason = "Đã trả tiền mặt"
        };

        private static List<POSSoldItemDto> Sold() => new()
        {
            new() { DrinkId = DrinkId, SizeId = SizeId, Quantity = 1 }
        };

        private static async Task RequestAndConfirmAsync(AppDbContext ctx)
        {
            var svc = CreateRefundSvc(ctx);
            var req = await svc.RequestFullRefundAsync(Req(), StaffId, StoreId, ManagerRoles);
            Assert.True(req.IsSuccess, req.Message);
            var conf = await svc.ConfirmCashRefundAsync(Confirm(req.Data!.OrderRefundId), StaffId, StoreId, ManagerRoles);
            Assert.True(conf.IsSuccess, conf.Message);
        }

        private static async Task<decimal> QtyIng(AppDbContext ctx)
            => await ctx.StoreInventories.Where(x => x.IngredientId == IngredientId)
                .Select(x => x.AvailableQty).SingleAsync();

        private static OrderRefundService CreateRefundSvc(AppDbContext ctx)
            => new(ctx, NullLogger<OrderRefundService>.Instance);

        private static InventoryDeductionService CreateDeductSvc(AppDbContext ctx)
        {
            var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
            var unit = new UnitConversionService(ctx, NullLogger<UnitConversionService>.Instance, physical);
            var normalizer = new RecipeOutputNormalizer(ctx, physical);
            var estimated = new EstimatedBomCostService(ctx, unit, physical, normalizer, NullLogger<EstimatedBomCostService>.Instance);
            var caps = new IInventoryWriterCapabilityProvider[]
            {
                new ProductionPreparedWriterCapabilityProvider(),
                new PosPreparedWriterCapabilityProvider()
            };
            var writer = new InventoryWriterModeService(ctx, physical, caps);
            return new InventoryDeductionService(
                ctx, NullLogger<InventoryDeductionService>.Instance, unit, estimated, physical,
                null, writer, new StoreInventoryWriteResolver(ctx, writer),
                new InventoryCostLayerConsumptionService(ctx));
        }

        private static async Task SeedBaseAsync(AppDbContext ctx)
        {
            var now = DateTime.UtcNow;
            ctx.Stores.Add(new Store
            {
                StoreId = StoreId, Name = "S134", Address = "A", Phone = "1", Active = true, CreatedAt = now
            });
            ctx.StoreInventoryWriterConfigurations.Add(new StoreInventoryWriterConfiguration
            {
                StoreId = StoreId,
                WriterMode = InventoryWriterMode.PreparedItem,
                HasEverActivatedPreparedItem = true,
                CreatedAt = now,
                UpdatedAt = now,
                RowVersion = new byte[] { 0 }
            });
            ctx.Staffs.Add(new Staff
            {
                StaffId = StaffId, AccountId = StaffId, FullName = "Mgr", StoreId = StoreId, Active = true, CreatedAt = now
            });
            await ctx.SaveChangesAsync();
        }

        private static async Task SeedCashOrderWithIngredientSaleAsync(
            AppDbContext ctx,
            bool deduct,
            decimal layerCost = 10m,
            decimal layerQty = 1000m)
        {
            await SeedBaseAsync(ctx);
            var now = DateTime.UtcNow;
            ctx.Ingredients.Add(new Ingredient
            {
                IngredientId = IngredientId, Code = "I134", Name = "Milk", BaseUnitId = UnitGram, Active = true
            });
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = ParentRecipeId,
                RecipeCode = "R134",
                Name = "Drink",
                Active = true,
                Status = "Active",
                DrinkId = DrinkId,
                SizeId = SizeId,
                YieldPercentage = 100m,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { IngredientId = IngredientId, Quantity = 50m, UnitId = UnitGram }
                }
            });
            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                IngredientId = IngredientId,
                AvailableQty = 1000m,
                ReservedQty = 0,
                LastUpdated = now,
                RowVersion = new byte[] { 0 }
            });
            if (layerQty > 0)
            {
                ctx.InventoryCostLayers.Add(new InventoryCostLayer
                {
                    StoreId = StoreId,
                    IngredientId = IngredientId,
                    Quantity = layerQty,
                    RemainingQuantity = layerQty,
                    UnitCost = layerCost,
                    CreatedAt = now
                });
            }

            ctx.Orders.Add(new Order
            {
                OrderId = OrderId,
                StoreId = StoreId,
                OrderStatusId = SystemConstants.OrderStatuses.Completed,
                PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                OrderTypeId = SystemConstants.OrderTypes.DineIn,
                Source = "POS",
                SubTotal = 45000m,
                Total = 45000m,
                CostStatus = SalesCostStatus.Pending,
                CreatedAt = now,
                StaffId = StaffId
            });
            ctx.Payments.Add(new Payment
            {
                OrderId = OrderId,
                PaymentMethodId = 1,
                Amount = 45000m,
                PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                PaidAt = now
            });
            ctx.OrderDetails.Add(new OrderDetail
            {
                OrderId = OrderId,
                DrinkId = DrinkId,
                SizeId = SizeId,
                DrinkName = "Latte",
                SizeName = "M",
                Price = 45000m,
                Quantity = 1,
                Note = "",
                CostStatus = SalesCostStatus.Pending
            });
            await ctx.SaveChangesAsync();

            if (deduct)
            {
                var d = await CreateDeductSvc(ctx).DeductStockForCommittedOrderAsync(Sold(), StoreId, OrderId);
                Assert.True(d.IsSuccess, d.Message);
            }
        }

        private static async Task SeedCashOrderWithPreparedItemSaleAsync(AppDbContext ctx)
        {
            await SeedBaseAsync(ctx);
            var now = DateTime.UtcNow;
            ctx.PreparedItems.Add(new PreparedItem
            {
                PreparedItemId = PreparedItemId,
                Code = "PI134",
                Name = "Syrup",
                BaseUnitId = UnitMl,
                Active = true
            });
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = ChildRecipeId,
                RecipeCode = "CH134",
                Name = "Child",
                Active = false,
                Status = "Archived",
                YieldPercentage = 100m,
                PreparedItemId = PreparedItemId,
                OutputQuantity = 1m,
                OutputUnitId = UnitMl
            });
            ctx.Recipes.Add(new Recipe
            {
                RecipeId = ParentRecipeId,
                RecipeCode = "PR134",
                Name = "Drink PI",
                Active = true,
                Status = "Active",
                DrinkId = DrinkId,
                SizeId = SizeId,
                YieldPercentage = 100m,
                RecipeDetails = new List<RecipeDetail>
                {
                    new() { ChildRecipeId = ChildRecipeId, Quantity = 100m, UnitId = UnitMl }
                }
            });
            ctx.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                PreparedItemId = PreparedItemId,
                RecipeId = null,
                BtpIdentityState = BtpIdentityState.Canonical,
                QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
                QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
                QuantitySemanticsEvidenceReference = "seed",
                QuantitySemanticsReviewedAt = now,
                QuantitySemanticsReviewedByAccountId = StaffId,
                AvailableQty = 5000m,
                ReservedQty = 0,
                LastUpdated = now,
                RowVersion = new byte[] { 0 }
            });
            ctx.InventoryCostLayers.Add(new InventoryCostLayer
            {
                StoreId = StoreId,
                PreparedItemId = PreparedItemId,
                IngredientId = null,
                Quantity = 5000m,
                RemainingQuantity = 5000m,
                UnitCost = 20m,
                CreatedAt = now
            });
            ctx.Orders.Add(new Order
            {
                OrderId = OrderId,
                StoreId = StoreId,
                OrderStatusId = SystemConstants.OrderStatuses.Completed,
                PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                OrderTypeId = SystemConstants.OrderTypes.DineIn,
                Source = "POS",
                SubTotal = 45000m,
                Total = 45000m,
                CostStatus = SalesCostStatus.Pending,
                CreatedAt = now,
                StaffId = StaffId
            });
            ctx.Payments.Add(new Payment
            {
                OrderId = OrderId,
                PaymentMethodId = 1,
                Amount = 45000m,
                PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
                PaidAt = now
            });
            ctx.OrderDetails.Add(new OrderDetail
            {
                OrderId = OrderId,
                DrinkId = DrinkId,
                SizeId = SizeId,
                DrinkName = "D",
                SizeName = "M",
                Price = 45000m,
                Quantity = 1,
                Note = "",
                CostStatus = SalesCostStatus.Pending
            });
            await ctx.SaveChangesAsync();
            var d = await CreateDeductSvc(ctx).DeductStockForCommittedOrderAsync(Sold(), StoreId, OrderId);
            Assert.True(d.IsSuccess, d.Message);
        }
    }
}
