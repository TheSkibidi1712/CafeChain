using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Admin.Production;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Production;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.Admin.Production
{
    /// <summary>
    /// Issue #120 — execute CONFIRMED ProductionRun once: deduct inputs, credit PreparedItem output.
    /// </summary>
    public sealed class ProductionRunExecutionService : IProductionRunExecutionService
    {
        private readonly AppDbContext _context;
        private readonly IScopeAuthorizationService _scopeAuthorization;
        private readonly IInventoryWriterModeService _writerModeService;
        private readonly IStoreInventoryWriteResolver _writeResolver;
        private readonly IPhysicalUnitConversionService _physicalConversion;
        private readonly IUnitConversionService _unitConversion;
        private readonly IEnumerable<IInventoryWriterCapabilityProvider> _capabilityProviders;
        private readonly ILogger<ProductionRunExecutionService> _logger;

        public ProductionRunExecutionService(
            AppDbContext context,
            IScopeAuthorizationService scopeAuthorization,
            IInventoryWriterModeService writerModeService,
            IStoreInventoryWriteResolver writeResolver,
            IPhysicalUnitConversionService physicalConversion,
            IUnitConversionService unitConversion,
            IEnumerable<IInventoryWriterCapabilityProvider> capabilityProviders,
            ILogger<ProductionRunExecutionService> logger)
        {
            _context = context;
            _scopeAuthorization = scopeAuthorization;
            _writerModeService = writerModeService;
            _writeResolver = writeResolver;
            _physicalConversion = physicalConversion;
            _unitConversion = unitConversion;
            _capabilityProviders = capabilityProviders;
            _logger = logger;
        }

        public async Task<ServiceResult<ProductionRunExecutionResultDto>> ExecuteAsync(
            int productionRunId,
            int staffId,
            int staffHomeStoreId)
        {
            if (productionRunId <= 0)
                return Fail(ProductionRunExecutionFailureCodes.InvalidRequest, "ProductionRunId không hợp lệ.");
            if (staffId <= 0)
                return Fail(ProductionRunExecutionFailureCodes.StaffUnauthorized, "Thiếu thông tin nhân viên.");

            var actorAccountId = await _context.Staffs.AsNoTracking()
                .Where(s => s.StaffId == staffId && s.Active)
                .Select(s => s.AccountId)
                .FirstOrDefaultAsync();
            if (actorAccountId <= 0)
                return Fail(ProductionRunExecutionFailureCodes.StaffUnauthorized, "Nhân viên không hợp lệ.");

            // Outer retry when concurrent canonical create forces full restart
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var result = await ExecuteOnceAsync(productionRunId, staffId, staffHomeStoreId, actorAccountId);
                if (result.IsSuccess
                    || result.ErrorCode != ProductionRunExecutionFailureCodes.ConcurrencyConflict
                    || attempt == 2)
                {
                    return result;
                }

                // Full reset: do not reuse failed transaction or poisoned tracker state.
                _context.ChangeTracker.Clear();
                _logger.LogWarning(
                    "[ProductionRunExecute] Retry after concurrency ProductionRunId={Id} Attempt={Attempt}",
                    productionRunId,
                    attempt + 1);
            }

            return Fail(ProductionRunExecutionFailureCodes.ConcurrencyConflict, "Xung đột đồng thời. Vui lòng thử lại.");
        }

        private async Task<ServiceResult<ProductionRunExecutionResultDto>> ExecuteOnceAsync(
            int productionRunId,
            int staffId,
            int staffHomeStoreId,
            int actorAccountId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var run = await LoadProductionRunForUpdateAsync(productionRunId);
                if (run == null)
                {
                    await transaction.RollbackAsync();
                    return Fail(ProductionRunExecutionFailureCodes.RunNotFound, "Không tìm thấy lệnh sơ chế.");
                }

                var auth = await AuthorizeAsync(staffId, staffHomeStoreId, run.StoreId);
                if (!auth.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return Fail(auth.ErrorCode!, auth.Message);
                }

                if (run.Status == ProductionRunStatus.Completed)
                {
                    var replay = await BuildResultFromCompletedAsync(run, wasReplay: true);
                    await transaction.CommitAsync();
                    return ServiceResult<ProductionRunExecutionResultDto>.Success(
                        replay,
                        "Lệnh sơ chế đã được áp dụng vào kho trước đó.");
                }

                if (run.Status != ProductionRunStatus.Confirmed)
                {
                    await transaction.RollbackAsync();
                    return Fail(
                        ProductionRunExecutionFailureCodes.InvalidStatus,
                        "Chỉ lệnh CONFIRMED mới được áp dụng vào kho.");
                }

                var snapshotResult = await _writerModeService.AcquireSnapshotAsync(run.StoreId);
                if (!snapshotResult.IsSuccess || snapshotResult.Data == null)
                {
                    await transaction.RollbackAsync();
                    return Fail(
                        snapshotResult.ErrorCode ?? ProductionRunExecutionFailureCodes.MissingWriterConfiguration,
                        snapshotResult.Message);
                }

                var mode = snapshotResult.Data.WriterMode;
                if (mode == InventoryWriterMode.LegacyRecipe)
                {
                    await transaction.RollbackAsync();
                    return Fail(
                        ProductionRunExecutionFailureCodes.ModeLegacy,
                        "Cửa hàng đang LegacyRecipe; không chạy writer RecipeId cũ. Chuyển PreparedItem để áp dụng kho.");
                }

                if (mode == InventoryWriterMode.Blocked)
                {
                    await transaction.RollbackAsync();
                    return Fail(
                        ProductionRunExecutionFailureCodes.ModeBlocked,
                        "Kho BTP đang bị khóa.");
                }

                if (mode != InventoryWriterMode.PreparedItem)
                {
                    await transaction.RollbackAsync();
                    return Fail(
                        ProductionRunExecutionFailureCodes.CapabilityNotReady,
                        "Chế độ ghi kho không hỗ trợ production PreparedItem.");
                }

                // Only PRODUCTION_PREPARED_WRITER must be Ready (other capabilities still block global mode cutover).
                var productionCap = _capabilityProviders
                    .Select(p => p.GetStatus())
                    .FirstOrDefault(s => s.CapabilityId == InventoryWriterCapabilityIds.ProductionPreparedWriter);
                if (productionCap == null || !productionCap.Ready)
                {
                    await transaction.RollbackAsync();
                    return Fail(
                        ProductionRunExecutionFailureCodes.CapabilityNotReady,
                        "PRODUCTION_PREPARED_WRITER chưa sẵn sàng.");
                }

                var recipe = await _context.Recipes
                    .Include(r => r.RecipeDetails)
                    .Include(r => r.PreparedItem)
                    .Include(r => r.OutputUnit)
                    .FirstOrDefaultAsync(r => r.RecipeId == run.RecipeId);

                if (recipe == null)
                {
                    await transaction.RollbackAsync();
                    return Fail(ProductionRunExecutionFailureCodes.RecipeNotFound, "Không tìm thấy công thức của lệnh.");
                }

                if (!recipe.PreparedItemId.HasValue
                    || recipe.OutputQuantity is null or <= 0
                    || !recipe.OutputUnitId.HasValue)
                {
                    await transaction.RollbackAsync();
                    return Fail(
                        ProductionRunExecutionFailureCodes.InvalidOutputContract,
                        "Recipe thiếu PreparedItemId / OutputQuantity / OutputUnitId.");
                }

                var outputPi = recipe.PreparedItem
                    ?? await _context.PreparedItems.FirstOrDefaultAsync(p => p.PreparedItemId == recipe.PreparedItemId.Value);

                if (outputPi == null || !outputPi.Active)
                {
                    await transaction.RollbackAsync();
                    return Fail(
                        ProductionRunExecutionFailureCodes.PreparedItemInvalid,
                        "PreparedItem đầu ra không hợp lệ hoặc không Active.");
                }

                var rawOutput = recipe.OutputQuantity.Value * run.RequestedRunCount;
                var outputConvert = await _physicalConversion.ConvertAsync(
                    rawOutput,
                    recipe.OutputUnitId.Value,
                    outputPi.BaseUnitId);
                if (!outputConvert.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return Fail(
                        ProductionRunExecutionFailureCodes.ConversionFailed,
                        outputConvert.Message ?? "Không quy đổi được sản lượng đầu ra.");
                }

                var normalizedOutput = outputConvert.Data;
                if (normalizedOutput <= 0)
                {
                    await transaction.RollbackAsync();
                    return Fail(
                        ProductionRunExecutionFailureCodes.InvalidOutputContract,
                        "Sản lượng chuẩn hóa phải > 0.");
                }

                // Build aggregated input plan
                var planResult = await BuildInputPlanAsync(run, recipe, snapshotResult.Data);
                if (!planResult.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return Fail(planResult.ErrorCode!, planResult.Message);
                }

                var inputPlan = planResult.Data!;

                // Resolve/create output inventory
                var outputResolve = await _writeResolver.ResolveAsync(new StoreInventoryWriteRequest
                {
                    ModeSnapshot = snapshotResult.Data,
                    StoreId = run.StoreId,
                    IdentityType = InventoryWriteIdentityTypes.PreparedItem,
                    PreparedItemId = outputPi.PreparedItemId,
                    NormalizedBaseUnitId = outputPi.BaseUnitId,
                    SourceRecipeId = recipe.RecipeId,
                    AllowCreateIntent = true
                });

                StoreInventory outputInv;
                if (outputResolve.Status == InventoryWriteResolutionStatuses.FoundCanonical
                    && outputResolve.StoreInventory != null)
                {
                    outputInv = outputResolve.StoreInventory;
                }
                else if (outputResolve.Status == InventoryWriteResolutionStatuses.CreateAllowed)
                {
                    var created = await CreateCanonicalOutputRowAsync(
                        run,
                        outputPi.PreparedItemId,
                        actorAccountId);
                    if (!created.IsSuccess || created.Data == null)
                    {
                        await transaction.RollbackAsync();
                        return Fail(
                            created.ErrorCode ?? ProductionRunExecutionFailureCodes.ExecutionFailed,
                            created.Message);
                    }

                    outputInv = created.Data;
                }
                else
                {
                    await transaction.RollbackAsync();
                    return Fail(
                        ProductionRunExecutionFailureCodes.InventoryResolutionFailed,
                        outputResolve.Message);
                }

                // Self-consumption: same inventory as input and output
                if (inputPlan.Any(i => i.StoreInventoryId == outputInv.StoreInventoryId))
                {
                    await transaction.RollbackAsync();
                    return Fail(
                        ProductionRunExecutionFailureCodes.SelfConsumptionNotSupported,
                        "Đầu vào và đầu ra cùng một dòng tồn — chưa hỗ trợ trong #120.");
                }

                // Validate usable stock before mutation
                foreach (var line in inputPlan.OrderBy(x => x.StoreInventoryId))
                {
                    var inv = line.Inventory;
                    var usable = inv.AvailableQty - inv.ReservedQty;
                    if (usable < line.RequiredQty)
                    {
                        await transaction.RollbackAsync();
                        return Fail(
                            ProductionRunExecutionFailureCodes.InsufficientStock,
                            $"Không đủ tồn khả dụng (Available − Reserved) cho StoreInventory #{inv.StoreInventoryId}. Cần {line.RequiredQty}, khả dụng {usable}.");
                    }
                }

                var now = DateTime.UtcNow;
                var movements = new List<InventoryTransaction>();

                // Mutate inputs
                foreach (var line in inputPlan.OrderBy(x => x.StoreInventoryId))
                {
                    var inv = line.Inventory;
                    var before = inv.AvailableQty;
                    inv.AvailableQty -= line.RequiredQty;
                    inv.LastUpdated = now;

                    movements.Add(new InventoryTransaction
                    {
                        StoreInventoryId = inv.StoreInventoryId,
                        Type = InventoryTransactionTypeEnum.PRODUCTION_OUT,
                        StockStatus = InventoryStockStatus.NORMAL,
                        Quantity = line.RequiredQty,
                        BeforeQty = before,
                        AfterQty = inv.AvailableQty,
                        ProductionRunId = run.ProductionRunId,
                        CreatedAt = now
                    });
                }

                // Credit output
                var outBefore = outputInv.AvailableQty;
                outputInv.AvailableQty += normalizedOutput;
                outputInv.LastUpdated = now;
                movements.Add(new InventoryTransaction
                {
                    StoreInventoryId = outputInv.StoreInventoryId,
                    Type = InventoryTransactionTypeEnum.PRODUCTION_IN,
                    StockStatus = InventoryStockStatus.NORMAL,
                    Quantity = normalizedOutput,
                    BeforeQty = outBefore,
                    AfterQty = outputInv.AvailableQty,
                    ProductionRunId = run.ProductionRunId,
                    CreatedAt = now
                });

                _context.InventoryTransactions.AddRange(movements);

                run.Status = ProductionRunStatus.Completed;
                run.CompletedAt = now;
                run.CompletedByStaffId = staffId;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var dto = await BuildResultFromCompletedAsync(run, wasReplay: false);
                dto.NormalizedOutputQuantity = normalizedOutput;
                dto.OutputBaseUnitId = outputPi.BaseUnitId;
                dto.OutputStoreInventoryId = outputInv.StoreInventoryId;
                dto.OutputPreparedItemId = outputPi.PreparedItemId;

                _logger.LogInformation(
                    "[ProductionRunExecute] Completed ProductionRunId={Id} StoreId={StoreId} OutputQty={Qty}",
                    run.ProductionRunId,
                    run.StoreId,
                    normalizedOutput);

                return ServiceResult<ProductionRunExecutionResultDto>.Success(
                    dto,
                    "Đã áp dụng lệnh sơ chế vào kho.");
            }
            catch (DbUpdateException ex)
            {
                try { await transaction.RollbackAsync(); } catch { /* ignore */ }
                _context.ChangeTracker.Clear();

                // Unique canonical or movement race → signal outer retry (full re-lock from start)
                if (IsUniqueViolation(ex))
                {
                    _logger.LogWarning(ex, "[ProductionRunExecute] Unique violation ProductionRunId={Id}", productionRunId);
                    return Fail(
                        ProductionRunExecutionFailureCodes.ConcurrencyConflict,
                        "Xung đột đồng thời khi ghi kho.");
                }

                _logger.LogError(ex, "[ProductionRunExecute] Failed ProductionRunId={Id}", productionRunId);
                return Fail(ProductionRunExecutionFailureCodes.ExecutionFailed, "Không thể áp dụng kho. Vui lòng thử lại.");
            }
            catch (Exception ex)
            {
                try { await transaction.RollbackAsync(); } catch { /* ignore */ }
                _context.ChangeTracker.Clear();
                _logger.LogError(ex, "[ProductionRunExecute] Failed ProductionRunId={Id}", productionRunId);
                return Fail(ProductionRunExecutionFailureCodes.ExecutionFailed, "Không thể áp dụng kho. Vui lòng thử lại.");
            }
        }

        private async Task<ProductionRun?> LoadProductionRunForUpdateAsync(int productionRunId)
        {
            if (_context.Database.IsSqlServer())
            {
                return await _context.ProductionRuns
                    .FromSqlInterpolated(
                        $@"SELECT * FROM ProductionRuns WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
                           WHERE ProductionRunId = {productionRunId}")
                    .SingleOrDefaultAsync();
            }

            // SQLite / tests: rely on transaction isolation + unique indexes
            return await _context.ProductionRuns
                .SingleOrDefaultAsync(r => r.ProductionRunId == productionRunId);
        }

        private async Task<ServiceResult> AuthorizeAsync(int staffId, int staffHomeStoreId, int storeId)
        {
            var staffOk = await _context.Staffs.AsNoTracking()
                .AnyAsync(s => s.StaffId == staffId && s.Active);
            if (!staffOk)
                return ServiceResult.Failure("Nhân viên không hoạt động.", errorCode: ProductionRunExecutionFailureCodes.StaffUnauthorized);

            if (storeId == staffHomeStoreId)
                return ServiceResult.Success();

            if (await _scopeAuthorization.CanAccessStoreAsync(staffId, storeId))
                return ServiceResult.Success();

            return ServiceResult.Failure(
                "Bạn không có quyền áp dụng kho cho cửa hàng của lệnh này.",
                errorCode: ProductionRunExecutionFailureCodes.StoreUnauthorized);
        }

        private sealed class InputPlanLine
        {
            public int StoreInventoryId { get; init; }
            public StoreInventory Inventory { get; init; } = null!;
            public decimal RequiredQty { get; set; }
            public List<int> SourceDetailIds { get; } = new();
        }

        private async Task<ServiceResult<List<InputPlanLine>>> BuildInputPlanAsync(
            ProductionRun run,
            Models.Drinks.Recipe recipe,
            InventoryWriterModeSnapshot snapshot)
        {
            var details = recipe.RecipeDetails?.ToList() ?? new List<Models.Drinks.RecipeDetail>();
            if (details.Count == 0)
            {
                return ServiceResult<List<InputPlanLine>>.Failure(
                    "Công thức không có chi tiết BOM.",
                    errorCode: ProductionRunExecutionFailureCodes.InvalidOutputContract);
            }

            // Temporary lines keyed by identity before inventory id known
            var normalized = new List<(int? IngredientId, int? PreparedItemId, decimal Qty, int DetailId, string Label)>();

            foreach (var d in details)
            {
                var hasIng = d.IngredientId.HasValue;
                var hasChild = d.ChildRecipeId.HasValue;
                if (hasIng == hasChild)
                {
                    return ServiceResult<List<InputPlanLine>>.Failure(
                        $"RecipeDetail #{d.RecipeDetailId} phải có đúng một IngredientId hoặc ChildRecipeId.",
                        errorCode: ProductionRunExecutionFailureCodes.InvalidOutputContract);
                }

                var raw = d.Quantity * run.RequestedRunCount;
                if (raw <= 0)
                {
                    return ServiceResult<List<InputPlanLine>>.Failure(
                        $"Số lượng chi tiết #{d.RecipeDetailId} không hợp lệ.",
                        errorCode: ProductionRunExecutionFailureCodes.InvalidOutputContract);
                }

                if (hasIng)
                {
                    var ingredient = await _context.Ingredients
                        .AsNoTracking()
                        .FirstOrDefaultAsync(i => i.IngredientId == d.IngredientId!.Value);
                    if (ingredient == null || !ingredient.Active)
                    {
                        return ServiceResult<List<InputPlanLine>>.Failure(
                            $"Nguyên liệu #{d.IngredientId} không hợp lệ.",
                            errorCode: ProductionRunExecutionFailureCodes.InvalidOutputContract);
                    }

                    var converted = await _unitConversion.ConvertAsync(
                        ingredient.IngredientId,
                        raw,
                        d.UnitId,
                        ingredient.BaseUnitId);
                    if (!converted.IsSuccess)
                    {
                        return ServiceResult<List<InputPlanLine>>.Failure(
                            converted.Message ?? "Lỗi quy đổi đơn vị nguyên liệu.",
                            errorCode: ProductionRunExecutionFailureCodes.ConversionFailed);
                    }

                    normalized.Add((ingredient.IngredientId, null, converted.Data, d.RecipeDetailId, ingredient.Name));
                }
                else
                {
                    var child = await _context.Recipes
                        .AsNoTracking()
                        .Include(r => r.PreparedItem)
                        .FirstOrDefaultAsync(r => r.RecipeId == d.ChildRecipeId!.Value);
                    if (child == null)
                    {
                        return ServiceResult<List<InputPlanLine>>.Failure(
                            $"ChildRecipe #{d.ChildRecipeId} không tồn tại.",
                            errorCode: ProductionRunExecutionFailureCodes.RecipeNotFound);
                    }

                    if (!child.PreparedItemId.HasValue)
                    {
                        return ServiceResult<List<InputPlanLine>>.Failure(
                            $"ChildRecipe #{child.RecipeId} chưa map PreparedItem.",
                            errorCode: ProductionRunExecutionFailureCodes.UnmappedChildRecipe);
                    }

                    var childPi = child.PreparedItem
                        ?? await _context.PreparedItems.AsNoTracking()
                            .FirstOrDefaultAsync(p => p.PreparedItemId == child.PreparedItemId.Value);
                    if (childPi == null || !childPi.Active)
                    {
                        return ServiceResult<List<InputPlanLine>>.Failure(
                            $"PreparedItem của ChildRecipe #{child.RecipeId} không hợp lệ.",
                            errorCode: ProductionRunExecutionFailureCodes.PreparedItemInvalid);
                    }

                    var converted = await _physicalConversion.ConvertAsync(
                        raw,
                        d.UnitId,
                        childPi.BaseUnitId);
                    if (!converted.IsSuccess)
                    {
                        return ServiceResult<List<InputPlanLine>>.Failure(
                            converted.Message ?? "Lỗi quy đổi đơn vị BTP con.",
                            errorCode: ProductionRunExecutionFailureCodes.ConversionFailed);
                    }

                    normalized.Add((null, childPi.PreparedItemId, converted.Data, d.RecipeDetailId, childPi.Name));
                }
            }

            // Resolve inventories and aggregate by StoreInventoryId
            var planMap = new Dictionary<int, InputPlanLine>();

            foreach (var group in normalized.GroupBy(x => new { x.IngredientId, x.PreparedItemId }))
            {
                StoreInventoryWriteResolution resolution;
                if (group.Key.IngredientId.HasValue)
                {
                    resolution = await _writeResolver.ResolveAsync(new StoreInventoryWriteRequest
                    {
                        ModeSnapshot = snapshot,
                        StoreId = run.StoreId,
                        IdentityType = InventoryWriteIdentityTypes.Ingredient,
                        IngredientId = group.Key.IngredientId,
                        AllowCreateIntent = false
                    });
                }
                else
                {
                    resolution = await _writeResolver.ResolveAsync(new StoreInventoryWriteRequest
                    {
                        ModeSnapshot = snapshot,
                        StoreId = run.StoreId,
                        IdentityType = InventoryWriteIdentityTypes.PreparedItem,
                        PreparedItemId = group.Key.PreparedItemId,
                        AllowCreateIntent = false
                    });
                }

                if (resolution.Status == InventoryWriteResolutionStatuses.NotFound
                    || resolution.StoreInventory == null)
                {
                    return ServiceResult<List<InputPlanLine>>.Failure(
                        resolution.Message ?? "Không tìm thấy tồn kho đầu vào.",
                        errorCode: ProductionRunExecutionFailureCodes.MissingInputInventory);
                }

                if (resolution.Status != InventoryWriteResolutionStatuses.FoundCanonical)
                {
                    return ServiceResult<List<InputPlanLine>>.Failure(
                        resolution.Message,
                        errorCode: ProductionRunExecutionFailureCodes.InventoryResolutionFailed);
                }

                var inv = resolution.StoreInventory;
                // Re-load tracked entity for mutation
                var tracked = await _context.StoreInventories
                    .SingleAsync(x => x.StoreInventoryId == inv.StoreInventoryId);

                var required = group.Sum(x => x.Qty);
                if (!planMap.TryGetValue(tracked.StoreInventoryId, out var line))
                {
                    line = new InputPlanLine
                    {
                        StoreInventoryId = tracked.StoreInventoryId,
                        Inventory = tracked,
                        RequiredQty = 0
                    };
                    planMap[tracked.StoreInventoryId] = line;
                }

                line.RequiredQty += required;
                line.SourceDetailIds.AddRange(group.Select(x => x.DetailId));
            }

            return ServiceResult<List<InputPlanLine>>.Success(planMap.Values.OrderBy(x => x.StoreInventoryId).ToList());
        }

        private async Task<ServiceResult<StoreInventory>> CreateCanonicalOutputRowAsync(
            ProductionRun run,
            int preparedItemId,
            int actorAccountId)
        {
            // Key-range style: re-query under same transaction before insert
            var existing = await _context.StoreInventories
                .Where(x =>
                    x.StoreId == run.StoreId
                    && x.PreparedItemId == preparedItemId
                    && x.BtpIdentityState == BtpIdentityState.Canonical)
                .OrderBy(x => x.StoreInventoryId)
                .ToListAsync();

            if (existing.Count > 1)
            {
                return ServiceResult<StoreInventory>.Failure(
                    "Collision: nhiều canonical row cho PreparedItem.",
                    errorCode: ProductionRunExecutionFailureCodes.InventoryResolutionFailed);
            }

            if (existing.Count == 1)
                return ServiceResult<StoreInventory>.Success(existing[0]);

            var now = DateTime.UtcNow;
            var row = new StoreInventory
            {
                StoreId = run.StoreId,
                IngredientId = null,
                RecipeId = null,
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
            try
            {
                await _context.SaveChangesAsync();
                return ServiceResult<StoreInventory>.Success(row);
            }
            catch (DbUpdateException)
            {
                // Unique canonical race — signal full execution restart
                return ServiceResult<StoreInventory>.Failure(
                    "Canonical create conflict.",
                    errorCode: ProductionRunExecutionFailureCodes.ConcurrencyConflict);
            }
        }

        private async Task<ProductionRunExecutionResultDto> BuildResultFromCompletedAsync(
            ProductionRun run,
            bool wasReplay)
        {
            var movements = await _context.InventoryTransactions
                .AsNoTracking()
                .Where(t => t.ProductionRunId == run.ProductionRunId)
                .OrderBy(t => t.InventoryTransactionId)
                .Select(t => new ProductionRunMovementSummaryDto
                {
                    InventoryTransactionId = t.InventoryTransactionId,
                    StoreInventoryId = t.StoreInventoryId,
                    Type = t.Type.ToString(),
                    Quantity = t.Quantity,
                    BeforeQty = t.BeforeQty,
                    AfterQty = t.AfterQty
                })
                .ToListAsync();

            decimal? outQty = null;
            int? outInvId = null;
            var productionIn = movements.FirstOrDefault(m => m.Type == nameof(InventoryTransactionTypeEnum.PRODUCTION_IN));
            if (productionIn != null)
            {
                outQty = productionIn.Quantity;
                outInvId = productionIn.StoreInventoryId;
            }

            return new ProductionRunExecutionResultDto
            {
                ProductionRunId = run.ProductionRunId,
                StoreId = run.StoreId,
                RecipeId = run.RecipeId,
                RequestedRunCount = run.RequestedRunCount,
                Status = "COMPLETED",
                WasReplay = wasReplay,
                StockApplied = true,
                CompletedAt = run.CompletedAt,
                NormalizedOutputQuantity = outQty,
                OutputStoreInventoryId = outInvId,
                MessageKey = wasReplay ? "ProductionRun.ExecuteReplay" : "ProductionRun.Executed",
                Movements = movements
            };
        }

        private static bool IsUniqueViolation(DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return msg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                   || msg.Contains("2601")
                   || msg.Contains("2627");
        }

        private static ServiceResult<ProductionRunExecutionResultDto> Fail(string code, string message)
            => ServiceResult<ProductionRunExecutionResultDto>.Failure(message, errorCode: code);
    }
}
