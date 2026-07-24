using CafeChain.Areas.Admin.Controllers;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests;

public sealed class PurchaseOrderPartialReceiptIssue178Tests : IntegrationTestBase
{
    private const int StoreId = 1780;
    private const int OtherStoreId = 1781;
    private const int StaffId = 17802;
    private const int UnitId = 17803;
    private const int IngredientId = 17804;
    private const int SupplierId = 17805;

    [Fact]
    public void PurchaseOrderController_UsesReceiveGoodsRoleBoundary_IncludingShiftSupervisor()
    {
        Assert.Equal(
            typeof(AdminStoreScopedController),
            typeof(AdminPurchaseOrdersController).BaseType);

        var authorize = Assert.Single(
            typeof(AdminPurchaseOrdersController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());
        var roles = (authorize.Roles ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.Contains(RoleConstants.BusinessOwner, roles);
        Assert.Contains(RoleConstants.AreaManager, roles);
        Assert.Contains(RoleConstants.StoreManager, roles);
        Assert.Contains(RoleConstants.ShiftSupervisor, roles);
        Assert.Contains(RoleConstants.AccountantWarehouse, roles);
        Assert.DoesNotContain(RoleConstants.SalesStaff, roles);
    }

    [Fact]
    public async Task PurchaseOrder_AreaManagerMutationAndMissingOrStaleRowVersion_AreRejected()
    {
        using var context = CreateDbContext();
        var request = await SeedFoundationAsync(context, 20m);
        var service = CreateService(context);
        var offerId = await context.IngredientSuppliers
            .Where(x => x.IngredientId == IngredientId && x.SupplierId == SupplierId)
            .Select(x => x.IngredientSupplierId)
            .SingleAsync();
        var created = await service.CreateDraftAsync(new CreatePurchaseOrderRequest
        {
            StoreId = StoreId,
            SupplierId = SupplierId,
            Lines = { new() { RestockRequestId = request.RestockRequestId, IngredientId = IngredientId, IngredientSupplierId = offerId, PackageCount = 2m } }
        }, StaffId, new[] { RoleConstants.AccountantWarehouse });
        Assert.True(created.IsSuccess, created.Message);

        var areaManager = await service.ApproveAsync(
            created.Data!.PurchaseOrderId, created.Data.RowVersion, StaffId, new[] { RoleConstants.AreaManager });
        var missing = await service.ApproveAsync(
            created.Data.PurchaseOrderId, string.Empty, StaffId, new[] { RoleConstants.BusinessOwner });
        var stale = await service.ApproveAsync(
            created.Data.PurchaseOrderId,
            Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
            StaffId,
            new[] { RoleConstants.BusinessOwner });

        Assert.False(areaManager.IsSuccess);
        Assert.False(missing.IsSuccess);
        Assert.Equal(BranchReceiptErrorCodes.ValidationRowVersionRequired, missing.ErrorCode);
        Assert.False(stale.IsSuccess);
        Assert.Equal(BranchReceiptErrorCodes.ResourceChanged, stale.ErrorCode);
        Assert.Equal(PurchaseOrderStatuses.Draft, (await context.PurchaseOrders.SingleAsync()).Status);
    }

    [Fact]
    public async Task PurchaseOrder_InactiveOfferRejected()
    {
        using var context = CreateDbContext();
        await SeedFoundationAsync(context, 20m);
        var offer = await context.IngredientSuppliers.SingleAsync(x => x.SupplierId == SupplierId);
        offer.Active = false;
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateDraftAsync(new CreatePurchaseOrderRequest
        {
            StoreId = StoreId,
            SupplierId = SupplierId,
            Lines =
            {
                new CreatePurchaseOrderLineRequest
                {
                    IngredientId = IngredientId,
                    IngredientSupplierId = offer.IngredientSupplierId,
                    PackageCount = 1m
                }
            }
        }, StaffId, new[] { RoleConstants.AccountantWarehouse });

        Assert.False(result.IsSuccess);
        Assert.Empty(context.PurchaseOrders);
    }

    [Fact]
    public async Task PurchaseOrder_FractionalPackageCountIsRejected()
    {
        using var context = CreateDbContext();
        await SeedFoundationAsync(context, 20m);
        var offerId = await context.IngredientSuppliers
            .Where(x => x.IngredientId == IngredientId && x.SupplierId == SupplierId)
            .Select(x => x.IngredientSupplierId)
            .SingleAsync();

        var result = await CreateService(context).CreateDraftAsync(new CreatePurchaseOrderRequest
        {
            StoreId = StoreId,
            SupplierId = SupplierId,
            Lines =
            {
                new CreatePurchaseOrderLineRequest
                {
                    IngredientId = IngredientId,
                    IngredientSupplierId = offerId,
                    PackageCount = 1.5m
                }
            }
        }, StaffId, new[] { RoleConstants.AccountantWarehouse });

        Assert.False(result.IsSuccess);
        Assert.Contains("số nguyên", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(context.PurchaseOrders);
    }

    [Fact]
    public async Task PurchaseOrder_RoundedPackageCoversRestockDemandWithoutCountingSurplusAsDemand()
    {
        using var context = CreateDbContext();
        var request = await SeedFoundationAsync(context, 2300m);
        var offer = await context.IngredientSuppliers
            .SingleAsync(x => x.IngredientId == IngredientId && x.SupplierId == SupplierId);
        offer.PackageQuantity = 1000m;
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateDraftAsync(new CreatePurchaseOrderRequest
        {
            StoreId = StoreId,
            SupplierId = SupplierId,
            Lines =
            {
                new CreatePurchaseOrderLineRequest
                {
                    RestockRequestId = request.RestockRequestId,
                    IngredientId = IngredientId,
                    IngredientSupplierId = offer.IngredientSupplierId,
                    PackageCount = 3m
                }
            }
        }, StaffId, new[] { RoleConstants.AccountantWarehouse });

        Assert.True(result.IsSuccess, result.Message);
        var line = await context.PurchaseOrderLines.AsNoTracking().SingleAsync();
        Assert.Equal(3m, line.PackageCount);
        Assert.Equal(3000m, line.OrderedBaseQuantity);
        var demandCovered = await new PurchaseOrderQuantityProvider(context)
            .GetAllocatedBaseQuantityAsync(request.RestockRequestId);
        Assert.Equal(2300m, demandCovered);
    }

    [Fact]
    public async Task PurchaseOrderLine_CanLinkRestock_AndApprovalDoesNotFulfillRestock()
    {
        using var context = CreateDbContext();
        var request = await SeedFoundationAsync(context, 20m);
        var service = CreateService(context);

        var created = await service.CreateDraftAsync(new CreatePurchaseOrderRequest
        {
            StoreId = StoreId,
            SupplierId = SupplierId,
            Lines =
            {
                new CreatePurchaseOrderLineRequest
                {
                    RestockRequestId = request.RestockRequestId,
                    IngredientId = IngredientId,
                    IngredientSupplierId = await context.IngredientSuppliers
                        .Where(x => x.IngredientId == IngredientId && x.SupplierId == SupplierId)
                        .Select(x => x.IngredientSupplierId)
                        .SingleAsync(),
                    PackageCount = 2m
                }
            }
        }, StaffId, new[] { RoleConstants.AccountantWarehouse });
        var approved = await service.ApproveAsync(
            created.Data!.PurchaseOrderId, created.Data.RowVersion, StaffId, new[] { RoleConstants.BusinessOwner });
        var sent = await service.MarkSentAsync(
            created.Data.PurchaseOrderId, approved.Data!.RowVersion, StaffId, new[] { RoleConstants.AccountantWarehouse });

        Assert.True(created.IsSuccess, created.Message);
        Assert.True(approved.IsSuccess, approved.Message);
        Assert.True(sent.IsSuccess, sent.Message);
        var line = await context.PurchaseOrderLines.SingleAsync();
        Assert.Equal(request.RestockRequestId, line.RestockRequestId);
        Assert.Equal(20m, line.OrderedBaseQuantity);
        Assert.Empty(context.RestockFulfillmentPostings);
        Assert.Equal(RestockRequestStatuses.Processing, (await context.RestockRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task AcceptedPartialAndFullReceipt_UpdatePoAndRestockFromEvidence()
    {
        using var context = CreateDbContext();
        var request = await SeedFoundationAsync(context, 20m);
        var (order, line) = await SeedSentOrderAsync(context, request, 20m);
        var service = CreateService(context);
        var restockPostings = new RestockFulfillmentPostingService(context);

        var firstReceiptLine = await SeedReceiptLineAsync(context, line, request, 8m, 0m, "R178-1");
        Assert.True((await service.RegisterReceiptPostingAsync(firstReceiptLine.BranchReceipt, firstReceiptLine, StaffId)).IsSuccess);
        Assert.True((await restockPostings.RegisterAsync(ToRestockCommand(firstReceiptLine))).IsSuccess);
        await context.SaveChangesAsync();
        Assert.Equal(PurchaseOrderStatuses.PartiallyReceived, (await context.PurchaseOrders.SingleAsync()).Status);
        Assert.Equal(RestockRequestStatuses.PartiallyReceived, (await context.RestockRequests.SingleAsync()).Status);

        var secondReceiptLine = await SeedReceiptLineAsync(context, line, request, 12m, 0m, "R178-2");
        Assert.True((await service.RegisterReceiptPostingAsync(secondReceiptLine.BranchReceipt, secondReceiptLine, StaffId)).IsSuccess);
        Assert.True((await restockPostings.RegisterAsync(ToRestockCommand(secondReceiptLine))).IsSuccess);
        await context.SaveChangesAsync();

        Assert.Equal(PurchaseOrderStatuses.Completed, (await context.PurchaseOrders.SingleAsync()).Status);
        Assert.Equal(RestockRequestStatuses.Completed, (await context.RestockRequests.SingleAsync()).Status);
        Assert.Equal(2, await context.PurchaseOrderReceiptPostings.CountAsync());
        Assert.Equal(2, await context.RestockFulfillmentPostings.CountAsync());
        Assert.Empty(context.InventoryTransactions);
    }

    [Fact]
    public async Task RejectedReceipt_DoesNotFulfillRestock_AndReplayDoesNotDuplicatePoPosting()
    {
        using var context = CreateDbContext();
        var request = await SeedFoundationAsync(context, 20m);
        var (_, line) = await SeedSentOrderAsync(context, request, 20m);
        var service = CreateService(context);
        var receiptLine = await SeedReceiptLineAsync(context, line, request, 0m, 5m, "R178-REJECT");

        var first = await service.RegisterReceiptPostingAsync(receiptLine.BranchReceipt, receiptLine, StaffId);
        var replay = await service.RegisterReceiptPostingAsync(receiptLine.BranchReceipt, receiptLine, StaffId);

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(replay.IsSuccess, replay.Message);
        var posting = await context.PurchaseOrderReceiptPostings.SingleAsync();
        Assert.Equal(0m, posting.AcceptedBaseQuantity);
        Assert.Equal(5m, posting.RejectedBaseQuantity);
        Assert.Empty(context.RestockFulfillmentPostings);
        Assert.Equal(RestockRequestStatuses.Processing, (await context.RestockRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task CrossStoreRestockPoLink_AndOverReceipt_AreRejected()
    {
        using var context = CreateDbContext();
        var request = await SeedFoundationAsync(context, 20m);
        var service = CreateService(context);
        var crossStoreRequest = new RestockRequest
        {
            StoreId = OtherStoreId,
            IngredientId = IngredientId,
            RequestedQuantity = 10m,
            Status = RestockRequestStatuses.Processing,
            Priority = RestockRequestPriorities.Normal,
            CreatedByStaffId = StaffId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        context.RestockRequests.Add(crossStoreRequest);
        await context.SaveChangesAsync();
        var offerId = await context.IngredientSuppliers
            .Where(x => x.IngredientId == IngredientId && x.SupplierId == SupplierId)
            .Select(x => x.IngredientSupplierId)
            .SingleAsync();
        var crossStoreLink = await service.CreateDraftAsync(new CreatePurchaseOrderRequest
        {
            StoreId = StoreId,
            SupplierId = SupplierId,
            Lines =
            {
                new CreatePurchaseOrderLineRequest
                {
                    RestockRequestId = crossStoreRequest.RestockRequestId,
                    IngredientId = IngredientId,
                    IngredientSupplierId = offerId,
                    PackageCount = 1m
                }
            }
        }, StaffId, new[] { RoleConstants.AccountantWarehouse });

        var (_, line) = await SeedSentOrderAsync(context, request, 20m);
        var receiptLine = await SeedReceiptLineAsync(context, line, request, 21m, 0m, "R178-OVER");
        var over = await service.ValidateReceiptLineAsync(receiptLine.BranchReceipt, receiptLine);
        receiptLine.BranchReceipt.StoreId = OtherStoreId;
        var crossStore = await service.ValidateReceiptLineAsync(receiptLine.BranchReceipt, receiptLine);

        Assert.False(crossStoreLink.IsSuccess);
        Assert.False(over.IsSuccess);
        Assert.False(crossStore.IsSuccess);
    }

    [Fact]
    public async Task TransferAndPurchase_CanSplitFulfillment()
    {
        using var context = CreateDbContext();
        var request = await SeedFoundationAsync(context, 20m);
        var restockPostings = new RestockFulfillmentPostingService(context);
        var transfer = await restockPostings.RegisterAsync(new RegisterRestockFulfillmentPostingCommand
        {
            RestockRequestId = request.RestockRequestId,
            DestinationStoreId = StoreId,
            SourceDocumentType = RestockFulfillmentDocumentTypes.InventoryTransfer,
            SourceDocumentId = 17880,
            SourceDocumentLineId = 17881,
            IngredientId = IngredientId,
            Quantity = 8m,
            BaseUnitId = UnitId,
            ActorStaffId = StaffId,
            Reason = "Điều chuyển một phần"
        });
        Assert.True(transfer.IsSuccess, transfer.Message);
        await context.SaveChangesAsync();

        var (_, line) = await SeedSentOrderAsync(context, request, 12m);
        var receiptLine = await SeedReceiptLineAsync(context, line, request, 12m, 0m, "R178-SPLIT");
        var purchase = await CreateService(context)
            .RegisterReceiptPostingAsync(receiptLine.BranchReceipt, receiptLine, StaffId);
        var received = await restockPostings.RegisterAsync(ToRestockCommand(receiptLine));
        Assert.True(purchase.IsSuccess, purchase.Message);
        Assert.True(received.IsSuccess, received.Message);
        await context.SaveChangesAsync();

        Assert.Equal(RestockRequestStatuses.Completed, (await context.RestockRequests.SingleAsync()).Status);
        var fulfilledQuantities = await context.RestockFulfillmentPostings.Select(x => x.Quantity).ToListAsync();
        Assert.Equal(20m, fulfilledQuantities.Sum());
        Assert.Equal(2, await context.RestockFulfillmentPostings.CountAsync());
        Assert.Equal(1, await context.PurchaseOrderReceiptPostings.CountAsync());
    }

    [Fact]
    public async Task IncomingProvider_CountsOnlyActivePoRemainingUnreceived()
    {
        using var context = CreateDbContext();
        var request = await SeedFoundationAsync(context, 20m);
        var (_, line) = await SeedSentOrderAsync(context, request, 20m);
        var receiptLine = await SeedReceiptLineAsync(context, line, request, 8m, 2m, "R178-INCOMING");
        Assert.True((await CreateService(context).RegisterReceiptPostingAsync(receiptLine.BranchReceipt, receiptLine, StaffId)).IsSuccess);

        var incoming = await new PurchaseOrderQuantityProvider(context)
            .GetIncomingBaseQuantitiesAsync(StoreId, new[] { IngredientId });

        Assert.Equal(12m, incoming[IngredientId]);
    }

    [Fact]
    public async Task RejectedQuantity_DoesNotReducePurchaseOrderRemaining()
    {
        using var context = CreateDbContext();
        var request = await SeedFoundationAsync(context, 20m);
        var (order, line) = await SeedSentOrderAsync(context, request, 20m);
        var receiptLine = await SeedReceiptLineAsync(context, line, request, 5m, 1m, "R178-REMAINING");
        var service = CreateService(context);

        var posted = await service.RegisterReceiptPostingAsync(receiptLine.BranchReceipt, receiptLine, StaffId);
        var detail = await service.GetDetailAsync(
            order.PurchaseOrderId, StaffId, new[] { RoleConstants.AccountantWarehouse });

        Assert.True(posted.IsSuccess, posted.Message);
        Assert.True(detail.IsSuccess, detail.Message);
        Assert.Equal(15m, detail.Data!.Lines.Single().RemainingBaseQuantity);
        Assert.Equal(5m, detail.Data.Lines.Single().AcceptedBaseQuantity);
        Assert.Equal(1m, detail.Data.Lines.Single().RejectedBaseQuantity);
    }

    [Fact]
    public async Task CloseRemaining_RequiresOwnerAndReason_WritesAuditWithoutInventoryOrFulfillment()
    {
        using var context = CreateDbContext();
        var request = await SeedFoundationAsync(context, 20m);
        var (order, line) = await SeedSentOrderAsync(context, request, 20m);
        var service = CreateService(context);
        var rowVersion = Convert.ToBase64String(line.RowVersion);

        var unauthorized = await service.CloseLineRemainingAsync(new ClosePurchaseOrderLineRemainingRequest
        {
            PurchaseOrderLineId = line.PurchaseOrderLineId,
            RowVersion = rowVersion,
            Reason = "Không yêu cầu giao bù",
            RequestKey = "issue178-unauthorized"
        }, StaffId, new[] { RoleConstants.AccountantWarehouse });
        var missingReason = await service.CloseLineRemainingAsync(new ClosePurchaseOrderLineRemainingRequest
        {
            PurchaseOrderLineId = line.PurchaseOrderLineId,
            RowVersion = rowVersion,
            Reason = " ",
            RequestKey = "issue178-missing-reason"
        }, StaffId, new[] { RoleConstants.BusinessOwner });
        var closed = await service.CloseLineRemainingAsync(new ClosePurchaseOrderLineRemainingRequest
        {
            PurchaseOrderLineId = line.PurchaseOrderLineId,
            RowVersion = rowVersion,
            Reason = "NCC ngừng giao phần thiếu; Owner chấp thuận",
            RequestKey = "issue178-close-remaining"
        }, StaffId, new[] { RoleConstants.BusinessOwner });

        Assert.False(unauthorized.IsSuccess);
        Assert.False(missingReason.IsSuccess);
        Assert.True(closed.IsSuccess, closed.Message);
        var persisted = await context.PurchaseOrderLines.AsNoTracking().SingleAsync();
        Assert.Equal(20m, persisted.ClosedRemainingQuantity);
        Assert.Equal(StaffId, persisted.ClosedRemainingByStaffId);
        Assert.NotNull(persisted.ClosedRemainingAtUtc);
        Assert.Contains("Owner", persisted.CloseRemainingReason);
        Assert.Equal(PurchaseOrderStatuses.Completed,
            (await context.PurchaseOrders.AsNoTracking().SingleAsync()).Status);
        Assert.Empty(context.InventoryTransactions);
        Assert.Empty(context.InventoryCostLayers);
        Assert.Empty(context.RestockFulfillmentPostings);
        Assert.Equal(RestockRequestStatuses.Processing,
            (await context.RestockRequests.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task CloseRemaining_RequiresPositiveRemaining()
    {
        using var context = CreateDbContext();
        var request = await SeedFoundationAsync(context, 20m);
        var (_, line) = await SeedSentOrderAsync(context, request, 20m);
        var receiptLine = await SeedReceiptLineAsync(context, line, request, 20m, 0m, "R178-FULL");
        var service = CreateService(context);
        Assert.True((await service.RegisterReceiptPostingAsync(
            receiptLine.BranchReceipt, receiptLine, StaffId)).IsSuccess);

        var result = await service.CloseLineRemainingAsync(
            new ClosePurchaseOrderLineRemainingRequest
            {
                PurchaseOrderLineId = line.PurchaseOrderLineId,
                RowVersion = Convert.ToBase64String(line.RowVersion),
                Reason = "Không còn phần nào phải giao",
                RequestKey = "issue178-no-remaining"
            },
            StaffId,
            new[] { RoleConstants.BusinessOwner });

        Assert.False(result.IsSuccess);
        Assert.Equal(0m, (await context.PurchaseOrderLines.AsNoTracking().SingleAsync())
            .ClosedRemainingQuantity);
    }

    [Fact]
    public void PoUi_ShowsCompletedWithShortfallDistinctFromFullyReceived()
    {
        var root = FindRepoRoot();
        var view = File.ReadAllText(Path.Combine(
            root,
            "CafeChain",
            "Areas",
            "Admin",
            "Views",
            "AdminPurchaseOrders",
            "Details.cshtml"));

        var statusDisplay = File.ReadAllText(Path.Combine(
            root,
            "CafeChain",
            "ViewModels",
            "Admin",
            "Shared",
            "AdminStatusDescriptor.cs"));

        Assert.Contains("AdminStatusDisplay.PurchaseOrder(Model.Status)", view);
        Assert.Contains("Hoàn thành", statusDisplay);
        Assert.Contains("Hoàn thành có phần không giao bù", view);
        Assert.Contains("ClosedRemainingQuantity", view);
    }

    [Fact]
    public async Task PurchaseAllocation_RejectsOverage_UnlessBusinessOwnerProvidesReason()
    {
        using var context = CreateDbContext();
        var request = await SeedFoundationAsync(context, 20m);
        var offerId = await context.IngredientSuppliers
            .Where(x => x.IngredientId == IngredientId && x.SupplierId == SupplierId)
            .Select(x => x.IngredientSupplierId)
            .SingleAsync();
        var service = CreateService(context);
        var input = new CreatePurchaseOrderRequest
        {
            StoreId = StoreId,
            SupplierId = SupplierId,
            Lines =
            {
                new CreatePurchaseOrderLineRequest
                {
                    RestockRequestId = request.RestockRequestId,
                    IngredientId = IngredientId,
                    IngredientSupplierId = offerId,
                    PackageCount = 3m
                }
            }
        };

        var rejected = await service.CreateDraftAsync(
            input, StaffId, new[] { RoleConstants.AccountantWarehouse });
        input.AllowOverallocationOverride = true;
        input.OverallocationOverrideReason = "Dự phòng cao điểm đã được chủ doanh nghiệp duyệt";
        var approved = await service.CreateDraftAsync(
            input, StaffId, new[] { RoleConstants.BusinessOwner });

        Assert.False(rejected.IsSuccess);
        Assert.True(approved.IsSuccess, approved.Message);
        Assert.Contains(await context.RestockRequestTransitions.ToListAsync(),
            x => x.Reason != null && x.Reason.StartsWith("OVER_ALLOCATION_OVERRIDE:"));
    }

    [Fact]
    public async Task PurchaseOrder_ConvertsIngredientSpecificPackageUnit_ToInventoryBaseUnit()
    {
        using var context = CreateDbContext();
        var request = await SeedFoundationAsync(context, 480m);
        const int packageUnitId = 17806;
        context.Units.Add(new Unit
        {
            UnitId = packageUnitId,
            UnitCode = "can178",
            Name = "Can #178",
            Type = UnitType.Dem,
            Active = true
        });
        context.UnitConversions.Add(new UnitConversion
        {
            IngredientId = IngredientId,
            FromUnitId = packageUnitId,
            FromQuantity = 1m,
            ToUnitId = UnitId,
            ToQuantity = 10m,
            Active = true
        });
        var offer = await context.IngredientSuppliers.SingleAsync(
            x => x.IngredientId == IngredientId && x.SupplierId == SupplierId);
        offer.UnitId = packageUnitId;
        offer.PackageQuantity = 24m;
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateDraftAsync(
            new CreatePurchaseOrderRequest
            {
                StoreId = StoreId,
                SupplierId = SupplierId,
                Lines =
                {
                    new CreatePurchaseOrderLineRequest
                    {
                        RestockRequestId = request.RestockRequestId,
                        IngredientId = IngredientId,
                        IngredientSupplierId = offer.IngredientSupplierId,
                        PackageCount = 2m
                    }
                }
            },
            StaffId,
            new[] { RoleConstants.AccountantWarehouse });

        Assert.True(result.IsSuccess, result.Message);
        var line = await context.PurchaseOrderLines.AsNoTracking().SingleAsync();
        Assert.Equal(packageUnitId, line.PackageUnitIdSnapshot);
        Assert.Equal(24m, line.PackageQuantitySnapshot);
        Assert.Equal(480m, line.OrderedBaseQuantity);
    }

    private static PurchaseOrderService CreateService(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
        var conversion = new UnitConversionService(
            context,
            NullLogger<UnitConversionService>.Instance,
            physical);
        return new PurchaseOrderService(
            context,
            conversion,
            new RestockAllocationService(context, new NoPurchaseOrderAllocationProvider()));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CafeChain", "CafeChain.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy thư mục gốc CafeChain.slnx.");
    }

    private static async Task<RestockRequest> SeedFoundationAsync(AppDbContext context, decimal requested)
    {
        context.Stores.AddRange(
            new Store { StoreId = StoreId, Name = "Store #178", Address = "Test", Phone = "0900178000", Active = true, CreatedAt = DateTime.UtcNow },
            new Store { StoreId = OtherStoreId, Name = "Other #178", Address = "Test", Phone = "0900178100", Active = true, CreatedAt = DateTime.UtcNow });
        context.Accounts.Add(new Account { AccountId = StaffId, Email = "staff178@test.local", PasswordHash = "x", Active = true, CreatedAt = DateTime.UtcNow });
        context.Staffs.Add(new Staff { StaffId = StaffId, AccountId = StaffId, StoreId = StoreId, FullName = "Warehouse #178", Active = true, CreatedAt = DateTime.UtcNow});
        context.Units.Add(new Unit { UnitId = UnitId, UnitCode = "kg178", Name = "Kilogram #178", Active = true });
        context.Ingredients.Add(new Ingredient { IngredientId = IngredientId, Code = "ING-178", Name = "Ingredient #178", BaseUnitId = UnitId, Active = true });
        context.Suppliers.Add(new Supplier { SupplierId = SupplierId, Code = "SUP-178", Name = "Supplier #178", Active = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.SupplierStores.Add(new SupplierStore { SupplierId = SupplierId, StoreId = StoreId, Active = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.IngredientSuppliers.Add(new IngredientSupplier { IngredientId = IngredientId, SupplierId = SupplierId, UnitId = UnitId, PackageQuantity = 10m, CurrentPrice = 100m, MinimumOrderPackageCount = 1, LeadTimeDays = 2, Active = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        var request = new RestockRequest { StoreId = StoreId, IngredientId = IngredientId, RequestedQuantity = requested, Status = RestockRequestStatuses.Processing, Priority = RestockRequestPriorities.Normal, CreatedByStaffId = StaffId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.RestockRequests.Add(request);
        await context.SaveChangesAsync();
        return request;
    }

    private static async Task<(PurchaseOrder Order, PurchaseOrderLine Line)> SeedSentOrderAsync(AppDbContext context, RestockRequest request, decimal ordered)
    {
        var offer = await context.IngredientSuppliers.SingleAsync(
            x => x.IngredientId == IngredientId && x.SupplierId == SupplierId);
        var order = new PurchaseOrder { Code = "PO-178-" + Guid.NewGuid().ToString("N")[..6], StoreId = StoreId, SupplierId = SupplierId, Status = PurchaseOrderStatuses.MarkedAsSent, OrderDate = DateTime.UtcNow, CreatedByStaffId = StaffId, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var line = new PurchaseOrderLine { RestockRequestId = request.RestockRequestId, IngredientId = IngredientId, IngredientSupplierId = offer.IngredientSupplierId, PackageUnitIdSnapshot = UnitId, PackageQuantitySnapshot = 10m, PackagePriceSnapshot = 100m, PackageCount = ordered / 10m, OrderedBaseQuantity = ordered, PromisedLeadTimeDaysSnapshot = 2 };
        order.Lines.Add(line);
        context.PurchaseOrders.Add(order);
        await context.SaveChangesAsync();
        return (order, line);
    }

    private static async Task<BranchReceiptLine> SeedReceiptLineAsync(AppDbContext context, PurchaseOrderLine poLine, RestockRequest request, decimal accepted, decimal rejected, string key)
    {
        var receipt = new BranchReceipt { ReceiptCode = key, ReceiptKey = key, StoreId = StoreId, SupplierId = SupplierId, Status = BranchReceiptStatuses.Draft, ReceivedAt = DateTime.UtcNow, ReceivedByStaffId = StaffId, CreatedAt = DateTime.UtcNow, CreatedByStaffId = StaffId };
        var line = new BranchReceiptLine { PurchaseOrderLineId = poLine.PurchaseOrderLineId, RestockRequestId = request.RestockRequestId, IngredientId = IngredientId, InputQuantity = accepted, InputUnitId = UnitId, ReceivedBaseQuantity = accepted, RejectedBaseQuantity = rejected, BaseUnitId = UnitId, BaseUnitCostSnapshot = 10m, LineTotalCost = accepted * 10m, CreatedAt = DateTime.UtcNow };
        receipt.Lines.Add(line);
        context.BranchReceipts.Add(receipt);
        await context.SaveChangesAsync();
        return line;
    }

    private static RegisterRestockFulfillmentPostingCommand ToRestockCommand(BranchReceiptLine line) => new()
    {
        RestockRequestId = line.RestockRequestId!.Value,
        DestinationStoreId = line.BranchReceipt.StoreId,
        SourceDocumentType = RestockFulfillmentDocumentTypes.BranchReceipt,
        SourceDocumentId = line.BranchReceiptId,
        SourceDocumentLineId = line.BranchReceiptLineId,
        IngredientId = line.IngredientId,
        Quantity = line.ReceivedBaseQuantity,
        BaseUnitId = line.BaseUnitId,
        ActorStaffId = StaffId,
        Reason = "Issue #178 test"
    };
}
