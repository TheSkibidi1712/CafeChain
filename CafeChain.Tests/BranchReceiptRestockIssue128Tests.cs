using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Permissions;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CafeChain.Tests
{
    /// <summary>Issue #128 — BranchReceipt draft/confirm, partial receipt, over-receipt, cost snapshot, audit.</summary>
    public class BranchReceiptRestockIssue128Tests : IntegrationTestBase
    {
        private const int StoreId = 1280;
        private const int IngredientId = 12801;
        private const int PreparedItemId = 12802;
        private const int UnitGram = 1;
        private const int ManagerStaffId = 12810;
        private const int WarehouseStaffId = 12811;
        private const int SupervisorStaffId = 12812;

        private static readonly string[] ManagerRoles = { RoleConstants.AccountantWarehouse };
        private static readonly string[] WarehouseRoles = { RoleConstants.AccountantWarehouse };
        private static readonly string[] SupervisorRoles = { RoleConstants.ShiftSupervisor };

        [Fact]
        public async Task Draft_DoesNotMutateInventory_OrCreateTransaction()
        {
            using var ctx = CreateDbContext();
            var requestId = await SeedProcessingRequestAsync(ctx, requested: 1000m);
            var service = CreateReceiptService(ctx);

            var draft = await service.CreateDraftAsync(NewReceiptRequest(
                requestId, inputQty: 400m, unitId: UnitGram, price: 50_000m, key: "k-draft-1"),
                ManagerStaffId, ManagerRoles);

            Assert.True(draft.IsSuccess, draft.Message);
            Assert.Equal(BranchReceiptStatuses.Draft, draft.Data!.Status);

            Assert.Equal(0, await ctx.InventoryTransactions.CountAsync());
            Assert.Equal(0, await ctx.InventoryCostLayers.CountAsync());
            var inv = await ctx.StoreInventories.FirstOrDefaultAsync(i =>
                i.StoreId == StoreId && i.IngredientId == IngredientId);
            Assert.True(inv == null || inv.AvailableQty == 0);
        }

        [Fact]
        public async Task Confirm_PostsInventory_CostLayer_AndTransaction()
        {
            using var ctx = CreateDbContext();
            var requestId = await SeedProcessingRequestAsync(ctx, requested: 1000m);
            var service = CreateReceiptService(ctx);

            var draft = await service.CreateDraftAsync(NewReceiptRequest(
                requestId, 400m, UnitGram, 40_000m, "k-confirm-1"),
                ManagerStaffId, ManagerRoles);
            Assert.True(draft.IsSuccess, draft.Message);

            var confirm = await service.ConfirmAsync(
                draft.Data!.BranchReceiptId, ManagerStaffId, StoreId, ManagerRoles, draft.Data.RowVersion);
            Assert.True(confirm.IsSuccess, confirm.Message);
            Assert.False(confirm.Data!.WasReplay);
            Assert.Equal(BranchReceiptStatuses.Confirmed, confirm.Data.Status);
            Assert.Single(confirm.Data.InventoryTransactionIds);

            var inv = await ctx.StoreInventories.SingleAsync(i =>
                i.StoreId == StoreId && i.IngredientId == IngredientId);
            Assert.Equal(400m, inv.AvailableQty);

            var tx = await ctx.InventoryTransactions.SingleAsync();
            Assert.Equal(InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN, tx.Type);
            Assert.Equal(400m, tx.Quantity);
            Assert.NotNull(tx.BranchReceiptLineId);
            Assert.True(tx.UnitCost > 0);
            Assert.True(tx.TotalCost > 0);

            var layer = await ctx.InventoryCostLayers.SingleAsync();
            Assert.Equal(400m, layer.Quantity);
            Assert.Equal(400m, layer.RemainingQuantity);
            Assert.True(layer.UnitCost > 0);

            var req = await ctx.RestockRequests.SingleAsync(r => r.RestockRequestId == requestId);
            Assert.Equal(RestockRequestStatuses.PartiallyReceived, req.Status);

            var posting = await ctx.RestockFulfillmentPostings.SingleAsync();
            Assert.Equal(RestockFulfillmentDocumentTypes.BranchReceipt, posting.SourceDocumentType);
            Assert.Equal(400m, posting.Quantity);

            Assert.True(await ctx.RestockRequestTransitions.AnyAsync(t =>
                t.RestockRequestId == requestId && t.BranchReceiptId == draft.Data.BranchReceiptId));
        }

        [Fact]
        public async Task PartialReceipt_ActualMinusRejected_IsOnlyQuantityPostedToInventory()
        {
            using var ctx = CreateDbContext();
            var requestId = await SeedProcessingRequestAsync(ctx, requested: 10m);
            var service = CreateReceiptService(ctx);
            var input = NewReceiptRequest(requestId, 10m, UnitGram, 100m, "k-partial-rejected");
            input.Lines[0].RejectedQuantity = 2m;
            input.Lines[0].RejectionIssueType = SupplierReceiptIssueTypes.Damaged;
            input.Lines[0].RejectionReason = "Bao bì rách khi giao nhận";

            var draft = await service.CreateDraftAsync(input, ManagerStaffId, ManagerRoles);
            Assert.True(draft.IsSuccess, draft.Message);
            var confirm = await service.ConfirmAsync(
                draft.Data!.BranchReceiptId, ManagerStaffId, StoreId, ManagerRoles, draft.Data.RowVersion);

            Assert.True(confirm.IsSuccess, confirm.Message);
            var line = await ctx.BranchReceiptLines.SingleAsync();
            Assert.Equal(8m, line.ReceivedBaseQuantity);
            Assert.Equal(2m, line.RejectedBaseQuantity);
            var inventory = await ctx.StoreInventories.SingleAsync(x =>
                x.StoreId == StoreId && x.IngredientId == IngredientId);
            Assert.Equal(8m, inventory.AvailableQty);
            Assert.Equal(8m, (await ctx.InventoryCostLayers.SingleAsync(x =>
                x.SourceBranchReceiptLineId == line.BranchReceiptLineId)).Quantity);
            Assert.Equal(8m, (await ctx.InventoryTransactions.SingleAsync(x =>
                x.BranchReceiptLineId == line.BranchReceiptLineId)).Quantity);
            Assert.Equal(8m, (await ctx.RestockFulfillmentPostings.SingleAsync(x =>
                x.SourceDocumentLineId == line.BranchReceiptLineId
                && x.SourceDocumentType == RestockFulfillmentDocumentTypes.BranchReceipt)).Quantity);
        }

        [Fact]
        public async Task PartialReceipt_InvalidRejectedQuantityOrMissingReason_IsRejected()
        {
            using var ctx = CreateDbContext();
            var requestId = await SeedProcessingRequestAsync(ctx, requested: 20m);
            var service = CreateReceiptService(ctx);
            var exceeds = NewReceiptRequest(requestId, 10m, UnitGram, 100m, "k-rejected-exceeds");
            exceeds.Lines[0].RejectedQuantity = 11m;
            var exceedsResult = await service.CreateDraftAsync(exceeds, ManagerStaffId, ManagerRoles);

            var missingReason = NewReceiptRequest(requestId, 10m, UnitGram, 100m, "k-rejected-reason");
            missingReason.Lines[0].RejectedQuantity = 2m;
            missingReason.Lines[0].RejectionIssueType = SupplierReceiptIssueTypes.Damaged;
            var reasonResult = await service.CreateDraftAsync(missingReason, ManagerStaffId, ManagerRoles);

            Assert.False(exceedsResult.IsSuccess);
            Assert.Equal(BranchReceiptErrorCodes.RejectedExceedsActualReceived, exceedsResult.ErrorCode);
            Assert.False(reasonResult.IsSuccess);
            Assert.Equal(BranchReceiptErrorCodes.RejectionReasonRequired, reasonResult.ErrorCode);
            Assert.Empty(ctx.BranchReceipts);
        }

        [Fact]
        public async Task PartialThenComplete_DerivesStatusFromConfirmedSums()
        {
            using var ctx = CreateDbContext();
            var requestId = await SeedProcessingRequestAsync(ctx, requested: 1000m);
            var service = CreateReceiptService(ctx);

            var d1 = await service.CreateDraftAsync(NewReceiptRequest(
                requestId, 600m, UnitGram, 60_000m, "k-partial-1"), ManagerStaffId, ManagerRoles);
            Assert.True(d1.IsSuccess, d1.Message);
            var c1 = await service.ConfirmAsync(d1.Data!.BranchReceiptId, ManagerStaffId, StoreId, ManagerRoles, d1.Data.RowVersion);
            Assert.True(c1.IsSuccess, c1.Message);

            var req = await ctx.RestockRequests.SingleAsync(r => r.RestockRequestId == requestId);
            Assert.Equal(RestockRequestStatuses.PartiallyReceived, req.Status);

            var d2 = await service.CreateDraftAsync(NewReceiptRequest(
                requestId, 400m, UnitGram, 40_000m, "k-partial-2"), ManagerStaffId, ManagerRoles);
            Assert.True(d2.IsSuccess, d2.Message);
            var c2 = await service.ConfirmAsync(d2.Data!.BranchReceiptId, ManagerStaffId, StoreId, ManagerRoles, d2.Data.RowVersion);
            Assert.True(c2.IsSuccess, c2.Message);

            req = await ctx.RestockRequests.SingleAsync(r => r.RestockRequestId == requestId);
            Assert.Equal(RestockRequestStatuses.Completed, req.Status);

            var inv = await ctx.StoreInventories.SingleAsync(i =>
                i.StoreId == StoreId && i.IngredientId == IngredientId);
            Assert.Equal(1000m, inv.AvailableQty);
            Assert.Equal(2, await ctx.InventoryTransactions.CountAsync(t =>
                t.Type == InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN));
            var postedQuantities = await ctx.RestockFulfillmentPostings
                .Where(p => p.RestockRequestId == requestId)
                .Select(p => p.Quantity)
                .ToListAsync();
            Assert.Equal(1000m, postedQuantities.Sum());
        }

        [Fact]
        public async Task OverReceipt_Blocked_WithErrorCode()
        {
            using var ctx = CreateDbContext();
            var requestId = await SeedProcessingRequestAsync(ctx, requested: 100m);
            var service = CreateReceiptService(ctx);

            var d1 = await service.CreateDraftAsync(NewReceiptRequest(
                requestId, 80m, UnitGram, 8_000m, "k-over-1"), ManagerStaffId, ManagerRoles);
            Assert.True(d1.IsSuccess, d1.Message);
            Assert.True((await service.ConfirmAsync(d1.Data!.BranchReceiptId, ManagerStaffId, StoreId, ManagerRoles, d1.Data.RowVersion)).IsSuccess);

            var d2 = await service.CreateDraftAsync(NewReceiptRequest(
                requestId, 30m, UnitGram, 3_000m, "k-over-2"), ManagerStaffId, ManagerRoles);
            Assert.False(d2.IsSuccess);
            Assert.Equal(BranchReceiptErrorCodes.RestockOverReceiptNotAllowed, d2.ErrorCode);

            var inv = await ctx.StoreInventories.SingleAsync(i =>
                i.StoreId == StoreId && i.IngredientId == IngredientId);
            Assert.Equal(80m, inv.AvailableQty);
        }

        [Fact]
        public async Task CostIncomplete_Rejected()
        {
            using var ctx = CreateDbContext();
            var requestId = await SeedProcessingRequestAsync(ctx, requested: 100m);
            var service = CreateReceiptService(ctx);

            var draft = await service.CreateDraftAsync(NewReceiptRequest(
                requestId, 50m, UnitGram, 0m, "k-cost-0"), ManagerStaffId, ManagerRoles);
            Assert.False(draft.IsSuccess);
            Assert.Equal(BranchReceiptErrorCodes.ReceiptCostIncomplete, draft.ErrorCode);
        }

        [Fact]
        public async Task Confirm_Replay_DoesNotDoublePost()
        {
            using var ctx = CreateDbContext();
            var requestId = await SeedProcessingRequestAsync(ctx, requested: 500m);
            var service = CreateReceiptService(ctx);

            var draft = await service.CreateDraftAsync(NewReceiptRequest(
                requestId, 200m, UnitGram, 20_000m, "k-replay"), ManagerStaffId, ManagerRoles);
            var id = draft.Data!.BranchReceiptId;

            var c1 = await service.ConfirmAsync(id, ManagerStaffId, StoreId, ManagerRoles, draft.Data.RowVersion);
            Assert.True(c1.IsSuccess);
            var c2 = await service.ConfirmAsync(id, ManagerStaffId, StoreId, ManagerRoles, draft.Data.RowVersion);
            Assert.True(c2.IsSuccess, c2.Message);
            Assert.True(c2.Data!.WasReplay);

            Assert.Equal(1, await ctx.InventoryTransactions.CountAsync(t =>
                t.Type == InventoryTransactionTypeEnum.BRANCH_RECEIPT_IN));
            Assert.Equal(1, await ctx.RestockFulfillmentPostings.CountAsync());
            var inv = await ctx.StoreInventories.SingleAsync(i =>
                i.StoreId == StoreId && i.IngredientId == IngredientId);
            Assert.Equal(200m, inv.AvailableQty);
        }

        [Fact]
        public async Task Confirm_MissingOrStaleRowVersionRejected()
        {
            using var ctx = CreateDbContext();
            var requestId = await SeedProcessingRequestAsync(ctx, requested: 100m);
            var service = CreateReceiptService(ctx);
            var draft = await service.CreateDraftAsync(NewReceiptRequest(
                requestId, 20m, UnitGram, 2_000m, "k-version"), ManagerStaffId, ManagerRoles);
            Assert.True(draft.IsSuccess, draft.Message);

            var missing = await service.ConfirmAsync(
                draft.Data!.BranchReceiptId, ManagerStaffId, StoreId, ManagerRoles, null);
            var stale = await service.ConfirmAsync(
                draft.Data.BranchReceiptId,
                ManagerStaffId,
                StoreId,
                ManagerRoles,
                Convert.ToBase64String(new byte[] { 9 }));

            Assert.False(missing.IsSuccess);
            Assert.Equal(BranchReceiptErrorCodes.ValidationRowVersionRequired, missing.ErrorCode);
            Assert.False(stale.IsSuccess);
            Assert.Equal(BranchReceiptErrorCodes.ResourceChanged, stale.ErrorCode);
            Assert.Equal(BranchReceiptStatuses.Draft,
                (await ctx.BranchReceipts.AsNoTracking().SingleAsync()).Status);
        }

        [Fact]
        public async Task Workflow_StartRejectCancel_NoInventoryMutation()
        {
            using var ctx = CreateDbContext();
            var requestId = await SeedSubmittedRequestAsync(ctx, requested: 100m);
            var workflow = CreateWorkflowService(ctx);

            var start = await workflow.StartProcessingAsync(
                requestId, WarehouseStaffId, StoreId, WarehouseRoles, "begin",
                await RequestVersionAsync(ctx, requestId));
            Assert.True(start.IsSuccess, start.Message);
            Assert.Equal(RestockRequestStatuses.Processing, start.Data!.Status);

            var req = await ctx.RestockRequests.SingleAsync(r => r.RestockRequestId == requestId);
            Assert.Equal(RestockRequestStatuses.Processing, req.Status);
            Assert.Equal(0, await ctx.InventoryTransactions.CountAsync());

            // separate request for reject
            var requestId2 = await SeedSubmittedRequestAsync(ctx, requested: 50m, alertSuffix: 2);
            var reject = await workflow.RejectAsync(
                requestId2, WarehouseStaffId, StoreId, WarehouseRoles, "hết NCC",
                await RequestVersionAsync(ctx, requestId2));
            Assert.True(reject.IsSuccess, reject.Message);
            Assert.Equal(RestockRequestStatuses.Rejected, reject.Data!.Status);
            Assert.Equal(0, await ctx.InventoryTransactions.CountAsync());

            var requestId3 = await SeedSubmittedRequestAsync(ctx, requested: 50m, alertSuffix: 3);
            var cancel = await workflow.CancelAsync(
                requestId3, ManagerStaffId, StoreId, ManagerRoles, "nhầm",
                await RequestVersionAsync(ctx, requestId3));
            Assert.True(cancel.IsSuccess, cancel.Message);
            Assert.Equal(RestockRequestStatuses.Cancelled, cancel.Data!.Status);
        }

        [Fact]
        public async Task Rejected_CannotReceive()
        {
            using var ctx = CreateDbContext();
            var requestId = await SeedSubmittedRequestAsync(ctx, requested: 100m);
            var workflow = CreateWorkflowService(ctx);
            Assert.True((await workflow.RejectAsync(
                requestId, WarehouseStaffId, StoreId, WarehouseRoles, "no",
                await RequestVersionAsync(ctx, requestId))).IsSuccess);

            var service = CreateReceiptService(ctx);
            var draft = await service.CreateDraftAsync(NewReceiptRequest(
                requestId, 10m, UnitGram, 1_000m, "k-rej"), ManagerStaffId, ManagerRoles);
            Assert.False(draft.IsSuccess);
            Assert.Equal(BranchReceiptErrorCodes.RequestStateInvalid, draft.ErrorCode);
        }

        [Fact]
        public async Task Warehouse_CanConfirm_AsGlobalDocumentProcessor()
        {
            using var ctx = CreateDbContext();
            var requestId = await SeedProcessingRequestAsync(ctx, requested: 100m);
            var service = CreateReceiptService(ctx);

            var draft = await service.CreateDraftAsync(NewReceiptRequest(
                requestId, 50m, UnitGram, 5_000m, "k-wh"), ManagerStaffId, ManagerRoles);
            Assert.True(draft.IsSuccess);

            var confirm = await service.ConfirmAsync(
                draft.Data!.BranchReceiptId, WarehouseStaffId, StoreId, WarehouseRoles, draft.Data.RowVersion);
            Assert.True(confirm.IsSuccess, confirm.Message);
        }

        [Fact]
        public async Task ShiftSupervisor_CanReadOwnStore_ButCannotCreateOrConfirm()
        {
            using var ctx = CreateDbContext();
            EnsureStaff(ctx, SupervisorStaffId, RoleConstants.ShiftSupervisor, "ss128@test.local");
            await ctx.SaveChangesAsync();

            var requestId = await SeedProcessingRequestAsync(ctx, requested: 100m);
            var service = CreateReceiptService(ctx);
            var draft = await service.CreateDraftAsync(NewReceiptRequest(
                requestId, 50m, UnitGram, 5_000m, "k-ss"), ManagerStaffId, ManagerRoles);
            Assert.True(draft.IsSuccess, draft.Message);

            var detail = await service.GetDetailAsync(
                draft.Data!.BranchReceiptId, SupervisorStaffId, StoreId, SupervisorRoles);
            Assert.True(detail.IsSuccess, detail.Message);

            var confirm = await service.ConfirmAsync(
                draft.Data!.BranchReceiptId, SupervisorStaffId, StoreId, SupervisorRoles, draft.Data.RowVersion);
            Assert.False(confirm.IsSuccess);
            Assert.Equal(BranchReceiptErrorCodes.Unauthorized, confirm.ErrorCode);
        }

        [Theory]
        [InlineData(RoleConstants.StoreManager)]
        [InlineData(RoleConstants.AreaManager)]
        public async Task StoreManagerAndAreaManager_CannotCreateReceipt(string role)
        {
            using var ctx = CreateDbContext();
            var requestId = await SeedProcessingRequestAsync(ctx, requested: 100m);

            var result = await CreateReceiptService(ctx).CreateDraftAsync(
                NewReceiptRequest(requestId, 10m, UnitGram, 1_000m, $"k-forbidden-{role}"),
                ManagerStaffId,
                new[] { role });

            Assert.False(result.IsSuccess);
            Assert.Equal(BranchReceiptErrorCodes.Unauthorized, result.ErrorCode);
        }

        [Fact]
        public async Task SystemAdmin_DoesNotReceiveImplicitReceiptMutationPermission()
        {
            using var ctx = CreateDbContext();
            var requestId = await SeedProcessingRequestAsync(ctx, requested: 100m);
            var service = CreateReceiptService(ctx);

            var draft = await service.CreateDraftAsync(
                NewReceiptRequest(requestId, 50m, UnitGram, 5_000m, "k-system-admin"),
                ManagerStaffId,
                new[] { RoleConstants.SystemAdmin });

            Assert.False(draft.IsSuccess);
            Assert.Equal(BranchReceiptErrorCodes.Unauthorized, draft.ErrorCode);
        }

        [Fact]
        public async Task LinkFulfillment_DoesNotMutateStock()
        {
            using var ctx = CreateDbContext();
            var requestId = await SeedSubmittedRequestAsync(ctx, requested: 100m);
            var workflow = CreateWorkflowService(ctx);

            var link = await workflow.LinkFulfillmentAsync(
                requestId, WarehouseStaffId, StoreId, WarehouseRoles,
                new LinkRestockFulfillmentRequest
                {
                    SourceType = RestockFulfillmentSourceTypes.Supplier,
                    PlannedBaseQuantity = 100m,
                    Notes = "PO-1"
                }, await RequestVersionAsync(ctx, requestId));
            Assert.True(link.IsSuccess, link.Message);
            Assert.Equal(0, await ctx.InventoryTransactions.CountAsync());
            Assert.Equal(1, await ctx.RestockRequestFulfillments.CountAsync());
            var req = await ctx.RestockRequests.SingleAsync(r => r.RestockRequestId == requestId);
            Assert.Equal(RestockRequestStatuses.Processing, req.Status);
        }

        [Fact]
        public async Task LinkFulfillment_RejectsInventoryDocumentDetailId_DualPostBoundary()
        {
            using var ctx = CreateDbContext();
            var requestId = await SeedSubmittedRequestAsync(ctx, requested: 100m);
            var workflow = CreateWorkflowService(ctx);

            var link = await workflow.LinkFulfillmentAsync(
                requestId, WarehouseStaffId, StoreId, WarehouseRoles,
                new LinkRestockFulfillmentRequest
                {
                    SourceType = RestockFulfillmentSourceTypes.Supplier,
                    PlannedBaseQuantity = 50m,
                    InventoryDocumentDetailId = 999
                }, await RequestVersionAsync(ctx, requestId));
            Assert.False(link.IsSuccess);
            Assert.Equal(0, await ctx.RestockRequestFulfillments.CountAsync());
        }

        [Fact]
        public async Task PartiallyReceived_CannotCancel()
        {
            using var ctx = CreateDbContext();
            var requestId = await SeedProcessingRequestAsync(ctx, requested: 100m);
            var service = CreateReceiptService(ctx);
            var draft = await service.CreateDraftAsync(NewReceiptRequest(
                requestId, 40m, UnitGram, 4_000m, "k-partial-cancel"), ManagerStaffId, ManagerRoles);
            Assert.True((await service.ConfirmAsync(
                draft.Data!.BranchReceiptId, ManagerStaffId, StoreId, ManagerRoles, draft.Data.RowVersion)).IsSuccess);

            var cancel = await CreateWorkflowService(ctx).CancelAsync(
                requestId, WarehouseStaffId, StoreId, WarehouseRoles, "late",
                await RequestVersionAsync(ctx, requestId));
            Assert.False(cancel.IsSuccess);
            Assert.Equal(BranchReceiptErrorCodes.TransitionInvalid, cancel.ErrorCode);
            var req = await ctx.RestockRequests.SingleAsync(r => r.RestockRequestId == requestId);
            Assert.Equal(RestockRequestStatuses.PartiallyReceived, req.Status);
        }

        [Fact]
        public async Task BranchReceipt_AlertEvaluationFailure_DoesNotRollbackReceipt()
        {
            // Alias of AlertFailure_DoesNotRollbackReceipt with explicit post-commit assertions.
            using var ctx = CreateDbContext();
            var requestId = await SeedProcessingRequestAsync(ctx, requested: 100m);
            var failingAlerts = new Mock<IStockAlertService>();
            failingAlerts
                .Setup(s => s.EvaluateStoreInventoryItemAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<CafeChain.Application.DTOs.POS.StockAlertEvaluationResultDto>.Failure("boom"));

            var service = CreateReceiptService(ctx, failingAlerts.Object);
            var draft = await service.CreateDraftAsync(NewReceiptRequest(
                requestId, 40m, UnitGram, 4_000m, "k-alert-alias"), ManagerStaffId, ManagerRoles);
            var confirm = await service.ConfirmAsync(
                draft.Data!.BranchReceiptId, ManagerStaffId, StoreId, ManagerRoles, draft.Data.RowVersion);

            Assert.True(confirm.IsSuccess, confirm.Message);
            Assert.True(confirm.Data!.AlertEvaluationFailed);
            Assert.Equal(BranchReceiptStatuses.Confirmed,
                (await ctx.BranchReceipts.SingleAsync(r => r.BranchReceiptId == draft.Data.BranchReceiptId)).Status);
            Assert.Equal(40m, (await ctx.StoreInventories.SingleAsync(i =>
                i.StoreId == StoreId && i.IngredientId == IngredientId)).AvailableQty);
            Assert.Equal(1, await ctx.InventoryTransactions.CountAsync());
            Assert.Equal(RestockRequestStatuses.PartiallyReceived,
                (await ctx.RestockRequests.SingleAsync(r => r.RestockRequestId == requestId)).Status);
        }

        [Fact]
        public async Task AlertFailure_DoesNotRollbackReceipt()
        {
            using var ctx = CreateDbContext();
            var requestId = await SeedProcessingRequestAsync(ctx, requested: 100m);
            var failingAlerts = new Mock<IStockAlertService>();
            failingAlerts
                .Setup(s => s.EvaluateStoreInventoryItemAsync(It.IsAny<int>(), It.IsAny<string>()))
                .ReturnsAsync(ServiceResult<CafeChain.Application.DTOs.POS.StockAlertEvaluationResultDto>.Failure("boom"));

            var service = CreateReceiptService(ctx, failingAlerts.Object);
            var draft = await service.CreateDraftAsync(NewReceiptRequest(
                requestId, 40m, UnitGram, 4_000m, "k-alert"), ManagerStaffId, ManagerRoles);
            var confirm = await service.ConfirmAsync(
                draft.Data!.BranchReceiptId, ManagerStaffId, StoreId, ManagerRoles, draft.Data.RowVersion);

            Assert.True(confirm.IsSuccess, confirm.Message);
            Assert.True(confirm.Data!.AlertEvaluationFailed);
            Assert.Equal(1, await ctx.InventoryTransactions.CountAsync());
            var inv = await ctx.StoreInventories.SingleAsync(i =>
                i.StoreId == StoreId && i.IngredientId == IngredientId);
            Assert.Equal(40m, inv.AvailableQty);
        }

        [Fact]
        public async Task Timeline_IsDurable()
        {
            using var ctx = CreateDbContext();
            var requestId = await SeedSubmittedRequestAsync(ctx, requested: 100m);
            var workflow = CreateWorkflowService(ctx);
            Assert.True((await workflow.StartProcessingAsync(
                requestId, WarehouseStaffId, StoreId, WarehouseRoles, null,
                await RequestVersionAsync(ctx, requestId))).IsSuccess);

            var service = CreateReceiptService(ctx);
            var draft = await service.CreateDraftAsync(NewReceiptRequest(
                requestId, 100m, UnitGram, 10_000m, "k-tl"), ManagerStaffId, ManagerRoles);
            Assert.True((await service.ConfirmAsync(
                draft.Data!.BranchReceiptId, ManagerStaffId, StoreId, ManagerRoles, draft.Data.RowVersion)).IsSuccess);

            var detail = await workflow.GetWorkflowDetailAsync(
                requestId, ManagerStaffId, StoreId, ManagerRoles);
            Assert.True(detail.IsSuccess);
            Assert.True(detail.Data!.Timeline.Count >= 2);
            Assert.Equal(RestockRequestStatuses.Completed, detail.Data.Status);
            Assert.Equal(100m, detail.Data.ReceivedQuantity);
            Assert.Equal(0m, detail.Data.RemainingQuantity);
        }

        private static CreateBranchReceiptRequest NewReceiptRequest(
            int requestId, decimal inputQty, int unitId, decimal price, string key) =>
            new()
            {
                StoreId = StoreId,
                ReceiptKey = key,
                Lines =
                {
                    new CreateBranchReceiptLineInput
                    {
                        RestockRequestId = requestId,
                        ActualReceivedQuantity = inputQty,
                        InputUnitId = unitId,
                        ActualPackagePrice = price
                    }
                }
            };

        private BranchReceiptService CreateReceiptService(AppDbContext ctx, IStockAlertService? alerts = null)
        {
            var physical = new PhysicalUnitConversionService(ctx, NullLogger<PhysicalUnitConversionService>.Instance);
            var unit = new UnitConversionService(ctx, NullLogger<UnitConversionService>.Instance, physical);
            var mode = new Mock<IInventoryWriterModeService>();
            var resolver = new Mock<IStoreInventoryWriteResolver>();
            if (alerts == null)
            {
                var alertMock = new Mock<IStockAlertService>();
                alertMock
                    .Setup(s => s.EvaluateStoreInventoryItemAsync(It.IsAny<int>(), It.IsAny<string>()))
                    .ReturnsAsync(ServiceResult<CafeChain.Application.DTOs.POS.StockAlertEvaluationResultDto>.Success(
                        new CafeChain.Application.DTOs.POS.StockAlertEvaluationResultDto()));
                alerts = alertMock.Object;
            }

            return new BranchReceiptService(
                ctx, unit, physical, mode.Object, resolver.Object,
                new RestockFulfillmentPostingService(ctx), alerts,
                new CafeChain.Application.Services.Security.ScopeAuthorizationService(ctx),
                NullLogger<BranchReceiptService>.Instance);
        }

        private static RestockRequestWorkflowService CreateWorkflowService(AppDbContext ctx) =>
            new(
                ctx,
                new CafeChain.Application.Services.Security.ScopeAuthorizationService(ctx),
                NullLogger<RestockRequestWorkflowService>.Instance,
                new RestockAllocationService(ctx, new NoPurchaseOrderAllocationProvider()));

        private static async Task<string> RequestVersionAsync(AppDbContext ctx, int requestId) =>
            Convert.ToBase64String(await ctx.RestockRequests.AsNoTracking()
                .Where(x => x.RestockRequestId == requestId)
                .Select(x => x.RowVersion)
                .SingleAsync());

        private async Task<int> SeedProcessingRequestAsync(AppDbContext ctx, decimal requested)
        {
            var id = await SeedSubmittedRequestAsync(ctx, requested);
            var r = await ctx.RestockRequests.SingleAsync(x => x.RestockRequestId == id);
            r.Status = RestockRequestStatuses.Processing;
            r.HandledByStaffId = WarehouseStaffId;
            r.HandledAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
            return id;
        }

        private async Task<int> SeedSubmittedRequestAsync(
            AppDbContext ctx, decimal requested, int alertSuffix = 1)
        {
            EnsureBase(ctx);
            EnsureStaff(ctx, ManagerStaffId, RoleConstants.StoreManager, "mgr128@test.local");
            EnsureStaff(ctx, WarehouseStaffId, RoleConstants.AccountantWarehouse, "aw128@test.local");
            var ingredientId = IngredientId + alertSuffix - 1;
            if (!ctx.Ingredients.Any(i => i.IngredientId == ingredientId)
                && !ctx.Ingredients.Local.Any(i => i.IngredientId == ingredientId))
            {
                ctx.Ingredients.Add(new Ingredient
                {
                    IngredientId = ingredientId,
                    Code = $"ING128-{alertSuffix}",
                    Name = $"Nguyên liệu 128-{alertSuffix}",
                    BaseUnitId = UnitGram,
                    Active = true
                });
            }
            await ctx.SaveChangesAsync();

            var alert = new StockAlert
            {
                StoreId = StoreId,
                IngredientId = ingredientId,
                AlertType = StockAlertTypes.LowStock,
                Severity = StockAlertSeverities.Warning,
                Status = StockAlertStatuses.Confirmed,
                Source = StockAlertSources.ManualCheck,
                CurrentQtySnapshot = 0,
                ThresholdSnapshot = requested,
                ConfirmedByStaffId = ManagerStaffId,
                ConfirmedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            ctx.StockAlerts.Add(alert);
            await ctx.SaveChangesAsync();

            var req = new RestockRequest
            {
                StockAlertId = alert.StockAlertId,
                StoreId = StoreId,
                IngredientId = ingredientId,
                RequestedQuantity = requested,
                Status = RestockRequestStatuses.Submitted,
                Priority = RestockRequestPriorities.Normal,
                CreatedByStaffId = ManagerStaffId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            ctx.RestockRequests.Add(req);
            await ctx.SaveChangesAsync();
            return req.RestockRequestId;
        }

        private static void EnsureBase(AppDbContext ctx)
        {
            if (!ctx.Units.Any(u => u.UnitId == UnitGram))
            {
                ctx.Units.Add(new Unit
                {
                    UnitId = UnitGram,
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
                    Name = "Store 128",
                    Address = "x",
                    Phone = "0",
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (!ctx.Ingredients.Any(i => i.IngredientId == IngredientId))
            {
                ctx.Ingredients.Add(new Ingredient
                {
                    IngredientId = IngredientId,
                    Code = "ING128",
                    Name = "Sữa 128",
                    BaseUnitId = UnitGram,
                    Active = true
                });
            }

            if (!ctx.PreparedItems.Any(p => p.PreparedItemId == PreparedItemId))
            {
                ctx.PreparedItems.Add(new PreparedItem
                {
                    PreparedItemId = PreparedItemId,
                    Code = "PI128",
                    Name = "Syrup 128",
                    BaseUnitId = UnitGram,
                    Active = true
                });
            }
        }

        private static void EnsureStaff(AppDbContext ctx, int staffId, string roleName, string email)
        {
            if (ctx.Staffs.Any(s => s.StaffId == staffId)) return;

            if (!ctx.Roles.Any(r => r.Name == roleName) && !ctx.Roles.Local.Any(r => r.Name == roleName))
            {
                var id = (ctx.Roles.Any() ? ctx.Roles.Max(r => r.RoleId) : 0)
                         + (ctx.Roles.Local.Any() ? ctx.Roles.Local.Max(r => r.RoleId) : 0) + 50 + staffId % 100;
                ctx.Roles.Add(new Role
                {
                    RoleId = id,
                    Name = roleName,
                    Active = true,
                    IsStoreLevel = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var role = ctx.Roles.Local.FirstOrDefault(r => r.Name == roleName)
                       ?? ctx.Roles.First(r => r.Name == roleName);
            var accountId = 40000 + staffId;
            ctx.Accounts.Add(new Account
            {
                AccountId = accountId,
                Email = email,
                PasswordHash = "x",
                Active = true,
                CreatedAt = DateTime.UtcNow
            });
            ctx.AccountRoles.Add(new AccountRole { AccountId = accountId, RoleId = role.RoleId });
            ctx.Staffs.Add(new Staff
            {
                StaffId = staffId,
                AccountId = accountId,
                StoreId = StoreId,
                FullName = $"Staff {staffId}",
                Active = true,
                CreatedAt = DateTime.UtcNow,
                BaseSalary = 0
            });
        }
    }
}
