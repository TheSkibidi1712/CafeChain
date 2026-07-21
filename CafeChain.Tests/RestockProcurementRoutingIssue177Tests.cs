using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Transfers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class RestockProcurementRoutingIssue177Tests : IntegrationTestBase
{
    private const int StoreId = 1770;
    private const int SourceStoreId = 1771;
    private const int IngredientId = 17701;
    private const int UnitId = 17702;
    private const int ManagerId = 17703;
    private const int WarehouseId = 17704;

    [Fact]
    public async Task RestockRequest_SubmittedCanBeAcceptedForProcessing()
    {
        using var context = CreateDbContext();
        var request = await SeedRequestAsync(context, RestockRequestStatuses.Draft, 20m);
        var service = CreateWorkflow(context);

        var submitted = await service.SubmitAsync(
            request.RestockRequestId,
            ManagerId,
            StoreId,
            new[] { RoleConstants.StoreManager },
            Convert.ToBase64String(request.RowVersion));
        var accepted = await service.StartProcessingAsync(
            request.RestockRequestId, WarehouseId, null,
            new[] { RoleConstants.AccountantWarehouse }, "Ưu tiên điều chuyển trước",
            submitted.Data!.RowVersion);

        Assert.True(submitted.IsSuccess, submitted.Message);
        Assert.True(accepted.IsSuccess, accepted.Message);
        var stored = await context.RestockRequests.AsNoTracking().SingleAsync();
        Assert.Equal(RestockRequestStatuses.Processing, stored.Status);
        Assert.Equal(WarehouseId, stored.AcceptedByStaffId);
        Assert.NotNull(stored.AcceptedAtUtc);
        Assert.Equal("Ưu tiên điều chuyển trước", stored.ProcessingNote);
        Assert.Equal(2, await context.RestockRequestTransitions.CountAsync());
    }

    [Fact]
    public async Task RestockRequest_AreaManagerMutation_IsRejected()
    {
        using var context = CreateDbContext();
        var request = await SeedRequestAsync(context, RestockRequestStatuses.Submitted, 20m);

        var result = await CreateWorkflow(context).StartProcessingAsync(
            request.RestockRequestId,
            ManagerId,
            StoreId,
            new[] { RoleConstants.AreaManager },
            "attempt",
            Convert.ToBase64String(request.RowVersion));

        Assert.False(result.IsSuccess);
        Assert.Equal(BranchReceiptErrorCodes.Unauthorized, result.ErrorCode);
        Assert.Equal(RestockRequestStatuses.Submitted,
            (await context.RestockRequests.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task RestockSubmit_RevalidatesRequestedQuantity()
    {
        using var context = CreateDbContext();
        var request = await SeedRequestAsync(context, RestockRequestStatuses.Draft, 0m);
        var service = CreateWorkflow(context);

        var result = await service.SubmitAsync(
            request.RestockRequestId,
            ManagerId,
            StoreId,
            new[] { RoleConstants.StoreManager },
            Convert.ToBase64String(request.RowVersion));

        Assert.False(result.IsSuccess);
        Assert.Equal(BranchReceiptErrorCodes.QuantityInvalid, result.ErrorCode);
        Assert.Equal(RestockRequestStatuses.Draft,
            (await context.RestockRequests.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task RestockSubmit_MissingOrStaleRowVersionRejected()
    {
        using var context = CreateDbContext();
        var request = await SeedRequestAsync(context, RestockRequestStatuses.Draft, 10m);
        var service = CreateWorkflow(context);

        var missing = await service.SubmitAsync(
            request.RestockRequestId, ManagerId, StoreId,
            new[] { RoleConstants.StoreManager }, null);
        var stale = await service.SubmitAsync(
            request.RestockRequestId, ManagerId, StoreId,
            new[] { RoleConstants.StoreManager },
            Convert.ToBase64String(new byte[] { 9 }));

        Assert.False(missing.IsSuccess);
        Assert.Equal(BranchReceiptErrorCodes.ValidationRowVersionRequired, missing.ErrorCode);
        Assert.False(stale.IsSuccess);
        Assert.Equal(BranchReceiptErrorCodes.ResourceChanged, stale.ErrorCode);
    }

    [Fact]
    public async Task TransferAndPurchaseAllocations_AreSeparatedAndCannotExceedRemaining()
    {
        using var context = CreateDbContext();
        var request = await SeedRequestAsync(context, RestockRequestStatuses.Processing, 20m);
        context.InventoryTransfers.Add(new InventoryTransfer
        {
            InventoryTransferId = 17710,
            Code = "TR-177",
            FromStoreId = SourceStoreId,
            ToStoreId = StoreId,
            Type = InventoryTransferType.STORE_TO_STORE,
            Purpose = InventoryTransferPurpose.REPLENISHMENT,
            Status = InventoryTransferStatus.DRAFT,
            DocumentDate = DateTime.UtcNow,
            CreatedByStaffId = WarehouseId,
            CreatedAt = DateTime.UtcNow,
            Details = new List<InventoryTransferDetail>
            {
                new()
                {
                    IngredientId = IngredientId,
                    RestockRequestId = request.RestockRequestId,
                    UnitId = UnitId,
                    Quantity = 8m,
                    BaseQuantity = 8m
                }
            }
        });
        await context.SaveChangesAsync();
        var service = new RestockAllocationService(context, new FixedPurchaseAllocationProvider(7m));

        var summary = await service.GetSummaryAsync(request.RestockRequestId);
        var over = await service.ValidateAllocationAsync(new RestockAllocationValidationRequest
        {
            RestockRequestId = request.RestockRequestId,
            DestinationStoreId = StoreId,
            IngredientId = IngredientId,
            AllocationQuantity = 6m,
            ActorStaffId = WarehouseId,
            ActorRoles = new[] { RoleConstants.AccountantWarehouse }
        });

        Assert.NotNull(summary);
        Assert.Equal(8m, summary.TransferAllocatedQuantity);
        Assert.Equal(7m, summary.PurchaseAllocatedQuantity);
        Assert.Equal(5m, summary.RemainingUnallocatedQuantity);
        Assert.False(over.IsSuccess);
    }

    [Fact]
    public async Task BusinessOwnerOverallocation_RequiresReasonAndWritesAudit()
    {
        using var context = CreateDbContext();
        var request = await SeedRequestAsync(context, RestockRequestStatuses.Processing, 10m);
        var service = new RestockAllocationService(context, new FixedPurchaseAllocationProvider(9m));

        var denied = await service.ValidateAllocationAsync(new RestockAllocationValidationRequest
        {
            RestockRequestId = request.RestockRequestId,
            DestinationStoreId = StoreId,
            IngredientId = IngredientId,
            AllocationQuantity = 2m,
            AllowOverallocationOverride = true,
            ActorStaffId = ManagerId,
            ActorRoles = new[] { RoleConstants.BusinessOwner }
        });
        var allowed = await service.ValidateAllocationAsync(new RestockAllocationValidationRequest
        {
            RestockRequestId = request.RestockRequestId,
            DestinationStoreId = StoreId,
            IngredientId = IngredientId,
            AllocationQuantity = 2m,
            AllowOverallocationOverride = true,
            OverrideReason = "Dự phòng hao hụt nhà cung cấp",
            ActorStaffId = ManagerId,
            ActorRoles = new[] { RoleConstants.BusinessOwner },
            RequestKey = "override-177"
        });
        await context.SaveChangesAsync();

        Assert.False(denied.IsSuccess);
        Assert.True(allowed.IsSuccess, allowed.Message);
        var audit = await context.RestockRequestTransitions.SingleAsync();
        Assert.Contains("OVER_ALLOCATION_OVERRIDE", audit.Reason);
        Assert.Equal("override-177", audit.RequestKey);
    }

    [Fact]
    public async Task CloseRemaining_RequiresReason_AndDoesNotCreateInventoryEvidence()
    {
        using var context = CreateDbContext();
        var request = await SeedRequestAsync(context, RestockRequestStatuses.Processing, 20m);
        var service = CreateWorkflow(context);

        var missingReason = await service.CloseRemainingAsync(
            request.RestockRequestId, WarehouseId, null,
            new[] { RoleConstants.AccountantWarehouse }, "",
            await RequestVersionAsync(context, request.RestockRequestId));
        var closed = await service.CloseRemainingAsync(
            request.RestockRequestId, WarehouseId, null,
            new[] { RoleConstants.AccountantWarehouse }, "Nhà cung cấp ngừng mặt hàng",
            await RequestVersionAsync(context, request.RestockRequestId));

        Assert.False(missingReason.IsSuccess);
        Assert.True(closed.IsSuccess, closed.Message);
        var stored = await context.RestockRequests.AsNoTracking().SingleAsync();
        Assert.Equal(RestockRequestStatuses.Completed, stored.Status);
        Assert.Equal(20m, stored.ClosedRemainingQuantity);
        Assert.Equal("Nhà cung cấp ngừng mặt hàng", stored.RemainingCloseReason);
        Assert.Empty(context.InventoryTransactions);
        Assert.Empty(context.RestockFulfillmentPostings);
    }

    [Fact]
    public void Model_DoesNotIntroducePurchaseRequestAuthority()
    {
        using var context = CreateDbContext();
        Assert.DoesNotContain(
            context.Model.GetEntityTypes(),
            entity => entity.ClrType.Name is "PurchaseRequest" or "PurchaseRequestLine");
    }

    private static RestockRequestWorkflowService CreateWorkflow(AppDbContext context)
    {
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        return new RestockRequestWorkflowService(
            context,
            scope.Object,
            NullLogger<RestockRequestWorkflowService>.Instance,
            new RestockAllocationService(context, new NoPurchaseOrderAllocationProvider()));
    }

    private static async Task<string> RequestVersionAsync(AppDbContext context, int requestId) =>
        Convert.ToBase64String(await context.RestockRequests.AsNoTracking()
            .Where(x => x.RestockRequestId == requestId)
            .Select(x => x.RowVersion)
            .SingleAsync());

    private static async Task<RestockRequest> SeedRequestAsync(
        AppDbContext context,
        string status,
        decimal quantity)
    {
        if (!await context.Stores.AnyAsync(x => x.StoreId == StoreId))
        {
            context.Stores.AddRange(
                new Store
                {
                    StoreId = StoreId,
                    Name = "Store #177",
                    Address = "Test",
                    Phone = "0900177000",
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Store
                {
                    StoreId = SourceStoreId,
                    Name = "Source Store #177",
                    Address = "Test",
                    Phone = "0900177100",
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                });
            context.Units.Add(new Unit
            {
                UnitId = UnitId,
                UnitCode = "kg177",
                Name = "Kilogram #177",
                Active = true
            });
            context.Ingredients.Add(new Ingredient
            {
                IngredientId = IngredientId,
                Code = "ING-177",
                Name = "Ingredient #177",
                BaseUnitId = UnitId,
                Active = true
            });
            context.StoreInventories.Add(new StoreInventory
            {
                StoreId = StoreId,
                IngredientId = IngredientId,
                AvailableQty = 0,
                ReservedQty = 0,
                LastUpdated = DateTime.UtcNow,
                RowVersion = new byte[] { 0 }
            });
            context.Accounts.AddRange(
                new Account
                {
                    AccountId = ManagerId,
                    Email = "manager177@test.local",
                    PasswordHash = "x",
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                },
                new Account
                {
                    AccountId = WarehouseId,
                    Email = "warehouse177@test.local",
                    PasswordHash = "x",
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                });
            context.Staffs.AddRange(
                new Staff
                {
                    StaffId = ManagerId,
                    AccountId = ManagerId,
                    StoreId = StoreId,
                    FullName = "Manager #177",
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                },
                new Staff
                {
                    StaffId = WarehouseId,
                    AccountId = WarehouseId,
                    StoreId = StoreId,
                    FullName = "Warehouse #177",
                    Active = true,
                    CreatedAt = DateTime.UtcNow,
                });
        }

        var request = new RestockRequest
        {
            StoreId = StoreId,
            IngredientId = IngredientId,
            RequestedQuantity = quantity,
            SuggestedQuantity = quantity,
            Status = status,
            Priority = RestockRequestPriorities.Normal,
            CreatedByStaffId = ManagerId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = new byte[] { 0 }
        };
        context.RestockRequests.Add(request);
        await context.SaveChangesAsync();
        return request;
    }

    private sealed class FixedPurchaseAllocationProvider : IRestockPurchaseAllocationProvider
    {
        private readonly decimal _quantity;

        public FixedPurchaseAllocationProvider(decimal quantity) => _quantity = quantity;

        public Task<decimal> GetAllocatedBaseQuantityAsync(
            int restockRequestId,
            int? excludePurchaseOrderLineId = null) => Task.FromResult(_quantity);
    }
}
