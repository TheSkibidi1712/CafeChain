using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.DTOs.Admin.Replenishment;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Production;
using CafeChain.Application.Interfaces.Admin.StoreInventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Options;
using CafeChain.Application.Services.Admin.Production;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class PreparedItemProductionPlanningSqlServerTests : IAsyncLifetime
{
    private const string Database = "CafeChain_ReplenishmentR4Tests";
    private int _accountId;
    private int _staffId;
    private int _storeId;
    private int _preparedItemId;
    private int _unitId;
    private int _recipeId;
    private int _demandId;

    private static string ConnectionString => SqlServerTestConnection.Create(Database);

    public async Task InitializeAsync()
    {
        await using (var master = new SqlConnection(SqlServerTestConnection.MasterConnectionString()))
        {
            await master.OpenAsync();
            await using var command = master.CreateCommand();
            command.CommandText = $"IF DB_ID(N'{Database}') IS NULL CREATE DATABASE [{Database}];";
            await command.ExecuteNonQueryAsync();
        }

        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        await SeedAsync(context);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SqlServer_ConcurrentPlans_CannotOverAllocateUncoveredNeed()
    {
        var bothReadsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readCount = 0;
        var readModel = new Mock<IPreparedItemReplenishmentReadService>();
        readModel
            .Setup(x => x.GetAsync(_accountId, _storeId, _preparedItemId, It.IsAny<int>()))
            .Returns(async () =>
            {
                if (Interlocked.Increment(ref readCount) == 2)
                    bothReadsStarted.TrySetResult();
                await bothReadsStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
                return ServiceResult<PreparedItemReplenishmentDto>.Success(new PreparedItemReplenishmentDto
                {
                    StoreId = _storeId,
                    PreparedItemId = _preparedItemId,
                    BaseUnitId = _unitId,
                    IsLow = true,
                    GrossNeedBase = 6m,
                    OpenProductionCoverageBase = 0m,
                    NetNeedBase = 6m,
                    DataStatus = PreparedItemReplenishmentDataStatuses.Ready
                });
            });

        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var results = await Task.WhenAll(
            CreateService(firstContext, readModel.Object).SetSourcingDecisionAsync(
                Command(Guid.NewGuid()), _staffId),
            CreateService(secondContext, readModel.Object).SetSourcingDecisionAsync(
                Command(Guid.NewGuid()), _staffId));

        Assert.Single(results.Where(x => x.IsSuccess));
        var conflict = Assert.Single(results.Where(x => !x.IsSuccess));
        Assert.Equal(RestockRequestErrorCodes.ResourceChanged, conflict.ErrorCode);

        await using var verify = CreateContext();
        var activeAllocations = await verify.RestockSourcingAllocations
            .AsNoTracking()
            .Where(x => x.RestockRequestId == _demandId
                && x.DecisionType == RestockSourcingDecisionTypes.Production
                && x.Status == RestockSourcingAllocationStatuses.Active)
            .ToListAsync();
        Assert.Single(activeAllocations);
        Assert.Equal(6m, activeAllocations[0].ProcurementQuantity);
        Assert.Single(await verify.ProductionRuns.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task SqlServer_CancelReplan_Race_IsSafe()
    {
        var readModel = CurrentNeedReadModel();
        int originalRunId;
        await using (var seed = CreateContext())
        {
            var planned = await CreateService(seed, readModel).SetSourcingDecisionAsync(
                Command(Guid.NewGuid()),
                _staffId);
            Assert.True(planned.IsSuccess, planned.Message);
            originalRunId = planned.Data.ProductionRunId!.Value;
        }

        await using var cancelContext = CreateContext();
        await using var replanContext = CreateContext();
        var cancelTask = CreateOperationsService(cancelContext).CancelAsync(
            originalRunId,
            _staffId,
            "Điều chỉnh kế hoạch bổ sung");
        var replanTask = CreateService(replanContext, readModel).SetSourcingDecisionAsync(
            Command(Guid.NewGuid()),
            _staffId);
        var cancelResult = await cancelTask;
        await replanTask;

        if (!cancelResult.IsSuccess)
        {
            await using var retryCancel = CreateContext();
            var retry = await CreateOperationsService(retryCancel).CancelAsync(
                originalRunId,
                _staffId,
                "Thử lại sau xung đột");
            Assert.True(retry.IsSuccess, retry.Message);
        }

        await using (var ensureReplan = CreateContext())
        {
            var active = await ensureReplan.RestockSourcingAllocations
                .CountAsync(x => x.RestockRequestId == _demandId
                    && x.DecisionType == RestockSourcingDecisionTypes.Production
                    && x.Status == RestockSourcingAllocationStatuses.Active);
            if (active == 0)
            {
                var replanned = await CreateService(ensureReplan, readModel).SetSourcingDecisionAsync(
                    Command(Guid.NewGuid()),
                    _staffId);
                Assert.True(replanned.IsSuccess, replanned.Message);
            }
        }

        await using var verify = CreateContext();
        Assert.Equal(ProductionRunStatus.Cancelled,
            await verify.ProductionRuns
                .Where(x => x.ProductionRunId == originalRunId)
                .Select(x => x.Status)
                .SingleAsync());
        var activeAllocations = await verify.RestockSourcingAllocations
            .Where(x => x.RestockRequestId == _demandId
                && x.DecisionType == RestockSourcingDecisionTypes.Production
                && x.Status == RestockSourcingAllocationStatuses.Active)
            .ToListAsync();
        Assert.Single(activeAllocations);
        Assert.Equal(6m, activeAllocations.Sum(x => x.ProcurementQuantity));
        Assert.Equal(RestockSourcingAllocationStatuses.Released,
            await verify.RestockSourcingAllocations
                .Where(x => x.ProductionRunId == originalRunId)
                .Select(x => x.Status)
                .SingleAsync());
    }

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options);

    private RestockRequestService CreateService(
        AppDbContext context,
        IPreparedItemReplenishmentReadService readModel)
    {
        var eligibility = new Mock<IProductionSourceEligibilityService>();
        eligibility
            .Setup(x => x.EvaluateAsync(It.IsAny<ProductionSourceEligibilityRequest>()))
            .ReturnsAsync(ServiceResult<ProductionSourceEligibilityDto>.Success(new ProductionSourceEligibilityDto
            {
                Eligible = true,
                ReasonCode = ProductionEligibilityReasonCodes.Eligible,
                Message = "Có thể lập kế hoạch sản xuất.",
                StoreId = _storeId,
                PreparedItemId = _preparedItemId,
                RecipeId = _recipeId,
                ExpectedOutputPerBatchBase = 6m,
                OutputBaseUnitId = _unitId,
                OutputBaseUnitCode = "r4-sql-litre"
            }));

        return new RestockRequestService(
            context,
            new Mock<IScopeAuthorizationService>().Object,
            NullLogger<RestockRequestService>.Instance,
            productionEligibility: eligibility.Object,
            preparedItemReplenishment: readModel);
    }

    private IPreparedItemReplenishmentReadService CurrentNeedReadModel()
    {
        var read = new Mock<IPreparedItemReplenishmentReadService>();
        read.Setup(x => x.GetAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(ServiceResult<PreparedItemReplenishmentDto>.Success(new PreparedItemReplenishmentDto
            {
                StoreId = _storeId,
                PreparedItemId = _preparedItemId,
                BaseUnitId = _unitId,
                IsLow = true,
                GrossNeedBase = 6m,
                OpenProductionCoverageBase = 0m,
                NetNeedBase = 6m,
                DataStatus = PreparedItemReplenishmentDataStatuses.Ready
            }));
        return read.Object;
    }

    private static ProductionRunOperationsService CreateOperationsService(AppDbContext context)
    {
        var permissions = new Mock<IAdminPermissionService>();
        permissions.Setup(x => x.HasPermissionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync((int accountId, string code, int? storeId) =>
                ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
                {
                    AccountId = accountId,
                    PermissionCode = code,
                    TargetStoreId = storeId,
                    Allowed = true,
                    RoleAllowed = true,
                    ScopeAllowed = true
                }));
        var physical = new PhysicalUnitConversionService(
            context,
            NullLogger<PhysicalUnitConversionService>.Instance);
        var readiness = new Mock<IProductionReadinessService>();
        readiness.Setup(x => x.PreviewAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()))
            .ReturnsAsync(ServiceResult<ProductionReadinessPreviewDto>.Success(new ProductionReadinessPreviewDto
            {
                IsReady = true,
                OverallStatus = "Sẵn sàng"
            }));
        return new ProductionRunOperationsService(
            context,
            permissions.Object,
            new UnitConversionService(context, NullLogger<UnitConversionService>.Instance, physical),
            physical,
            readiness.Object,
            Options.Create(new ProductionOperationsOptions()));
    }

    private SourcingDecisionRequest Command(Guid requestKey) => new()
    {
        RestockRequestId = _demandId,
        DecisionType = RestockSourcingDecisionTypes.Production,
        ProcurementQuantity = 6m,
        ProcurementUnitId = _unitId,
        RequestKey = requestKey,
        Reason = "Kiểm thử lập sản xuất đồng thời"
    };

    private async Task SeedAsync(AppDbContext context)
    {
        var unit = new Unit
        {
            UnitCode = "r4-sql-litre",
            Name = "Lít",
            Type = UnitType.TheTich,
            Active = true
        };
        var store = new Store
        {
            Name = "Chi nhánh R4 SQL",
            Address = "Kiểm thử",
            Phone = "000",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        var account = new Account
        {
            Email = "r4-sql-manager@test.local",
            PasswordHash = "x",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        context.AddRange(unit, store, account);
        await context.SaveChangesAsync();
        _unitId = unit.UnitId;
        _storeId = store.StoreId;
        _accountId = account.AccountId;

        var preparedItem = new PreparedItem
        {
            Code = "BTP-R4-SQL",
            Name = "Cốt trà R4 SQL",
            BaseUnitId = _unitId,
            Active = true
        };
        var staff = new Staff
        {
            AccountId = _accountId,
            StoreId = _storeId,
            FullName = "Quản lý R4 SQL",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        context.AddRange(preparedItem, staff);
        await context.SaveChangesAsync();
        _preparedItemId = preparedItem.PreparedItemId;
        _staffId = staff.StaffId;

        var recipe = new Recipe
        {
            RecipeCode = "RCP-R4-SQL",
            Name = "Công thức R4 SQL",
            PreparedItemId = _preparedItemId,
            OutputQuantity = 6m,
            OutputUnitId = _unitId,
            Active = true,
            Status = "Active"
        };
        context.Recipes.Add(recipe);
        await context.SaveChangesAsync();
        _recipeId = recipe.RecipeId;

        context.StoreInventories.Add(new StoreInventory
        {
            StoreId = _storeId,
            PreparedItemId = _preparedItemId,
            BtpIdentityState = BtpIdentityState.Canonical,
            QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
            QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
            QuantitySemanticsEvidenceReference = "replenishment-r4-sql",
            QuantitySemanticsReviewedAt = DateTime.UtcNow,
            QuantitySemanticsReviewedByAccountId = _accountId,
            AvailableQty = 2m,
            ReservedQty = 0m,
            MinStockLevel = 3m,
            TargetStockLevel = 8m,
            LastUpdated = DateTime.UtcNow,
            RowVersion = []
        });
        var demand = new RestockRequest
        {
            StoreId = _storeId,
            PreparedItemId = _preparedItemId,
            RequestedQuantity = 6m,
            RequestedProcurementQuantity = 6m,
            ProcurementUnitId = _unitId,
            TargetStockProcurementQuantity = 8m,
            Status = RestockRequestStatuses.Processing,
            Priority = RestockRequestPriorities.High,
            SourceType = RestockRequestSourceTypes.StockAlert,
            SourcingStatus = RestockSourcingStatuses.Unallocated,
            CreatedByStaffId = _staffId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = []
        };
        context.RestockRequests.Add(demand);
        await context.SaveChangesAsync();
        _demandId = demand.RestockRequestId;
    }
}
