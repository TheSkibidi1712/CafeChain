using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Production;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.Production;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Configuration;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class ProductionBatchYieldTests : IntegrationTestBase
{
    private const int StoreId = 38610;
    private const int OperatorStaffId = 38611;
    private const int ApproverStaffId = 38612;
    private const int AcceptorStaffId = 38613;
    private const int IngredientId = 38614;
    private const int PreparedItemId = 38615;
    private const int RecipeId = 38616;
    private const int UnitGram = 1;

    [Fact]
    public async Task UnderYield_UsesActualInputCreditsAcceptedOutputAndLeavesRemainingDemand()
    {
        using var context = CreateDbContext();
        var fixture = await SeedAcceptanceAsync(context, acceptedOutput: 9_600m, actualInput: 900m);

        var logger = new CapturingLogger<ProductionRunAcceptanceService>();
        var result = await CreateAcceptanceService(context, logger).AcceptAsync(
            fixture.RunId,
            AcceptorStaffId);

        Assert.True(result.IsSuccess, $"{result.Message}\n{logger.LastException}");
        Assert.Equal(9_600m, result.Data!.NormalizedOutputQuantity);
        Assert.Equal(4_100m, await context.StoreInventories
            .Where(x => x.IngredientId == IngredientId)
            .Select(x => x.AvailableQty)
            .SingleAsync());
        Assert.Equal(9_600m, await context.StoreInventories
            .Where(x => x.PreparedItemId == PreparedItemId)
            .Select(x => x.AvailableQty)
            .SingleAsync());
        var posting = Assert.Single(context.RestockFulfillmentPostings);
        Assert.Equal(9_600m, posting.Quantity);
        var restock = await context.RestockRequests.SingleAsync();
        Assert.Equal(RestockRequestStatuses.PartiallyReceived, restock.Status);
        Assert.Equal(400m, restock.RequestedQuantity - posting.Quantity);
        Assert.Equal(RestockSourcingAllocationStatuses.Released,
            (await context.RestockSourcingAllocations.SingleAsync()).Status);
        Assert.Equal(RestockSourcingStatuses.Unallocated, restock.SourcingStatus);
        Assert.Equal(900m, await context.InventoryCostLayers
            .Where(x => x.IngredientId == IngredientId)
            .Select(x => 5_000m - x.RemainingQuantity)
            .SingleAsync());
        Assert.Equal(9_000m / 9_600m, result.Data.OutputUnitCost);
    }

    [Fact]
    public async Task Overage_CreditsAllAcceptedOutputButCapsRestockFulfillment()
    {
        using var context = CreateDbContext();
        var fixture = await SeedAcceptanceAsync(context, acceptedOutput: 10_500m, actualInput: 1_000m);

        var logger = new CapturingLogger<ProductionRunAcceptanceService>();
        var result = await CreateAcceptanceService(context, logger).AcceptAsync(
            fixture.RunId,
            AcceptorStaffId);

        Assert.True(result.IsSuccess, $"{result.Message}\n{logger.LastException}");
        Assert.Equal(10_500m, await context.StoreInventories
            .Where(x => x.PreparedItemId == PreparedItemId)
            .Select(x => x.AvailableQty)
            .SingleAsync());
        Assert.Equal(10_000m, Assert.Single(context.RestockFulfillmentPostings).Quantity);
        Assert.Equal(RestockRequestStatuses.Completed, (await context.RestockRequests.SingleAsync()).Status);
        Assert.Equal(RestockSourcingAllocationStatuses.Released,
            (await context.RestockSourcingAllocations.SingleAsync()).Status);
        Assert.Equal(500m, result.Data!.NormalizedOutputQuantity - 10_000m);
    }

    [Fact]
    public async Task WasteOnlyCompletion_ConsumesActualInputWithoutInventoryCreditOrZeroDivision()
    {
        using var context = CreateDbContext();
        var fixture = await SeedAcceptanceAsync(context, acceptedOutput: 0, actualInput: 900m);

        var result = await CreateAcceptanceService(context).AcceptAsync(fixture.RunId, AcceptorStaffId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(0, result.Data!.NormalizedOutputQuantity);
        Assert.Equal(4_100m, await context.StoreInventories
            .Where(x => x.IngredientId == IngredientId)
            .Select(x => x.AvailableQty)
            .SingleAsync());
        Assert.False(await context.StoreInventories.AnyAsync(x => x.PreparedItemId == PreparedItemId));
        Assert.Empty(context.RestockFulfillmentPostings);
        var run = await context.ProductionRuns.SingleAsync();
        Assert.Equal(ProductionRunStatus.Completed, run.Status);
        Assert.Equal(9_000m, run.TotalInputCost);
        Assert.Null(run.OutputUnitCost);
    }

    [Fact]
    public async Task RetryAcceptance_ReturnsReplayWithoutDuplicateInventoryPosting()
    {
        using var context = CreateDbContext();
        var fixture = await SeedAcceptanceAsync(context, acceptedOutput: 9_600m, actualInput: 900m);
        var service = CreateAcceptanceService(context);

        var first = await service.AcceptAsync(fixture.RunId, AcceptorStaffId);
        var retry = await service.AcceptAsync(fixture.RunId, AcceptorStaffId);

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(retry.IsSuccess, retry.Message);
        Assert.True(retry.Data!.WasReplay);
        Assert.Equal(2, context.InventoryTransactions.Count());
        Assert.Single(context.RestockFulfillmentPostings);
        Assert.Single(context.InventoryCostLayers.Where(x => x.PreparedItemId == PreparedItemId));
    }

    [Fact]
    public async Task CancelledRun_ReleasesProductionCoverage()
    {
        using var context = CreateDbContext();
        var fixture = await SeedBaseAsync(context, ProductionRunStatus.Planned);

        var result = await CreateOperationsService(context).CancelAsync(
            fixture.RunId,
            AcceptorStaffId,
            "Không còn nhu cầu sản xuất");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(ProductionRunStatus.Cancelled, (await context.ProductionRuns.SingleAsync()).Status);
        var allocation = await context.RestockSourcingAllocations.SingleAsync();
        Assert.Equal(RestockSourcingAllocationStatuses.Released, allocation.Status);
        Assert.Equal(AcceptorStaffId, allocation.ReleasedByStaffId);
        Assert.NotNull(allocation.ReleasedAtUtc);
        var demand = await context.RestockRequests.SingleAsync();
        Assert.Equal(RestockSourcingStatuses.Unallocated, demand.SourcingStatus);
        Assert.Null(demand.SourcingDecision);
    }

    [Fact]
    public async Task CancelReplay_DoesNotReleaseTwice()
    {
        using var context = CreateDbContext();
        var fixture = await SeedBaseAsync(context, ProductionRunStatus.Released);
        var service = CreateOperationsService(context);

        var first = await service.CancelAsync(fixture.RunId, AcceptorStaffId, "Dừng kế hoạch");
        var releasedAt = (await context.RestockSourcingAllocations.AsNoTracking().SingleAsync()).ReleasedAtUtc;
        var replay = await service.CancelAsync(fixture.RunId, AcceptorStaffId, "Gửi lại yêu cầu hủy");

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(replay.IsSuccess, replay.Message);
        Assert.True(replay.Data!.WasReplay);
        Assert.Equal(releasedAt, (await context.RestockSourcingAllocations.AsNoTracking().SingleAsync()).ReleasedAtUtc);
        Assert.Single(context.ProductionRunTransitions.Where(x => x.ToStatus == "CANCELLED"));
    }

    [Fact]
    public async Task AcceptedOutput_ReevaluatesPreparedItemAlert()
    {
        using var context = CreateDbContext();
        var fixture = await SeedAcceptanceAsync(context, acceptedOutput: 9_600m, actualInput: 900m);
        var alerts = new Mock<IStockAlertService>();
        alerts.Setup(x => x.EvaluateStoreInventoryItemAsync(
                It.IsAny<int>(),
                StockAlertSources.ProductionAcceptance))
            .ReturnsAsync(ServiceResult<StockAlertEvaluationResultDto>.Success(new StockAlertEvaluationResultDto()));

        var result = await CreateAcceptanceService(context, stockAlerts: alerts.Object)
            .AcceptAsync(fixture.RunId, AcceptorStaffId);

        Assert.True(result.IsSuccess, result.Message);
        alerts.Verify(x => x.EvaluateStoreInventoryItemAsync(
            It.IsAny<int>(),
            StockAlertSources.ProductionAcceptance), Times.Once);
    }

    [Fact]
    public async Task ReevaluationFailure_DoesNotRollbackAcceptedInventory()
    {
        using var context = CreateDbContext();
        var fixture = await SeedAcceptanceAsync(context, acceptedOutput: 9_600m, actualInput: 900m);
        var alerts = new Mock<IStockAlertService>();
        alerts.Setup(x => x.EvaluateStoreInventoryItemAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("auxiliary reevaluation unavailable"));

        var result = await CreateAcceptanceService(context, stockAlerts: alerts.Object)
            .AcceptAsync(fixture.RunId, AcceptorStaffId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(ProductionRunStatus.Completed, (await context.ProductionRuns.SingleAsync()).Status);
        Assert.Equal(9_600m, await context.StoreInventories
            .Where(x => x.PreparedItemId == PreparedItemId)
            .Select(x => x.AvailableQty)
            .SingleAsync());
        Assert.Equal(2, await context.InventoryTransactions.CountAsync());
    }

    [Fact]
    public async Task VarianceAboveTolerance_RequiresDifferentApproverBeforeAcceptance()
    {
        using var context = CreateDbContext();
        var fixture = await SeedOperationAsync(context);
        var operations = CreateOperationsService(context);

        var recorded = await operations.RecordActualAsync(new RecordProductionActualRequest
        {
            ProductionRunId = fixture.RunId,
            ActualProducedBase = 9_000m,
            AcceptedOutputBase = 9_000m,
            RejectedOutputBase = 0,
            Reason = "Sản lượng thấp do hao hụt thực tế",
            Inputs =
            {
                new ProductionActualInputRequest
                {
                    IngredientId = IngredientId,
                    ActualBaseQuantity = 950m
                }
            }
        }, OperatorStaffId);

        Assert.True(recorded.IsSuccess, recorded.Message);
        Assert.Equal("AWAITINGVARIANCEAPPROVAL", recorded.Data!.Status);
        Assert.Equal(10m, recorded.Data.VariancePercent);
        var selfApprove = await operations.ApproveVarianceAsync(
            fixture.RunId,
            OperatorStaffId,
            "Tự duyệt");
        Assert.False(selfApprove.IsSuccess);
        Assert.Equal(ProductionRunOperationErrorCodes.MakerChecker, selfApprove.ErrorCode);

        var approved = await operations.ApproveVarianceAsync(
            fixture.RunId,
            ApproverStaffId,
            "Đã kiểm tra nguyên nhân hao hụt");
        Assert.True(approved.IsSuccess, approved.Message);
        Assert.Equal("AWAITINGACCEPTANCE", approved.Data!.Status);
        var run = await context.ProductionRuns.SingleAsync();
        Assert.Equal(OperatorStaffId, run.ActualRecordedByStaffId);
        Assert.Equal(ApproverStaffId, run.VarianceApprovedByStaffId);
    }

    [Fact]
    public async Task Release_MissingChildPreparedItem_IsBlockedWithoutCreatingChildRun()
    {
        using var context = CreateDbContext();
        var fixture = await SeedBaseAsync(context, ProductionRunStatus.Planned);
        var readiness = new Mock<IProductionReadinessService>();
        readiness.Setup(x => x.PreviewAsync(StoreId, RecipeId, 2m))
            .ReturnsAsync(ServiceResult<ProductionReadinessPreviewDto>.Success(new ProductionReadinessPreviewDto
            {
                IsReady = false,
                OverallStatus = "Chưa sẵn sàng",
                Reasons =
                {
                    new ProductionReadinessReasonDto
                    {
                        Code = ProductionReadinessCodes.PreparedItemShortage,
                        Message = "Bán thành phẩm BTP-CON thiếu tồn khả dụng; hãy chuẩn bị phụ thuộc trước khi phát hành.",
                        Blocking = true
                    }
                }
            }));
        var operations = CreateOperationsService(context, readiness.Object);

        var result = await operations.ReleaseAsync(fixture.RunId, AcceptorStaffId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ProductionRunOperationErrorCodes.NotReady, result.ErrorCode);
        Assert.Contains("Bán thành phẩm", result.Message);
        Assert.Equal(ProductionRunStatus.Planned,
            await context.ProductionRuns.Where(x => x.ProductionRunId == fixture.RunId).Select(x => x.Status).SingleAsync());
        Assert.Equal(1, await context.ProductionRuns.CountAsync());
    }

    private async Task<(int RunId, int RestockId)> SeedAcceptanceAsync(
        AppDbContext context,
        decimal acceptedOutput,
        decimal actualInput)
    {
        var fixture = await SeedBaseAsync(context, ProductionRunStatus.AwaitingAcceptance);
        var run = await context.ProductionRuns.SingleAsync();
        run.ActualRecordedByStaffId = OperatorStaffId;
        run.ActualRecordedAtUtc = DateTime.UtcNow;
        if (Math.Abs(acceptedOutput - 10_000m) / 10_000m * 100m > 5m)
        {
            run.VarianceApprovedByStaffId = ApproverStaffId;
            run.VarianceApprovedAtUtc = DateTime.UtcNow;
        }
        context.ProductionRunInputActuals.Add(new ProductionRunInputActual
        {
            ProductionRunId = run.ProductionRunId,
            IngredientId = IngredientId,
            BaseUnitId = UnitGram,
            PlannedBaseQuantity = 1_000m,
            ActualBaseQuantity = actualInput,
            ConfirmedByStaffId = OperatorStaffId,
            ConfirmedAtUtc = DateTime.UtcNow
        });
        context.ProductionRunOutputs.Add(new ProductionRunOutput
        {
            ProductionRunId = run.ProductionRunId,
            BaseUnitId = UnitGram,
            ExpectedOutputBase = 10_000m,
            ActualProducedBase = acceptedOutput,
            AcceptedOutputBase = acceptedOutput,
            RejectedOutputBase = 0,
            VariancePercent = Math.Abs(acceptedOutput - 10_000m) / 10_000m * 100m,
            Reason = acceptedOutput == 10_000m ? null : "Sản lượng thực tế",
            RecordedByStaffId = OperatorStaffId,
            RecordedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        return fixture;
    }

    private Task<(int RunId, int RestockId)> SeedOperationAsync(AppDbContext context)
        => SeedBaseAsync(context, ProductionRunStatus.InProgress);

    private async Task<(int RunId, int RestockId)> SeedBaseAsync(
        AppDbContext context,
        ProductionRunStatus status)
    {
        var now = DateTime.UtcNow;
        context.Stores.Add(new Store
        {
            StoreId = StoreId,
            Name = "Cửa hàng batch yield",
            Address = "Test",
            Phone = "000",
            Active = true,
            CreatedAt = now
        });
        context.StoreInventoryWriterConfigurations.Add(new StoreInventoryWriterConfiguration
        {
            StoreId = StoreId,
            WriterMode = InventoryWriterMode.PreparedItem,
            HasEverActivatedPreparedItem = true,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = new byte[] { 0 }
        });
        context.Staffs.AddRange(
            Staff(OperatorStaffId, "Người vận hành"),
            Staff(ApproverStaffId, "Người duyệt"),
            Staff(AcceptorStaffId, "Người nhận đầu ra"));
        context.Ingredients.Add(new Ingredient
        {
            IngredientId = IngredientId,
            Code = "ING-BATCH-386",
            Name = "Đầu vào thực tế",
            BaseUnitId = UnitGram,
            Active = true
        });
        context.PreparedItems.Add(new PreparedItem
        {
            PreparedItemId = PreparedItemId,
            Code = "BTP-BATCH-386",
            Name = "BTP đầu ra",
            BaseUnitId = UnitGram,
            Active = true
        });
        context.Recipes.Add(new Recipe
        {
            RecipeId = RecipeId,
            RecipeCode = "RECIPE-BATCH-386",
            Name = "Công thức 5 kg mỗi mẻ",
            Active = true,
            Status = "Active",
            PreparedItemId = PreparedItemId,
            OutputQuantity = 5_000m,
            OutputUnitId = UnitGram,
            YieldVarianceTolerancePercent = 5m
        });
        context.RecipeDetails.Add(new RecipeDetail
        {
            RecipeDetailId = 38617,
            RecipeId = RecipeId,
            IngredientId = IngredientId,
            Quantity = 500m,
            UnitId = UnitGram
        });
        context.StoreInventories.Add(new StoreInventory
        {
            StoreId = StoreId,
            IngredientId = IngredientId,
            AvailableQty = 5_000m,
            ReservedQty = 0,
            LastUpdated = now,
            RowVersion = new byte[] { 0 }
        });
        context.InventoryCostLayers.Add(new InventoryCostLayer
        {
            StoreId = StoreId,
            IngredientId = IngredientId,
            Quantity = 5_000m,
            RemainingQuantity = 5_000m,
            UnitCost = 10m,
            CreatedAt = now
        });
        var restock = new RestockRequest
        {
            StoreId = StoreId,
            PreparedItemId = PreparedItemId,
            RequestedQuantity = 10_000m,
            RequestedProcurementQuantity = 10_000m,
            ProcurementUnitId = UnitGram,
            Status = RestockRequestStatuses.Processing,
            Priority = RestockRequestPriorities.High,
            SourceType = RestockRequestSourceTypes.StockAlert,
            SourcingStatus = RestockSourcingStatuses.FullyAllocated,
            SourcingDecision = RestockSourcingDecisionTypes.Production,
            CreatedByStaffId = AcceptorStaffId,
            CreatedAt = now,
            UpdatedAt = now
        };
        context.RestockRequests.Add(restock);
        var run = new ProductionRun
        {
            StoreId = StoreId,
            RecipeId = RecipeId,
            RequestedRunCount = 2,
            ContractVersion = 2,
            PlannedBatchCount = 2,
            ExpectedOutputPerBatchBase = 5_000m,
            ExpectedOutputBase = 10_000m,
            OutputBaseUnitId = UnitGram,
            YieldVarianceTolerancePercent = 5m,
            RequestKey = Guid.NewGuid(),
            RequestFingerprint = new string('A', 64),
            Status = status,
            ValuationStatus = ProductionValuationStatus.Pending,
            CreatedByStaffId = AcceptorStaffId,
            CreatedAt = now,
            ConfirmedAt = now,
            StartedByStaffId = status == ProductionRunStatus.InProgress ? OperatorStaffId : null,
            StartedAtUtc = status == ProductionRunStatus.InProgress ? now : null
        };
        context.ProductionRuns.Add(run);
        await context.SaveChangesAsync();
        context.RestockSourcingAllocations.Add(new RestockSourcingAllocation
        {
            RestockRequestId = restock.RestockRequestId,
            DecisionType = RestockSourcingDecisionTypes.Production,
            ProcurementQuantity = 10_000m,
            ProcurementUnitId = UnitGram,
            Status = RestockSourcingAllocationStatuses.Active,
            SourceDocumentType = "PRODUCTION_RUN",
            SourceDocumentId = run.ProductionRunId,
            ProductionRunId = run.ProductionRunId,
            CreatedByStaffId = AcceptorStaffId,
            CreatedAtUtc = now
        });
        await context.SaveChangesAsync();
        return (run.ProductionRunId, restock.RestockRequestId);
    }

    private ProductionRunOperationsService CreateOperationsService(
        AppDbContext context,
        IProductionReadinessService? readiness = null)
    {
        var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
        return new ProductionRunOperationsService(
            context,
            AllowAllPermissions(),
            new UnitConversionService(context, NullLogger<UnitConversionService>.Instance, physical),
            physical,
            readiness ?? ReadyProduction());
    }

    private static IProductionReadinessService ReadyProduction()
    {
        var readiness = new Mock<IProductionReadinessService>();
        readiness.Setup(x => x.PreviewAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>()))
            .ReturnsAsync(ServiceResult<ProductionReadinessPreviewDto>.Success(new ProductionReadinessPreviewDto
            {
                IsReady = true,
                OverallStatus = "Sẵn sàng"
            }));
        return readiness.Object;
    }

    private ProductionRunAcceptanceService CreateAcceptanceService(
        AppDbContext context,
        ILogger<ProductionRunAcceptanceService>? logger = null,
        IStockAlertService? stockAlerts = null)
    {
        var physical = new PhysicalUnitConversionService(context, NullLogger<PhysicalUnitConversionService>.Instance);
        var capabilities = new IInventoryWriterCapabilityProvider[]
        {
            new ProductionPreparedWriterCapabilityProvider()
        };
        var writer = new InventoryWriterModeService(context, physical, capabilities);
        return new ProductionRunAcceptanceService(
            context,
            AllowAllPermissions(),
            writer,
            new StoreInventoryWriteResolver(context, writer),
            new InventoryCostLayerConsumptionService(context),
            new RestockFulfillmentPostingService(context),
            capabilities,
            logger ?? NullLogger<ProductionRunAcceptanceService>.Instance,
            stockAlerts);
    }

    private static IAdminPermissionService AllowAllPermissions()
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
        return permissions.Object;
    }

    private static Staff Staff(int id, string name) => new()
    {
        StaffId = id,
        AccountId = id,
        FullName = name,
        StoreId = StoreId,
        Active = true,
        CreatedAt = DateTime.UtcNow
    };

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public Exception? LastException { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LastException = exception;
        }
    }
}
