using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Production;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Costing;
using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.Admin.Production;

/// <summary>
/// Atomic v2 acceptance: actual FIFO inputs, accepted output, Restock progress and audit.
/// Legacy v1 execution remains in ProductionRunExecutionService.
/// </summary>
public sealed class ProductionRunAcceptanceService : IProductionRunAcceptanceService
{
    private readonly AppDbContext _context;
    private readonly IAdminPermissionService _permissions;
    private readonly IInventoryWriterModeService _writerModeService;
    private readonly IStoreInventoryWriteResolver _writeResolver;
    private readonly IInventoryCostLayerConsumptionService _costLayerConsumption;
    private readonly IRestockFulfillmentPostingService _restockFulfillment;
    private readonly IStockAlertService? _stockAlertService;
    private readonly IEnumerable<IInventoryWriterCapabilityProvider> _capabilityProviders;
    private readonly ILogger<ProductionRunAcceptanceService> _logger;

    public ProductionRunAcceptanceService(
        AppDbContext context,
        IAdminPermissionService permissions,
        IInventoryWriterModeService writerModeService,
        IStoreInventoryWriteResolver writeResolver,
        IInventoryCostLayerConsumptionService costLayerConsumption,
        IRestockFulfillmentPostingService restockFulfillment,
        IEnumerable<IInventoryWriterCapabilityProvider> capabilityProviders,
        ILogger<ProductionRunAcceptanceService> logger,
        IStockAlertService? stockAlertService = null)
    {
        _context = context;
        _permissions = permissions;
        _writerModeService = writerModeService;
        _writeResolver = writeResolver;
        _costLayerConsumption = costLayerConsumption;
        _restockFulfillment = restockFulfillment;
        _stockAlertService = stockAlertService;
        _capabilityProviders = capabilityProviders;
        _logger = logger;
    }

    public async Task<ServiceResult<ProductionRunExecutionResultDto>> AcceptAsync(
        int productionRunId,
        int actorStaffId)
    {
        if (productionRunId <= 0 || actorStaffId <= 0)
            return Failure(ProductionRunExecutionFailureCodes.InvalidRequest, "Thông tin xác nhận nhập kho chưa hợp lệ.");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var run = await LoadRunForUpdateAsync(productionRunId);
            if (run == null || run.ContractVersion != 2)
            {
                await transaction.RollbackAsync();
                return Failure(ProductionRunExecutionFailureCodes.RunNotFound, "Không tìm thấy lệnh sản xuất theo mẻ.");
            }

            var actorAccountId = await _context.Staffs.AsNoTracking()
                .Where(x => x.StaffId == actorStaffId && x.Active)
                .Select(x => x.AccountId)
                .SingleOrDefaultAsync();
            if (actorAccountId <= 0)
            {
                await transaction.RollbackAsync();
                return Failure(ProductionRunExecutionFailureCodes.StaffUnauthorized, "Không xác định được người thực hiện.");
            }
            var permission = await _permissions.HasPermissionAsync(
                actorAccountId,
                PermissionConstants.ProductionOrderAcceptOutput,
                run.StoreId);
            if (!permission.IsSuccess || permission.Data?.Allowed != true)
            {
                await transaction.RollbackAsync();
                return Failure(ProductionRunExecutionFailureCodes.StaffUnauthorized, "Bạn không có quyền xác nhận đầu ra sản xuất tại cửa hàng này.");
            }

            if (run.Status == ProductionRunStatus.Completed)
            {
                var replay = await BuildResultAsync(run, true);
                await transaction.CommitAsync();
                return ServiceResult<ProductionRunExecutionResultDto>.Success(
                    replay,
                    "Lệnh sản xuất đã được nhập kho trước đó.");
            }
            if (run.Status != ProductionRunStatus.AwaitingAcceptance)
            {
                await transaction.RollbackAsync();
                return Failure(
                    ProductionRunExecutionFailureCodes.InvalidStatus,
                    run.Status == ProductionRunStatus.AwaitingVarianceApproval
                        ? "Chênh lệch sản lượng phải được phê duyệt trước khi nhập kho."
                        : "Trạng thái hiện tại không cho phép xác nhận đầu ra.");
            }

            var output = await _context.ProductionRunOutputs
                .SingleOrDefaultAsync(x => x.ProductionRunId == run.ProductionRunId);
            var inputs = await _context.ProductionRunInputActuals
                .Where(x => x.ProductionRunId == run.ProductionRunId)
                .OrderBy(x => x.ProductionRunInputActualId)
                .ToListAsync();
            if (output == null || inputs.Count == 0 || inputs.Sum(x => x.ActualBaseQuantity) <= 0)
            {
                await transaction.RollbackAsync();
                return Failure(
                    ProductionRunExecutionFailureCodes.InvalidOutputContract,
                    "Chưa có đủ số liệu đầu vào và đầu ra thực tế để nhập kho.");
            }
            if (output.AcceptedOutputBase == 0 && !run.VarianceApprovedByStaffId.HasValue)
            {
                await transaction.RollbackAsync();
                return Failure(
                    ProductionRunExecutionFailureCodes.InvalidStatus,
                    "Hoàn tất không có sản lượng đạt phải được phê duyệt chênh lệch.");
            }

            var snapshot = await _writerModeService.AcquireSnapshotAsync(run.StoreId);
            if (!snapshot.IsSuccess || snapshot.Data == null
                || snapshot.Data.WriterMode != InventoryWriterMode.PreparedItem)
            {
                await transaction.RollbackAsync();
                return Failure(
                    snapshot.ErrorCode ?? ProductionRunExecutionFailureCodes.MissingWriterConfiguration,
                    snapshot.Message ?? "Cửa hàng chưa sẵn sàng ghi tồn kho bán thành phẩm.");
            }
            var productionCapability = _capabilityProviders
                .Select(x => x.GetStatus())
                .FirstOrDefault(x => x.CapabilityId == InventoryWriterCapabilityIds.ProductionPreparedWriter);
            if (productionCapability?.Ready != true)
            {
                await transaction.RollbackAsync();
                return Failure(
                    ProductionRunExecutionFailureCodes.CapabilityNotReady,
                    "Dịch vụ ghi tồn kho sản xuất chưa sẵn sàng.");
            }

            var recipe = await _context.Recipes.AsNoTracking()
                .Include(x => x.PreparedItem)
                .SingleOrDefaultAsync(x => x.RecipeId == run.RecipeId);
            if (recipe?.PreparedItemId == null || recipe.PreparedItem == null || !recipe.PreparedItem.Active)
            {
                await transaction.RollbackAsync();
                return Failure(
                    ProductionRunExecutionFailureCodes.PreparedItemInvalid,
                    "Bán thành phẩm đầu ra không còn hợp lệ.");
            }

            var inputPlansResult = await BuildActualInputPlansAsync(run, inputs, snapshot.Data);
            if (!inputPlansResult.IsSuccess || inputPlansResult.Data == null)
            {
                await transaction.RollbackAsync();
                return Failure(inputPlansResult.ErrorCode!, inputPlansResult.Message);
            }
            var inputPlans = inputPlansResult.Data;

            StoreInventory? outputInventory = null;
            if (output.AcceptedOutputBase > 0)
            {
                var outputResolution = await _writeResolver.ResolveAsync(new StoreInventoryWriteRequest
                {
                    ModeSnapshot = snapshot.Data,
                    StoreId = run.StoreId,
                    IdentityType = InventoryWriteIdentityTypes.PreparedItem,
                    PreparedItemId = recipe.PreparedItemId,
                    NormalizedBaseUnitId = recipe.PreparedItem.BaseUnitId,
                    SourceRecipeId = recipe.RecipeId,
                    AllowCreateIntent = true
                });
                if (outputResolution.Status == InventoryWriteResolutionStatuses.FoundCanonical
                    && outputResolution.StoreInventory != null)
                {
                    outputInventory = await LoadInventoryForUpdateAsync(outputResolution.StoreInventory.StoreInventoryId);
                }
                else if (outputResolution.Status == InventoryWriteResolutionStatuses.CreateAllowed)
                {
                    var createOutput = await CreateOutputInventoryAsync(run, recipe.PreparedItemId.Value, actorAccountId);
                    if (!createOutput.IsSuccess || createOutput.Data == null)
                    {
                        await transaction.RollbackAsync();
                        return Failure(createOutput.ErrorCode!, createOutput.Message);
                    }
                    outputInventory = createOutput.Data;
                }
                if (outputInventory == null)
                {
                    await transaction.RollbackAsync();
                    return Failure(
                        ProductionRunExecutionFailureCodes.InventoryResolutionFailed,
                        outputResolution.Message ?? "Không xác định được tồn kho đầu ra.");
                }
                if (inputPlans.Any(x => x.Inventory.StoreInventoryId == outputInventory.StoreInventoryId))
                {
                    await transaction.RollbackAsync();
                    return Failure(
                        ProductionRunExecutionFailureCodes.SelfConsumptionNotSupported,
                        "Không hỗ trợ dùng cùng một dòng tồn làm đầu vào và đầu ra.");
                }
            }

            var costPlans = new List<(ActualInputPlan Input, CostLayerConsumptionPlan Cost)>();
            var gaps = new List<ProductionCostEvidenceGapDto>();
            foreach (var input in inputPlans.OrderBy(x => x.Inventory.StoreInventoryId))
            {
                var inventory = await LoadInventoryForUpdateAsync(input.Inventory.StoreInventoryId);
                if (inventory == null || inventory.AvailableQty - inventory.ReservedQty < input.Quantity)
                {
                    await transaction.RollbackAsync();
                    return Failure(
                        ProductionRunExecutionFailureCodes.InsufficientStock,
                        "Tồn khả dụng không đủ cho số lượng đầu vào thực tế đã xác nhận.");
                }
                input.Inventory = inventory;
                var cost = await _costLayerConsumption.PlanConsumeAsync(
                    run.StoreId,
                    input.IngredientId,
                    input.PreparedItemId,
                    input.Quantity);
                if (!cost.IsSuccess || cost.Data?.IsFullyCovered != true)
                {
                    gaps.Add(new ProductionCostEvidenceGapDto
                    {
                        InputCode = input.Code,
                        InputName = input.Name,
                        RequiredQuantity = input.Quantity,
                        AvailableLayerQuantity = cost.Data?.AvailableLayerQuantity ?? 0,
                        MissingQuantity = Math.Max(0, input.Quantity - (cost.Data?.AvailableLayerQuantity ?? 0))
                    });
                }
                else
                {
                    costPlans.Add((input, cost.Data));
                }
            }
            if (gaps.Count > 0)
            {
                await transaction.RollbackAsync();
                var failed = Failure(
                    ProductionRunExecutionFailureCodes.CostEvidenceIncomplete,
                    "Đầu vào thực tế chưa có đủ bằng chứng giá vốn FIFO.");
                failed.Data = new ProductionRunExecutionResultDto
                {
                    ProductionRunId = run.ProductionRunId,
                    StoreId = run.StoreId,
                    RecipeId = run.RecipeId,
                    CostEvidenceGaps = gaps,
                    StockApplied = false
                };
                return failed;
            }

            var now = DateTime.UtcNow;
            var movements = new List<InventoryTransaction>();
            var allocationPlans = new List<(InventoryTransaction Tx, CostLayerConsumptionPlan Cost)>();
            foreach (var (input, cost) in costPlans)
            {
                _costLayerConsumption.ApplyPlan(cost);
                var before = input.Inventory.AvailableQty;
                input.Inventory.AvailableQty -= input.Quantity;
                input.Inventory.LastUpdated = now;
                var movement = new InventoryTransaction
                {
                    StoreInventoryId = input.Inventory.StoreInventoryId,
                    Type = InventoryTransactionTypeEnum.PRODUCTION_OUT,
                    StockStatus = InventoryStockStatus.NORMAL,
                    Quantity = input.Quantity,
                    BeforeQty = before,
                    AfterQty = input.Inventory.AvailableQty,
                    UnitCost = cost.WeightedUnitCost,
                    TotalCost = cost.TotalCost,
                    ProductionRunId = run.ProductionRunId,
                    CreatedAt = now
                };
                movements.Add(movement);
                allocationPlans.Add((movement, cost));
            }

            var totalInputCost = costPlans.Sum(x => x.Cost.TotalCost);
            decimal? outputUnitCost = null;
            if (output.AcceptedOutputBase > 0)
            {
                outputUnitCost = totalInputCost / output.AcceptedOutputBase;
                var before = outputInventory!.AvailableQty;
                outputInventory.AvailableQty += output.AcceptedOutputBase;
                outputInventory.LastUpdated = now;
                movements.Add(new InventoryTransaction
                {
                    StoreInventoryId = outputInventory.StoreInventoryId,
                    Type = InventoryTransactionTypeEnum.PRODUCTION_IN,
                    StockStatus = InventoryStockStatus.NORMAL,
                    Quantity = output.AcceptedOutputBase,
                    BeforeQty = before,
                    AfterQty = outputInventory.AvailableQty,
                    UnitCost = outputUnitCost,
                    TotalCost = totalInputCost,
                    ProductionRunId = run.ProductionRunId,
                    CreatedAt = now
                });
            }

            _context.InventoryTransactions.AddRange(movements);
            await _context.SaveChangesAsync();
            foreach (var (movement, cost) in allocationPlans)
            {
                foreach (var slice in cost.Slices)
                {
                    _context.ProductionCostAllocations.Add(new ProductionCostAllocation
                    {
                        ProductionRunId = run.ProductionRunId,
                        InventoryTransactionId = movement.InventoryTransactionId,
                        InventoryCostLayerId = slice.InventoryCostLayerId,
                        Quantity = slice.Quantity,
                        UnitCost = slice.UnitCost,
                        TotalCost = slice.TotalCost,
                        CreatedAtUtc = now
                    });
                }
            }
            if (output.AcceptedOutputBase > 0)
            {
                _context.InventoryCostLayers.Add(new InventoryCostLayer
                {
                    StoreId = run.StoreId,
                    PreparedItemId = recipe.PreparedItemId,
                    Quantity = output.AcceptedOutputBase,
                    RemainingQuantity = output.AcceptedOutputBase,
                    UnitCost = outputUnitCost!.Value,
                    CreatedAt = now,
                    SourceProductionRunId = run.ProductionRunId
                });
            }

            var allocation = await _context.RestockSourcingAllocations
                .Include(x => x.RestockRequest)
                .SingleOrDefaultAsync(x => x.ProductionRunId == run.ProductionRunId);
            if (allocation == null)
            {
                await transaction.RollbackAsync();
                return Failure(
                    ProductionRunExecutionFailureCodes.InvalidOutputContract,
                    "Lệnh sản xuất không còn liên kết với yêu cầu nhập hàng.");
            }
            var postedQuantities = await _context.RestockFulfillmentPostings.AsNoTracking()
                .Where(x => x.RestockRequestId == allocation.RestockRequestId)
                .Select(x => x.Quantity)
                .ToListAsync();
            var alreadyPosted = postedQuantities.Sum();
            var remainingDemand = Math.Max(
                0,
                allocation.RestockRequest.RequestedQuantity
                    - allocation.RestockRequest.ClosedRemainingQuantity
                    - alreadyPosted);
            var restockQuantity = Math.Min(output.AcceptedOutputBase, remainingDemand);
            if (restockQuantity > 0)
            {
                var fulfillment = await _restockFulfillment.RegisterAsync(new RegisterRestockFulfillmentPostingCommand
                {
                    RestockRequestId = allocation.RestockRequestId,
                    DestinationStoreId = run.StoreId,
                    SourceDocumentType = RestockFulfillmentDocumentTypes.ProductionRun,
                    SourceDocumentId = run.ProductionRunId,
                    SourceDocumentLineId = output.ProductionRunOutputId,
                    PreparedItemId = recipe.PreparedItemId,
                    Quantity = restockQuantity,
                    BaseUnitId = recipe.PreparedItem.BaseUnitId,
                    ActorStaffId = actorStaffId,
                    Reason = "Sản lượng đạt đã được xác nhận từ lệnh sản xuất."
                });
                if (!fulfillment.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return Failure(
                        ProductionRunExecutionFailureCodes.ExecutionFailed,
                        fulfillment.Message);
                }
            }

            var previous = run.Status;
            run.Status = ProductionRunStatus.Completed;
            run.CompletedByStaffId = actorStaffId;
            run.CompletedAt = now;
            run.ValuationStatus = ProductionValuationStatus.Complete;
            run.TotalInputCost = totalInputCost;
            run.OutputUnitCost = outputUnitCost;
            run.ValuedAtUtc = now;
            allocation.Status = RestockSourcingAllocationStatuses.Released;
            allocation.ReleasedAtUtc = now;
            allocation.ReleasedByStaffId = actorStaffId;
            allocation.ReleaseReason = "Lệnh sản xuất đã hoàn tất; phần bao phủ mở được giải phóng để đánh giá lại nhu cầu.";
            var remainingActiveAllocationQuantity = await _context.RestockSourcingAllocations
                .AsNoTracking()
                .Where(x => x.RestockRequestId == allocation.RestockRequestId
                    && x.RestockSourcingAllocationId != allocation.RestockSourcingAllocationId
                    && (x.Status == RestockSourcingAllocationStatuses.Active
                        || x.Status == RestockSourcingAllocationStatuses.PendingPurchaseAdvice))
                .Select(x => x.ProcurementQuantity)
                .ToListAsync();
            var activeQuantity = remainingActiveAllocationQuantity.Sum();
            var requestedQuantity = allocation.RestockRequest.RequestedProcurementQuantity
                ?? allocation.RestockRequest.RequestedQuantity;
            allocation.RestockRequest.SourcingStatus = activeQuantity <= 0
                ? RestockSourcingStatuses.Unallocated
                : activeQuantity >= requestedQuantity
                    ? RestockSourcingStatuses.FullyAllocated
                    : RestockSourcingStatuses.PartiallyAllocated;
            if (activeQuantity <= 0)
                allocation.RestockRequest.SourcingDecision = null;
            allocation.RestockRequest.UpdatedAt = now;
            _context.ProductionRunTransitions.Add(new ProductionRunTransition
            {
                ProductionRunId = run.ProductionRunId,
                FromStatus = previous.ToString().ToUpperInvariant(),
                ToStatus = nameof(ProductionRunStatus.Completed).ToUpperInvariant(),
                ActorStaffId = actorStaffId,
                OccurredAtUtc = now,
                Reason = restockQuantity < output.AcceptedOutputBase
                    ? "Đã nhập toàn bộ sản lượng đạt; phần vượt nhu cầu không tự phân bổ sang yêu cầu khác."
                    : "Đã nhập sản lượng đạt và cập nhật tiến độ yêu cầu."
            });

            await _context.SaveChangesAsync();
            var result = await BuildResultAsync(run, false);
            result.NormalizedOutputQuantity = output.AcceptedOutputBase;
            result.OutputStoreInventoryId = outputInventory?.StoreInventoryId;
            result.OutputPreparedItemId = recipe.PreparedItemId;
            result.OutputBaseUnitId = recipe.PreparedItem.BaseUnitId;
            await transaction.CommitAsync();
            if (outputInventory != null)
                await ReevaluatePreparedItemAlertSafeAsync(run, outputInventory.StoreInventoryId);
            return ServiceResult<ProductionRunExecutionResultDto>.Success(
                result,
                output.AcceptedOutputBase == 0
                    ? "Đã hoàn tất lệnh không có sản lượng đạt; không ghi tăng tồn kho."
                    : "Đã nhập sản lượng đạt và cập nhật yêu cầu nhập hàng.");
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            _logger.LogWarning(ex, "Concurrent production acceptance {ProductionRunId}", productionRunId);
            return Failure(
                ProductionRunExecutionFailureCodes.ConcurrencyConflict,
                "Lệnh sản xuất đã được người khác xác nhận. Vui lòng tải lại dữ liệu.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            _logger.LogError(ex, "Production acceptance failed {ProductionRunId}", productionRunId);
            return Failure(
                ProductionRunExecutionFailureCodes.ExecutionFailed,
                "Không thể xác nhận đầu ra sản xuất lúc này. Vui lòng thử lại.");
        }
    }

    private async Task ReevaluatePreparedItemAlertSafeAsync(ProductionRun run, int storeInventoryId)
    {
        if (_stockAlertService == null)
            return;

        try
        {
            var evaluation = await _stockAlertService.EvaluateStoreInventoryItemAsync(
                storeInventoryId,
                StockAlertSources.ProductionAcceptance);
            if (!evaluation.IsSuccess)
            {
                _logger.LogWarning(
                    "Post-accept stock alert reevaluation failed RunId={ProductionRunId} StoreId={StoreId} InventoryId={StoreInventoryId}: {Message}",
                    run.ProductionRunId,
                    run.StoreId,
                    storeInventoryId,
                    evaluation.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Post-accept stock alert reevaluation threw RunId={ProductionRunId} StoreId={StoreId} InventoryId={StoreInventoryId}",
                run.ProductionRunId,
                run.StoreId,
                storeInventoryId);
        }
    }

    private async Task<ServiceResult<List<ActualInputPlan>>> BuildActualInputPlansAsync(
        ProductionRun run,
        IReadOnlyCollection<ProductionRunInputActual> actualInputs,
        InventoryWriterModeSnapshot snapshot)
    {
        var result = new List<ActualInputPlan>();
        foreach (var input in actualInputs.Where(x => x.ActualBaseQuantity > 0))
        {
            var resolution = await _writeResolver.ResolveAsync(new StoreInventoryWriteRequest
            {
                ModeSnapshot = snapshot,
                StoreId = run.StoreId,
                IdentityType = input.IngredientId.HasValue
                    ? InventoryWriteIdentityTypes.Ingredient
                    : InventoryWriteIdentityTypes.PreparedItem,
                IngredientId = input.IngredientId,
                PreparedItemId = input.PreparedItemId,
                NormalizedBaseUnitId = input.BaseUnitId,
                AllowCreateIntent = false
            });
            if (resolution.Status != InventoryWriteResolutionStatuses.FoundCanonical
                || resolution.StoreInventory == null)
            {
                return ServiceResult<List<ActualInputPlan>>.Failure(
                    resolution.Message ?? "Không tìm thấy tồn kho đầu vào thực tế.",
                    errorCode: ProductionRunExecutionFailureCodes.MissingInputInventory);
            }

            string code;
            string name;
            if (input.IngredientId.HasValue)
            {
                var identity = await _context.Ingredients.AsNoTracking()
                    .Where(x => x.IngredientId == input.IngredientId)
                    .Select(x => new { x.Code, x.Name })
                    .SingleAsync();
                code = identity.Code;
                name = identity.Name;
            }
            else
            {
                var identity = await _context.PreparedItems.AsNoTracking()
                    .Where(x => x.PreparedItemId == input.PreparedItemId)
                    .Select(x => new { x.Code, x.Name })
                    .SingleAsync();
                code = identity.Code;
                name = identity.Name;
            }
            result.Add(new ActualInputPlan
            {
                Inventory = resolution.StoreInventory,
                IngredientId = input.IngredientId,
                PreparedItemId = input.PreparedItemId,
                Quantity = input.ActualBaseQuantity,
                Code = code,
                Name = name
            });
        }
        return ServiceResult<List<ActualInputPlan>>.Success(result);
    }

    private async Task<ProductionRun?> LoadRunForUpdateAsync(int id)
    {
        if (_context.Database.IsSqlServer())
        {
            return await _context.ProductionRuns
                .FromSqlInterpolated(
                    $@"SELECT * FROM ProductionRuns WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                       WHERE ProductionRunId = {id}")
                .SingleOrDefaultAsync();
        }
        return await _context.ProductionRuns.SingleOrDefaultAsync(x => x.ProductionRunId == id);
    }

    private async Task<StoreInventory?> LoadInventoryForUpdateAsync(int id)
    {
        if (_context.Database.IsSqlServer())
        {
            return await _context.StoreInventories
                .FromSqlInterpolated(
                    $@"SELECT * FROM StoreInventories WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
                       WHERE StoreInventoryId = {id}")
                .SingleOrDefaultAsync();
        }
        return await _context.StoreInventories.SingleOrDefaultAsync(x => x.StoreInventoryId == id);
    }

    private async Task<ServiceResult<StoreInventory>> CreateOutputInventoryAsync(
        ProductionRun run,
        int preparedItemId,
        int actorAccountId)
    {
        var existing = await _context.StoreInventories
            .Where(x => x.StoreId == run.StoreId
                && x.PreparedItemId == preparedItemId
                && x.BtpIdentityState == BtpIdentityState.Canonical)
            .ToListAsync();
        if (existing.Count == 1)
            return ServiceResult<StoreInventory>.Success(existing[0]);
        if (existing.Count > 1)
            return ServiceResult<StoreInventory>.Failure(
                "Có nhiều dòng tồn kho chuẩn cho cùng bán thành phẩm.",
                errorCode: ProductionRunExecutionFailureCodes.InventoryResolutionFailed);

        var now = DateTime.UtcNow;
        var row = new StoreInventory
        {
            StoreId = run.StoreId,
            PreparedItemId = preparedItemId,
            BtpIdentityState = BtpIdentityState.Canonical,
            QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
            QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
            QuantitySemanticsEvidenceReference = $"ProductionRun:{run.ProductionRunId}",
            QuantitySemanticsReviewedAt = now,
            QuantitySemanticsReviewedByAccountId = actorAccountId,
            AvailableQty = 0,
            ReservedQty = 0,
            MinStockLevel = 0,
            MaxNegativeQty = 0,
            LastUpdated = now
        };
        _context.StoreInventories.Add(row);
        await _context.SaveChangesAsync();
        return ServiceResult<StoreInventory>.Success(row);
    }

    private async Task<ProductionRunExecutionResultDto> BuildResultAsync(ProductionRun run, bool replay)
    {
        var movements = await _context.InventoryTransactions.AsNoTracking()
            .Where(x => x.ProductionRunId == run.ProductionRunId)
            .OrderBy(x => x.InventoryTransactionId)
            .Select(x => new ProductionRunMovementSummaryDto
            {
                InventoryTransactionId = x.InventoryTransactionId,
                StoreInventoryId = x.StoreInventoryId,
                Type = x.Type.ToString(),
                Quantity = x.Quantity,
                BeforeQty = x.BeforeQty,
                AfterQty = x.AfterQty,
                UnitCost = x.UnitCost,
                TotalCost = x.TotalCost
            })
            .ToListAsync();
        return new ProductionRunExecutionResultDto
        {
            ProductionRunId = run.ProductionRunId,
            StoreId = run.StoreId,
            RecipeId = run.RecipeId,
            RequestedRunCount = run.RequestedRunCount,
            Status = run.Status.ToString().ToUpperInvariant(),
            WasReplay = replay,
            StockApplied = run.Status == ProductionRunStatus.Completed,
            CompletedAt = run.CompletedAt,
            NormalizedOutputQuantity = movements
                .Where(x => x.Type == nameof(InventoryTransactionTypeEnum.PRODUCTION_IN))
                .Select(x => (decimal?)x.Quantity)
                .FirstOrDefault() ?? 0,
            ValuationStatus = run.ValuationStatus.ToString(),
            TotalInputCost = run.TotalInputCost,
            OutputUnitCost = run.OutputUnitCost,
            ValuedAtUtc = run.ValuedAtUtc,
            MessageKey = replay ? "ProductionRun.AcceptanceReplay" : "ProductionRun.Accepted",
            Movements = movements
        };
    }

    private static ServiceResult<ProductionRunExecutionResultDto> Failure(string code, string message)
        => ServiceResult<ProductionRunExecutionResultDto>.Failure(message, errorCode: code);

    private sealed class ActualInputPlan
    {
        public StoreInventory Inventory { get; set; } = null!;
        public int? IngredientId { get; init; }
        public int? PreparedItemId { get; init; }
        public decimal Quantity { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
    }
}
