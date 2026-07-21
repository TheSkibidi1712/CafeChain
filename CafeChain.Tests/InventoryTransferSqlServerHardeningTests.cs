using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.DTOs.Systems;
using CafeChain.Application.DTOs.Admin.InventoryTransfers;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Interfaces.Systems;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.InventoryTransfers;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Security;
using CafeChain.Data;
using CafeChain.Infrastrusture.Repositories.Admin.InventoryTransfers;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Inventories.Transfers;
using CafeChain.Models.Permissions;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using CafeChain.Models.Systems;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class InventoryTransferSqlServerHardeningTests : IAsyncLifetime
{
    private const string Database = "CafeChain_InventoryHardeningTests";
    private static string ConnectionString => SqlServerTestConnection.Create(Database);
    private static string MasterConnectionString => SqlServerTestConnection.MasterConnectionString();

    private int _sourceStoreId;
    private int _destinationStoreId;
    private int _unitId;
    private int _staffId;
    private int _accountId;

    public async Task InitializeAsync()
    {
        try
        {
            await using (var master = new SqlConnection(MasterConnectionString))
            {
                await master.OpenAsync();
                await using var command = master.CreateCommand();
                command.CommandText = $"IF DB_ID(N'{Database}') IS NULL CREATE DATABASE [{Database}];";
                await command.ExecuteNonQueryAsync();
            }

            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
            await SeedFoundationAsync(context);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"SQL Server integration environment unavailable for inventory hardening. " +
                $"Set {SqlServerTestConnection.EnvVarName}. Database={Database}. {ex.Message}",
                ex);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SqlServer_PreparedItemTransfer_PreservesIdentityAndCost()
    {
        int transferId;
        int detailId;
        int preparedItemId;
        int requestId;
        await using (var seed = CreateContext())
        {
            var preparedItem = new PreparedItem
            {
                Code = "PI-SQL-TRANSFER-" + Guid.NewGuid().ToString("N")[..6],
                Name = "Prepared SQL transfer",
                BaseUnitId = _unitId,
                Active = true
            };
            seed.PreparedItems.Add(preparedItem);
            await seed.SaveChangesAsync();
            preparedItemId = preparedItem.PreparedItemId;

            seed.StoreInventories.AddRange(
                CanonicalInventory(_sourceStoreId, preparedItemId, 100m),
                CanonicalInventory(_destinationStoreId, preparedItemId, 0m));
            seed.InventoryCostLayers.Add(new InventoryCostLayer
            {
                StoreId = _sourceStoreId,
                PreparedItemId = preparedItemId,
                Quantity = 100m,
                RemainingQuantity = 100m,
                UnitCost = 5m,
                CreatedAt = DateTime.UtcNow
            });

            var alert = new StockAlert
            {
                StoreId = _destinationStoreId,
                PreparedItemId = preparedItemId,
                AlertType = StockAlertTypes.LowStock,
                Severity = StockAlertSeverities.Warning,
                Status = StockAlertStatuses.Confirmed,
                Source = StockAlertSources.ManualCheck,
                CurrentQtySnapshot = 0m,
                ThresholdSnapshot = 20m,
                ConfirmedByStaffId = _staffId,
                ConfirmedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            seed.StockAlerts.Add(alert);
            await seed.SaveChangesAsync();
            var request = new RestockRequest
            {
                StockAlertId = alert.StockAlertId,
                StoreId = _destinationStoreId,
                PreparedItemId = preparedItemId,
                RequestedQuantity = 20m,
                Status = RestockRequestStatuses.Processing,
                Priority = RestockRequestPriorities.Normal,
                CreatedByStaffId = _staffId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            seed.RestockRequests.Add(request);
            await seed.SaveChangesAsync();
            requestId = request.RestockRequestId;

            var transfer = new InventoryTransfer
            {
                Code = "CK-SQL-HARDENING",
                RequestKey = "sql-transfer-draft",
                FromStoreId = _sourceStoreId,
                ToStoreId = _destinationStoreId,
                Type = InventoryTransferType.STORE_TO_STORE,
                Purpose = InventoryTransferPurpose.REPLENISHMENT,
                Status = InventoryTransferStatus.DRAFT,
                DocumentDate = DateTime.Today,
                CreatedByStaffId = _staffId,
                CreatedAt = DateTime.UtcNow,
                Details =
                [
                    new InventoryTransferDetail
                    {
                        PreparedItemId = preparedItemId,
                        RestockRequestId = requestId,
                        UnitId = _unitId,
                        Quantity = 20m,
                        BaseQuantity = 20m,
                        UnitPrice = 999m
                    }
                ]
            };
            seed.InventoryTransfers.Add(transfer);
            await seed.SaveChangesAsync();
            transferId = transfer.InventoryTransferId;
            detailId = transfer.Details.Single().InventoryTransferDetailId;
        }

        await using (var first = CreateContext())
        {
            var result = await CreateTransferService(first).ConfirmAsync(transferId, "sql-transfer-confirm-1");
            Assert.Equal(InventoryTransferStatus.DISPATCHED, result.Status);
        }
        await using (var replay = CreateContext())
        {
            var result = await CreateTransferService(replay).ConfirmAsync(transferId, "sql-transfer-confirm-2");
            Assert.Equal(InventoryTransferStatus.DISPATCHED, result.Status);
        }
        await using (var receive = CreateContext())
        {
            var rowVersion = Convert.ToBase64String(await receive.InventoryTransfers.AsNoTracking()
                .Where(x => x.InventoryTransferId == transferId)
                .Select(x => x.RowVersion)
                .SingleAsync());
            var result = await CreateTransferService(receive).ReceiveAsync(
                transferId,
                new InventoryTransferReceiveDTO
                {
                    RowVersion = rowVersion,
                    RequestKey = "sql-transfer-receive-1",
                    ReceivedAt = DateTime.UtcNow,
                    Lines =
                    [
                        new InventoryTransferReceiveLineDTO
                        {
                            InventoryTransferDetailId = detailId,
                            ReceivedBaseQuantity = 20m
                        }
                    ]
                });
            Assert.Equal(InventoryTransferStatus.COMPLETED, result.Status);
        }

        await using var verify = CreateContext();
        var source = await verify.StoreInventories.SingleAsync(i =>
            i.StoreId == _sourceStoreId && i.PreparedItemId == preparedItemId);
        var destination = await verify.StoreInventories.SingleAsync(i =>
            i.StoreId == _destinationStoreId && i.PreparedItemId == preparedItemId);
        Assert.Equal(80m, source.AvailableQty);
        Assert.Equal(20m, destination.AvailableQty);
        Assert.Null(source.IngredientId);
        Assert.Null(destination.IngredientId);
        Assert.Equal(2, await verify.InventoryTransactions.CountAsync(t =>
            t.InventoryTransferDetailId == detailId));
        Assert.Equal(1, await verify.RestockFulfillmentPostings.CountAsync(p =>
            p.RestockRequestId == requestId
            && p.SourceDocumentType == RestockFulfillmentDocumentTypes.InventoryTransfer));
        var destinationLayer = await verify.InventoryCostLayers.SingleAsync(l =>
            l.StoreId == _destinationStoreId && l.PreparedItemId == preparedItemId);
        Assert.Equal(20m, destinationLayer.Quantity);
        Assert.Equal(5m, destinationLayer.UnitCost);
        Assert.Equal(RestockRequestStatuses.Completed,
            (await verify.RestockRequests.SingleAsync(r => r.RestockRequestId == requestId)).Status);
    }

    [Fact]
    public async Task SqlServer_AreaManager_CannotAccessOutsideScope()
    {
        int alertId;
        await using (var seed = CreateContext())
        {
            var areaRoleId = await EnsureRoleAsync(seed, RoleConstants.AreaManager);
            if (!await seed.AccountRoles.AnyAsync(x => x.AccountId == _accountId && x.RoleId == areaRoleId))
                seed.AccountRoles.Add(new AccountRole { AccountId = _accountId, RoleId = areaRoleId });

            var scopeType = await seed.ScopeTypes.FirstOrDefaultAsync(x => x.ScopeTypeId == (int)ScopeLevel.Store);
            if (scopeType == null)
            {
                scopeType = new ScopeType
                {
                    ScopeTypeId = (int)ScopeLevel.Store,
                    Code = "STORE",
                    Name = "Cửa hàng"
                };
                seed.ScopeTypes.Add(scopeType);
                await seed.SaveChangesAsync();
            }
            if (!await seed.StaffScopes.AnyAsync(x =>
                    x.StaffId == _staffId
                    && x.ScopeTypeId == (int)ScopeLevel.Store
                    && x.ScopeRefId == _sourceStoreId))
            {
                seed.StaffScopes.Add(new StaffScope
                {
                    StaffId = _staffId,
                    ScopeTypeId = (int)ScopeLevel.Store,
                    ScopeRefId = _sourceStoreId
                });
            }

            var ingredient = new Ingredient
            {
                Code = "AREA-SQL-" + Guid.NewGuid().ToString("N")[..6],
                Name = "Area scope ingredient",
                BaseUnitId = _unitId,
                Active = true
            };
            seed.Ingredients.Add(ingredient);
            await seed.SaveChangesAsync();
            var alert = new StockAlert
            {
                StoreId = _destinationStoreId,
                IngredientId = ingredient.IngredientId,
                AlertType = StockAlertTypes.LowStock,
                Severity = StockAlertSeverities.Warning,
                Status = StockAlertStatuses.Open,
                Source = StockAlertSources.ManualCheck,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            seed.StockAlerts.Add(alert);
            await seed.SaveChangesAsync();
            alertId = alert.StockAlertId;
        }

        await using var context = CreateContext();
        var service = new StockAlertManagerService(
            context,
            new ScopeAuthorizationService(context),
            NullLogger<StockAlertManagerService>.Instance);
        var result = await service.ConfirmAsync(
            alertId, _staffId, _destinationStoreId, "outside scope", null);

        Assert.False(result.IsSuccess);
        Assert.Equal(StockAlertStatuses.Open,
            (await context.StockAlerts.SingleAsync(a => a.StockAlertId == alertId)).Status);
    }

    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options);

    private AdminInventoryTransferService CreateTransferService(AppDbContext context)
    {
        var repository = new AdminInventoryTransferRepository(context);
        var dedup = new Mock<IRequestDeduplicationService>();
        dedup.Setup(x => x.BeginAsync(
                It.IsAny<string>(), It.IsAny<string>(), _staffId, It.IsAny<object>(), It.IsAny<int?>()))
            .ReturnsAsync(() => new RequestDeduplicationBeginResult
            {
                CanProcess = true,
                Entry = new RequestDeduplication()
            });
        var issuePolicy = new Mock<IInventoryIssuePolicy>();
        issuePolicy.Setup(x => x.EvaluateAsync(It.IsAny<InventoryIssueRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryIssueRequest request, CancellationToken _) => new InventoryIssueDecision(
                InventoryIssueOutcome.Allowed,
                InventoryIssueReasonCodes.NonNegativeIssueAllowed,
                request.BeforeAvailableQty,
                request.IssueQty,
                request.BeforeAvailableQty - request.IssueQty,
                0,
                0,
                false,
                false,
                string.Empty));
        var alerts = new Mock<IStockAlertService>();
        alerts.Setup(x => x.EvaluateStoreInventoryItemAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(ServiceResult<StockAlertEvaluationResultDto>.Success(new()));
        var actor = new Mock<IAdminActorContextAccessor>();
        actor.Setup(x => x.Get(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
            .Returns(new AdminActorContext { StaffId = _staffId, RoleNames = [RoleConstants.BusinessOwner] });
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(_staffId, It.IsAny<int>())).ReturnsAsync(true);
        var allocations = new Mock<IRestockAllocationService>();
        allocations.Setup(x => x.ValidateAllocationAsync(It.IsAny<RestockAllocationValidationRequest>()))
            .ReturnsAsync(ServiceResult<RestockAllocationSummaryDto>.Success(new RestockAllocationSummaryDto()));
        return new AdminInventoryTransferService(
            repository,
            dedup.Object,
            issuePolicy.Object,
            new InventoryCostLayerConsumptionService(context),
            new RestockFulfillmentPostingService(context),
            alerts.Object,
            new FixedUserContext(_staffId),
            actor.Object,
            scope.Object,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            allocations.Object);
    }

    private async Task SeedFoundationAsync(AppDbContext context)
    {
        _unitId = await context.Units.Select(x => x.UnitId).FirstOrDefaultAsync();
        if (_unitId <= 0)
        {
            var unit = new Unit { UnitCode = "ml", Name = "Millilitre", Active = true };
            context.Units.Add(unit);
            await context.SaveChangesAsync();
            _unitId = unit.UnitId;
        }

        var stores = await context.Stores.OrderBy(x => x.StoreId).Take(2).ToListAsync();
        while (stores.Count < 2)
        {
            var store = new Store
            {
                Name = "SQL hardening store " + stores.Count,
                Address = "test",
                Phone = "0",
                Active = true,
                CreatedAt = DateTime.UtcNow
            };
            context.Stores.Add(store);
            await context.SaveChangesAsync();
            stores.Add(store);
        }
        _sourceStoreId = stores[0].StoreId;
        _destinationStoreId = stores[1].StoreId;

        var managerRoleId = await EnsureRoleAsync(context, RoleConstants.StoreManager);
        var account = new Account
        {
            Email = $"inventory-sql-{Guid.NewGuid():N}@test.local",
            PasswordHash = "x",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        _accountId = account.AccountId;
        context.AccountRoles.Add(new AccountRole { AccountId = account.AccountId, RoleId = managerRoleId });
        var staff = new Staff
        {
            AccountId = account.AccountId,
            StoreId = _sourceStoreId,
            FullName = "Inventory SQL actor",
            Active = true,
            CreatedAt = DateTime.UtcNow,
            BaseSalary = 0m
        };
        context.Staffs.Add(staff);
        await context.SaveChangesAsync();
        _staffId = staff.StaffId;
    }

    private static async Task<int> EnsureRoleAsync(AppDbContext context, string name)
    {
        var role = await context.Roles.FirstOrDefaultAsync(x => x.Name == name);
        if (role != null)
            return role.RoleId;
        role = new Role
        {
            Name = name,
            Active = true,
            IsStoreLevel = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Roles.Add(role);
        await context.SaveChangesAsync();
        return role.RoleId;
    }

    private StoreInventory CanonicalInventory(int storeId, int preparedItemId, decimal quantity) => new()
    {
        StoreId = storeId,
        PreparedItemId = preparedItemId,
        BtpIdentityState = BtpIdentityState.Canonical,
        QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
        QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
        QuantitySemanticsEvidenceReference = "SQL_TRANSFER_TEST",
        QuantitySemanticsReviewedAt = DateTime.UtcNow,
        QuantitySemanticsReviewedByAccountId = _accountId,
        AvailableQty = quantity,
        ReservedQty = 0m,
        LastUpdated = DateTime.UtcNow
    };

    private sealed class FixedUserContext(int staffId) : IUserContext
    {
        public int StaffId { get; } = staffId;
        public string StaffName => "Inventory SQL actor";
    }
}
