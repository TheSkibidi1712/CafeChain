using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Transfers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class PurchaseAdviceIssue184Tests : IntegrationTestBase
{
    [Fact]
    public async Task PurchaseAdvice_CreateFromRestockRemaining_AndPrefillsStoreAndIngredient()
    {
        using var context = CreateDbContext();
        var seed = await SeedAsync(context, 20m);
        var result = await CreateService(context).CreateAsync(CreateRequest(seed, 12m), Manager(seed));

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(seed.StoreId, result.Data!.StoreId);
        Assert.Equal(seed.IngredientId, result.Data.Lines.Single().IngredientId);
        Assert.Equal(PurchaseAdviceStatuses.Draft, result.Data.Status);
        Assert.StartsWith("PA-", result.Data.AdviceNumber);
    }

    [Fact]
    public async Task PurchaseAdvice_CannotExceedRestockRemaining()
    {
        using var context = CreateDbContext();
        var seed = await SeedAsync(context, 10m);
        var result = await CreateService(context).CreateAsync(CreateRequest(seed, 11m), Manager(seed));
        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.ExceedsRestockRemaining, result.ErrorCode);
    }

    [Fact]
    public async Task PurchaseAdvice_ExplicitlyClosedQuantityIsNotPurchasable()
    {
        using var context = CreateDbContext();
        var seed = await SeedAsync(context, 10m);
        var restock = await context.RestockRequests.SingleAsync(x => x.RestockRequestId == seed.RestockRequestId);
        restock.ClosedRemainingQuantity = 4m;
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(CreateRequest(seed, 7m), Manager(seed));
        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.ExceedsRestockRemaining, result.ErrorCode);
    }

    [Fact]
    public async Task PurchaseAdvice_CannotIncludeTransferredOrPurchasedQuantity()
    {
        using var context = CreateDbContext();
        var seed = await SeedAsync(context, 20m);
        await AddTransferAsync(context, seed, 8m);
        await AddPurchaseOrderAsync(context, seed, 5m);

        var result = await CreateService(context).CreateAsync(CreateRequest(seed, 8m), Manager(seed));
        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.ExceedsRestockRemaining, result.ErrorCode);
    }

    [Fact]
    public async Task PurchaseAdvice_DuplicateActiveAdviceRejected()
    {
        using var context = CreateDbContext();
        var seed = await SeedAsync(context, 20m);
        var service = CreateService(context);
        Assert.True((await service.CreateAsync(CreateRequest(seed, 5m), Manager(seed))).IsSuccess);
        var duplicate = await service.CreateAsync(CreateRequest(seed, 5m), Manager(seed));
        Assert.False(duplicate.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.AlreadyExists, duplicate.ErrorCode);
    }

    [Fact]
    public async Task PurchaseAdvice_DraftCanBeEdited()
    {
        using var context = CreateDbContext();
        var seed = await SeedAsync(context, 20m);
        var service = CreateService(context);
        var created = (await service.CreateAsync(CreateRequest(seed, 5m), Manager(seed))).Data!;
        var line = created.Lines.Single();
        var updated = await service.UpdateAsync(new UpdatePurchaseAdviceRequest
        {
            PurchaseAdviceId = created.PurchaseAdviceId,
            NeededByDate = DateTime.Today.AddDays(4),
            Priority = PurchaseAdvicePriorities.High,
            RowVersion = created.RowVersion,
            Lines = new List<UpdatePurchaseAdviceLineRequest>
            {
                new() { PurchaseAdviceLineId = line.PurchaseAdviceLineId, RequestedPurchaseBaseQuantity = 7m, NeededByDate = DateTime.Today.AddDays(4), RowVersion = line.RowVersion }
            }
        }, Manager(seed));
        Assert.True(updated.IsSuccess, updated.Message);
        Assert.Equal(7m, updated.Data!.Lines.Single().RequestedPurchaseBaseQuantity);
    }

    [Fact]
    public async Task PurchaseAdvice_SubmitRequiresLines()
    {
        using var context = CreateDbContext();
        var seed = await SeedAsync(context, 10m);
        var result = await CreateService(context).CreateAsync(new CreatePurchaseAdviceRequest
        {
            StoreId = seed.StoreId, RequestKey = Guid.NewGuid().ToString("N"), Priority = PurchaseAdvicePriorities.Normal, NeededByDate = DateTime.Today.AddDays(1)
        }, Manager(seed));
        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.Empty, result.ErrorCode);
    }

    [Fact]
    public async Task PurchaseAdvice_SubmitTransitionsAndDoubleSubmitIsIdempotent()
    {
        using var context = CreateDbContext();
        var seed = await SeedAsync(context, 10m);
        var service = CreateService(context);
        var created = (await service.CreateAsync(CreateRequest(seed, 5m), Manager(seed))).Data!;
        var request = new PurchaseAdviceTransitionRequest { RowVersion = created.RowVersion };
        var first = await service.SubmitAsync(created.PurchaseAdviceId, request, Manager(seed));
        var second = await service.SubmitAsync(created.PurchaseAdviceId,
            new PurchaseAdviceTransitionRequest { RowVersion = first.Data!.RowVersion }, Manager(seed));
        Assert.True(first.IsSuccess, first.Message);
        Assert.True(second.IsSuccess, second.Message);
        Assert.Equal(PurchaseAdviceStatuses.Submitted, second.Data!.Status);
        Assert.Equal(2, await context.PurchaseAdviceTransitions.CountAsync());
    }

    [Fact]
    public async Task PurchaseAdvice_CancelDraft_DeactivatesReservation()
    {
        using var context = CreateDbContext();
        var seed = await SeedAsync(context, 10m);
        var service = CreateService(context);
        var created = (await service.CreateAsync(CreateRequest(seed, 5m), Manager(seed))).Data!;
        var cancelled = await service.CancelAsync(created.PurchaseAdviceId,
            new PurchaseAdviceTransitionRequest { RowVersion = created.RowVersion }, Manager(seed));
        Assert.True(cancelled.IsSuccess, cancelled.Message);
        Assert.False((await context.PurchaseAdviceLines.AsNoTracking().SingleAsync()).IsActiveReservation);
    }

    [Fact]
    public async Task PurchaseAdvice_CannotCancelAfterReview()
    {
        using var context = CreateDbContext();
        var seed = await SeedAsync(context, 10m);
        var service = CreateScopedService(context, seed);
        var submitted = await SubmitAsync(service, seed);
        var review = await service.StartReviewAsync(submitted.PurchaseAdviceId,
            new PurchaseAdviceTransitionRequest { RowVersion = submitted.RowVersion }, Warehouse(seed));
        var cancelled = await service.CancelAsync(review.Data!.PurchaseAdviceId,
            new PurchaseAdviceTransitionRequest { RowVersion = review.Data.RowVersion }, Manager(seed));
        Assert.False(cancelled.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.NotEditable, cancelled.ErrorCode);
    }

    [Fact]
    public async Task PurchaseAdvice_RejectRequiresReason()
    {
        using var context = CreateDbContext();
        var seed = await SeedAsync(context, 10m);
        var service = CreateScopedService(context, seed);
        var submitted = await SubmitAsync(service, seed);
        var review = await service.StartReviewAsync(submitted.PurchaseAdviceId,
            new PurchaseAdviceTransitionRequest { RowVersion = submitted.RowVersion }, Warehouse(seed));
        var rejected = await service.RejectAsync(review.Data!.PurchaseAdviceId,
            new PurchaseAdviceTransitionRequest { RowVersion = review.Data.RowVersion }, Warehouse(seed));
        Assert.False(rejected.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.RejectionReasonRequired, rejected.ErrorCode);
    }

    [Fact]
    public async Task PurchaseAdvice_CannotEditAfterSubmit()
    {
        using var context = CreateDbContext();
        var seed = await SeedAsync(context, 10m);
        var service = CreateService(context);
        var submitted = await SubmitAsync(service, seed);
        var edit = await service.UpdateAsync(new UpdatePurchaseAdviceRequest
        {
            PurchaseAdviceId = submitted.PurchaseAdviceId,
            NeededByDate = submitted.NeededByDate,
            Priority = submitted.Priority,
            RowVersion = submitted.RowVersion,
            Lines = submitted.Lines.Select(x => new UpdatePurchaseAdviceLineRequest
            {
                PurchaseAdviceLineId = x.PurchaseAdviceLineId, RequestedPurchaseBaseQuantity = x.RequestedPurchaseBaseQuantity, RowVersion = x.RowVersion
            }).ToList()
        }, Manager(seed));
        Assert.False(edit.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.NotEditable, edit.ErrorCode);
    }

    [Fact]
    public async Task PurchaseAdvice_StaleVersionRejected()
    {
        using var context = CreateDbContext();
        var seed = await SeedAsync(context, 10m);
        var service = CreateService(context);
        var created = (await service.CreateAsync(CreateRequest(seed, 5m), Manager(seed))).Data!;
        var result = await service.SubmitAsync(created.PurchaseAdviceId,
            new PurchaseAdviceTransitionRequest { RowVersion = Convert.ToBase64String(new byte[] { 9 }) }, Manager(seed));
        Assert.False(result.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.StaleVersion, result.ErrorCode);
    }

    [Fact]
    public async Task StoreManager_OwnStoreCanCreateAdvice_OtherStoreRejected()
    {
        using var context = CreateDbContext();
        var seed = await SeedAsync(context, 10m);
        var service = CreateService(context);
        Assert.True((await service.CreateAsync(CreateRequest(seed, 5m), Manager(seed))).IsSuccess);
        var other = new AdminActorContext
        {
            StaffId = seed.ManagerId,
            StoreId = seed.StoreId + 99,
            RoleNames = new[] { RoleConstants.StoreManager }
        };
        var denied = await service.CreateAsync(CreateRequest(seed, 2m), other);
        Assert.False(denied.IsSuccess);
        Assert.Equal(PurchaseAdviceErrorCodes.Forbidden, denied.ErrorCode);
    }

    [Fact]
    public async Task AccountantWarehouse_WithoutStoreScope_CannotCreateOrReview()
    {
        using var context = CreateDbContext();
        var seed = await SeedAsync(context, 10m);
        var service = CreateService(context);
        var submitted = await SubmitAsync(service, seed);
        var review = await service.StartReviewAsync(submitted.PurchaseAdviceId,
            new PurchaseAdviceTransitionRequest { RowVersion = submitted.RowVersion }, Warehouse(seed));
        var create = await service.CreateAsync(CreateRequest(seed, 1m), Warehouse(seed));
        Assert.False(review.IsSuccess);
        Assert.False(create.IsSuccess);
        Assert.Empty(context.BranchReceipts);
        Assert.Empty(context.InventoryTransactions);
    }

    [Fact]
    public async Task AccountantWarehouse_WithStoreScope_CanCreateSubmitAndReview()
    {
        using var context = CreateDbContext();
        var seed = await SeedAsync(context, 10m);
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(seed.WarehouseId, seed.StoreId))
            .ReturnsAsync(true);
        var service = new PurchaseAdviceService(context, scope.Object);

        var created = await service.CreateAsync(CreateRequest(seed, 5m), Warehouse(seed));
        Assert.True(created.IsSuccess, created.Message);

        var submitted = await service.SubmitAsync(
            created.Data!.PurchaseAdviceId,
            new PurchaseAdviceTransitionRequest { RowVersion = created.Data.RowVersion },
            Warehouse(seed));
        Assert.True(submitted.IsSuccess, submitted.Message);

        var review = await service.StartReviewAsync(
            submitted.Data!.PurchaseAdviceId,
            new PurchaseAdviceTransitionRequest { RowVersion = submitted.Data.RowVersion },
            Warehouse(seed));

        Assert.True(review.IsSuccess, review.Message);
        Assert.Equal(PurchaseAdviceStatuses.UnderReview, review.Data!.Status);
    }

    [Fact]
    public async Task AreaManagerReadOnlyAndCashierForbidden()
    {
        using var context = CreateDbContext();
        var seed = await SeedAsync(context, 10m);
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(seed.AreaManagerId, seed.StoreId)).ReturnsAsync(true);
        var service = new PurchaseAdviceService(context, scope.Object);
        var created = (await service.CreateAsync(CreateRequest(seed, 5m), Manager(seed))).Data!;
        var areaRead = await service.GetDetailAsync(created.PurchaseAdviceId,
            new AdminActorContext { StaffId = seed.AreaManagerId, StoreId = 0, RoleNames = new[] { RoleConstants.AreaManager } });
        var areaEdit = await service.SubmitAsync(created.PurchaseAdviceId,
            new PurchaseAdviceTransitionRequest { RowVersion = created.RowVersion },
            new AdminActorContext { StaffId = seed.AreaManagerId, RoleNames = new[] { RoleConstants.AreaManager } });
        var cashierRead = await service.GetDetailAsync(created.PurchaseAdviceId,
            new AdminActorContext { StaffId = seed.ManagerId, StoreId = seed.StoreId, RoleNames = new[] { RoleConstants.SalesStaff } });
        Assert.True(areaRead.IsSuccess);
        Assert.False(areaEdit.IsSuccess);
        Assert.False(cashierRead.IsSuccess);
    }

    [Fact]
    public async Task PurchaseAdvice_ListReturnsSummaryWithoutCreatingPurchaseOrder()
    {
        using var context = CreateDbContext();
        var seed = await SeedAsync(context, 10m);
        var service = CreateService(context);
        Assert.True((await service.CreateAsync(CreateRequest(seed, 5m), Manager(seed))).IsSuccess);

        var page = await service.GetPageAsync(new PurchaseAdviceFilterDto { StoreId = seed.StoreId }, Manager(seed));
        Assert.True(page.IsSuccess, page.Message);
        Assert.Single(page.Data!.Items);
        Assert.Equal("#" + seed.RestockRequestId, page.Data.Items.Single().SourceRestockSummary);
        Assert.Empty(context.PurchaseOrders);
    }

    [Fact]
    public async Task DirectPurchase_CreatesImplicitDemandAndLinksPurchaseAllocation()
    {
        using var context = CreateDbContext();
        var seed = await SeedAsync(context, 8.75m);
        var procurementUnit = await context.Units
            .SingleAsync(x => x.UnitCode == "kg" && x.Active);
        var directIngredient = new Ingredient
        {
            Code = "ING-DIRECT-" + Guid.NewGuid().ToString("N")[..8],
            Name = "Direct coffee #184",
            BaseUnitId = seed.UnitId,
            Active = true
        };
        context.Ingredients.Add(directIngredient);
        await context.SaveChangesAsync();

        var conversion = new Mock<IUnitConversionService>();
        conversion.Setup(x => x.ConvertAsync(
                directIngredient.IngredientId,
                8.75m,
                procurementUnit.UnitId,
                It.IsAny<int?>()))
            .ReturnsAsync(ServiceResult<decimal>.Success(8.75m));

        var requestKey = "DIRECT-184-" + Guid.NewGuid().ToString("N");
        CreatePurchaseAdviceRequest DirectRequest() =>
            new()
            {
                IsDirectProposal = true,
                StoreId = seed.StoreId,
                RequestKey = requestKey,
                NeededByDate = DateTime.UtcNow.AddDays(3),
                Priority = PurchaseAdvicePriorities.Normal,
                Lines = new List<CreatePurchaseAdviceLineRequest>
                {
                    new()
                    {
                        IngredientId = directIngredient.IngredientId,
                        RequestedProcurementQuantity = 8.75m,
                        ProcurementUnitId = procurementUnit.UnitId
                    }
                }
            };
        var service = CreateService(context, conversion.Object);
        var result = await service.CreateDirectAsync(DirectRequest(), Manager(seed));

        Assert.True(result.IsSuccess, result.Message);
        var demand = await context.RestockRequests
            .SingleAsync(x => x.SourceType == RestockRequestSourceTypes.DirectPurchaseProposal);
        var allocation = await context.RestockSourcingAllocations
            .SingleAsync(x => x.RestockRequestId == demand.RestockRequestId);
        var line = await context.PurchaseAdviceLines
            .SingleAsync(x => x.RestockRequestId == demand.RestockRequestId);
        Assert.Equal(8.75m, demand.RequestedProcurementQuantity);
        Assert.Equal(procurementUnit.UnitId, demand.ProcurementUnitId);
        Assert.Equal(RestockSourcingDecisionTypes.Purchase, demand.SourcingDecision);
        Assert.Equal(RestockSourcingAllocationStatuses.Active, allocation.Status);
        Assert.Equal(line.PurchaseAdviceLineId, allocation.PurchaseAdviceLineId);
        Assert.Equal(8.75m, line.RequestedProcurementQuantity);

        var replay = await service.CreateDirectAsync(DirectRequest(), Manager(seed));

        Assert.True(replay.IsSuccess, replay.Message);
        Assert.Equal(result.Data!.PurchaseAdviceId, replay.Data!.PurchaseAdviceId);
        Assert.Single(await context.RestockRequests
            .Where(x => x.SourceType == RestockRequestSourceTypes.DirectPurchaseProposal)
            .ToListAsync());
        Assert.Single(await context.RestockSourcingAllocations
            .Where(x => x.SourceDocumentType == RestockRequestSourceTypes.DirectPurchaseProposal)
            .ToListAsync());
    }

    private static async Task<PurchaseAdviceDetailDto> SubmitAsync(PurchaseAdviceService service, Seed seed)
    {
        var created = (await service.CreateAsync(CreateRequest(seed, 5m), Manager(seed))).Data!;
        return (await service.SubmitAsync(created.PurchaseAdviceId,
            new PurchaseAdviceTransitionRequest { RowVersion = created.RowVersion }, Manager(seed))).Data!;
    }

    private static PurchaseAdviceService CreateService(
        AppDbContext context,
        IUnitConversionService? unitConversion = null)
    {
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(false);
        return new PurchaseAdviceService(context, scope.Object, unitConversion);
    }

    private static PurchaseAdviceService CreateScopedService(
        AppDbContext context,
        Seed seed,
        IUnitConversionService? unitConversion = null)
    {
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(seed.WarehouseId, seed.StoreId))
            .ReturnsAsync(true);
        return new PurchaseAdviceService(context, scope.Object, unitConversion);
    }

    private static CreatePurchaseAdviceRequest CreateRequest(Seed seed, decimal quantity) => new()
    {
        StoreId = seed.StoreId,
        RequestKey = Guid.NewGuid().ToString("N"),
        NeededByDate = DateTime.Today.AddDays(2),
        Priority = PurchaseAdvicePriorities.Normal,
        Lines = new List<CreatePurchaseAdviceLineRequest>
        {
            new() { RestockRequestId = seed.RestockRequestId, RequestedPurchaseBaseQuantity = quantity, RestockRowVersion = seed.RestockRowVersion }
        }
    };

    private static AdminActorContext Manager(Seed seed) => new() { StaffId = seed.ManagerId, StoreId = seed.StoreId, RoleNames = new[] { RoleConstants.StoreManager } };
    private static AdminActorContext Warehouse(Seed seed) => new() { StaffId = seed.WarehouseId, RoleNames = new[] { RoleConstants.AccountantWarehouse } };

    private static async Task AddTransferAsync(AppDbContext context, Seed seed, decimal quantity)
    {
        var other = new Store { Name = "Source", Address = "Test", Phone = Guid.NewGuid().ToString("N")[..10], Active = true, CreatedAt = DateTime.UtcNow };
        context.Stores.Add(other); await context.SaveChangesAsync();
        context.InventoryTransfers.Add(new InventoryTransfer
        {
            Code = "TR-184-" + Guid.NewGuid().ToString("N")[..6], FromStoreId = other.StoreId, ToStoreId = seed.StoreId,
            Type = InventoryTransferType.STORE_TO_STORE, Purpose = InventoryTransferPurpose.REPLENISHMENT,
            Status = InventoryTransferStatus.DRAFT, DocumentDate = DateTime.UtcNow, CreatedByStaffId = seed.ManagerId, CreatedAt = DateTime.UtcNow,
            Details = new List<InventoryTransferDetail> { new() { RestockRequestId = seed.RestockRequestId, IngredientId = seed.IngredientId, UnitId = seed.UnitId, Quantity = quantity, BaseQuantity = quantity } }
        });
        await context.SaveChangesAsync();
    }

    private static async Task AddPurchaseOrderAsync(AppDbContext context, Seed seed, decimal quantity)
    {
        context.PurchaseOrders.Add(new PurchaseOrder
        {
            Code = "PO-184-" + Guid.NewGuid().ToString("N")[..6], StoreId = seed.StoreId, SupplierId = 999184,
            Status = PurchaseOrderStatuses.Draft, OrderDate = DateTime.UtcNow, CreatedByStaffId = seed.WarehouseId, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow,
            Lines = new List<PurchaseOrderLine> { new() { RestockRequestId = seed.RestockRequestId, IngredientId = seed.IngredientId, IngredientSupplierId = 999184, PackageUnitIdSnapshot = seed.UnitId, PackageQuantitySnapshot = 1m, PackagePriceSnapshot = 1m, PackageCount = quantity, OrderedBaseQuantity = quantity } }
        });
        await context.SaveChangesAsync();
    }

    private static async Task<Seed> SeedAsync(AppDbContext context, decimal requested)
    {
        var now = DateTime.UtcNow;
        var store = new Store { Name = "Store #184", Address = "Test", Phone = Guid.NewGuid().ToString("N")[..10], Active = true, CreatedAt = now };
        var account1 = new Account { Email = Guid.NewGuid() + "@test.local", PasswordHash = "x", Active = true, CreatedAt = now };
        var account2 = new Account { Email = Guid.NewGuid() + "@test.local", PasswordHash = "x", Active = true, CreatedAt = now };
        var account3 = new Account { Email = Guid.NewGuid() + "@test.local", PasswordHash = "x", Active = true, CreatedAt = now };
        var unit = new Unit { UnitCode = "u" + Guid.NewGuid().ToString("N")[..7], Name = "kg", Active = true };
        context.AddRange(store, account1, account2, account3, unit); await context.SaveChangesAsync();
        var manager = new Staff { AccountId = account1.AccountId, StoreId = store.StoreId, FullName = "Manager #184", Active = true, CreatedAt = now};
        var warehouse = new Staff { AccountId = account2.AccountId, FullName = "Warehouse #184", Active = true, CreatedAt = now};
        var area = new Staff { AccountId = account3.AccountId, FullName = "Area #184", Active = true, CreatedAt = now};
        var ingredient = new Ingredient { Code = "ING-" + Guid.NewGuid().ToString("N")[..8], Name = "Coffee bean #184", BaseUnitId = unit.UnitId, Active = true };
        context.AddRange(manager, warehouse, area, ingredient); await context.SaveChangesAsync();
        var request = new RestockRequest { StoreId = store.StoreId, IngredientId = ingredient.IngredientId, RequestedQuantity = requested, Status = RestockRequestStatuses.Processing, Priority = RestockRequestPriorities.Normal, CreatedByStaffId = manager.StaffId, CreatedAt = now, UpdatedAt = now };
        context.RestockRequests.Add(request); await context.SaveChangesAsync();
        return new Seed(store.StoreId, manager.StaffId, warehouse.StaffId, area.StaffId, unit.UnitId, ingredient.IngredientId, request.RestockRequestId, Convert.ToBase64String(request.RowVersion));
    }

    private sealed record Seed(int StoreId, int ManagerId, int WarehouseId, int AreaManagerId, int UnitId, int IngredientId, int RestockRequestId, string RestockRowVersion);
}
