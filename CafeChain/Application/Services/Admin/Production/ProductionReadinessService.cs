using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Admin.Production;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Production
{
    /// <summary>
    /// Read-only readiness projection. Execution remains authoritative and revalidates inside its transaction.
    /// </summary>
    public sealed class ProductionReadinessService : IProductionReadinessService
    {
        private readonly AppDbContext _context;
        private readonly IRecipeOutputNormalizer _outputNormalizer;
        private readonly IUnitConversionService _unitConversion;
        private readonly IPhysicalUnitConversionService _physicalConversion;
        private readonly IInventoryWriterModeService _writerModeService;
        private readonly IEstimatedBomCostService _estimatedBomCost;
        private readonly IEnumerable<IInventoryWriterCapabilityProvider> _capabilityProviders;

        public ProductionReadinessService(
            AppDbContext context,
            IRecipeOutputNormalizer outputNormalizer,
            IUnitConversionService unitConversion,
            IPhysicalUnitConversionService physicalConversion,
            IInventoryWriterModeService writerModeService,
            IEstimatedBomCostService estimatedBomCost,
            IEnumerable<IInventoryWriterCapabilityProvider> capabilityProviders)
        {
            _context = context;
            _outputNormalizer = outputNormalizer;
            _unitConversion = unitConversion;
            _physicalConversion = physicalConversion;
            _writerModeService = writerModeService;
            _estimatedBomCost = estimatedBomCost;
            _capabilityProviders = capabilityProviders;
        }

        public async Task<IReadOnlyList<ProductionRecipeOptionDto>> GetRecipeOptionsAsync()
        {
            var recipes = await _context.Recipes
                .AsNoTracking()
                .Include(r => r.PreparedItem)
                    .ThenInclude(p => p!.BaseUnit)
                .Include(r => r.OutputUnit)
                .Where(r => r.Active
                    && r.Status == "Active"
                    && !r.DrinkId.HasValue
                    && !r.ToppingId.HasValue)
                .OrderBy(r => r.PreparedItem != null ? r.PreparedItem.Name : r.Name)
                .ThenByDescending(r => r.EffectiveDate)
                .ThenByDescending(r => r.RecipeId)
                .ToListAsync();

            var options = new List<ProductionRecipeOptionDto>(recipes.Count);
            foreach (var recipe in recipes)
            {
                var option = new ProductionRecipeOptionDto
                {
                    RecipeId = recipe.RecipeId,
                    RecipeCode = recipe.RecipeCode ?? "",
                    RecipeName = recipe.Name ?? "",
                    PreparedItemId = recipe.PreparedItemId,
                    PreparedItemCode = recipe.PreparedItem?.Code,
                    PreparedItemName = recipe.PreparedItem?.Name,
                    OutputPerRunDisplay = recipe.OutputQuantity.HasValue
                        ? $"{recipe.OutputQuantity.Value:0.####} {recipe.OutputUnit?.UnitCode ?? recipe.OutputUnit?.Name ?? ""}".Trim()
                        : "—"
                };

                if (!recipe.PreparedItemId.HasValue)
                {
                    option.DisabledReason = "Công thức BTP chưa map PreparedItem.";
                }
                else if (!recipe.OutputQuantity.HasValue
                    || recipe.OutputQuantity.Value <= 0
                    || !recipe.OutputUnitId.HasValue)
                {
                    option.DisabledReason = "Thiếu output contract hợp lệ.";
                }
                else
                {
                    var normalized = await _outputNormalizer.NormalizeAsync(
                        recipe.PreparedItemId.Value,
                        recipe.OutputQuantity.Value,
                        recipe.OutputUnitId.Value);
                    option.Selectable = normalized.IsSuccess;
                    if (!normalized.IsSuccess)
                        option.DisabledReason = normalized.Message;
                }

                options.Add(option);
            }

            return options;
        }

        public async Task<ServiceResult<ProductionReadinessPreviewDto>> PreviewAsync(
            int storeId,
            int recipeId,
            decimal runCount)
        {
            if (storeId <= 0 || recipeId <= 0 || runCount <= 0 || runCount > 9999)
            {
                return ServiceResult<ProductionReadinessPreviewDto>.Failure(
                    "Cửa hàng, công thức và số mẻ phải hợp lệ.",
                    errorCode: ProductionReadinessCodes.InvalidRecipe);
            }

            var store = await _context.Stores.AsNoTracking()
                .FirstOrDefaultAsync(x => x.StoreId == storeId && x.Active);
            if (store == null)
            {
                return ServiceResult<ProductionReadinessPreviewDto>.Failure(
                    "Không tìm thấy cửa hàng hoạt động.",
                    errorCode: ProductionReadinessCodes.InvalidRecipe);
            }

            var recipe = await _context.Recipes
                .AsNoTracking()
                .AsSplitQuery()
                .Include(r => r.PreparedItem)
                    .ThenInclude(p => p!.BaseUnit)
                .Include(r => r.OutputUnit)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(d => d.Unit)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(d => d.Ingredient)
                        .ThenInclude(i => i!.BaseUnit)
                .Include(r => r.RecipeDetails)
                    .ThenInclude(d => d.ChildRecipe)
                        .ThenInclude(c => c!.PreparedItem)
                            .ThenInclude(p => p!.BaseUnit)
                .FirstOrDefaultAsync(r => r.RecipeId == recipeId);

            if (recipe == null
                || !recipe.Active
                || recipe.Status != "Active"
                || recipe.DrinkId.HasValue
                || recipe.ToppingId.HasValue
                || !recipe.PreparedItemId.HasValue
                || !recipe.OutputQuantity.HasValue
                || !recipe.OutputUnitId.HasValue)
            {
                return ServiceResult<ProductionReadinessPreviewDto>.Failure(
                    "Chỉ công thức BTP Active có PreparedItem và output contract hợp lệ mới được preview.",
                    errorCode: ProductionReadinessCodes.InvalidRecipe);
            }

            var output = await _outputNormalizer.NormalizeAsync(
                recipe.PreparedItemId.Value,
                recipe.OutputQuantity.Value,
                recipe.OutputUnitId.Value);
            if (!output.IsSuccess || output.Data == null)
            {
                return ServiceResult<ProductionReadinessPreviewDto>.Failure(
                    output.Message ?? "Không chuẩn hóa được sản lượng đầu ra.",
                    errorCode: ProductionReadinessCodes.InvalidOutput);
            }

            var preview = new ProductionReadinessPreviewDto
            {
                StoreId = store.StoreId,
                StoreName = store.Name ?? $"Cửa hàng #{store.StoreId}",
                RecipeId = recipe.RecipeId,
                RecipeCode = recipe.RecipeCode ?? "",
                RecipeName = recipe.Name ?? "",
                PreparedItemId = output.Data.PreparedItemId,
                PreparedItemCode = output.Data.PreparedItemCode,
                PreparedItemName = output.Data.PreparedItemName,
                RunCount = runCount,
                OutputQuantityPerRun = recipe.OutputQuantity.Value,
                OutputUnitCode = output.Data.OutputUnitCode,
                RawTotalOutput = recipe.OutputQuantity.Value * runCount,
                NormalizedOutputPerRun = output.Data.NormalizedQuantityInBase,
                NormalizedTotalOutput = output.Data.NormalizedQuantityInBase * runCount,
                OutputBaseUnitCode = output.Data.BaseUnitCode
            };

            await AddWriterReadinessAsync(preview);

            var normalizedInputs = await NormalizeInputsAsync(recipe, runCount, preview.Reasons);
            if (normalizedInputs.Count == 0)
            {
                AddReason(preview, ProductionReadinessCodes.InvalidOutput, "Công thức chưa có input BOM hợp lệ.");
                FinalizePreview(preview);
                return ServiceResult<ProductionReadinessPreviewDto>.Success(preview);
            }

            await PopulateInventoryAndCostEvidenceAsync(preview, normalizedInputs);

            var estimated = await _estimatedBomCost.CalculateRecipeEstimatedCostAsync(recipe.RecipeId);
            preview.EstimatedBomCostComplete = estimated.IsComplete;
            if (estimated.IsComplete)
            {
                preview.EstimatedBomCostPerRun = estimated.TotalCost;
                preview.EstimatedBomCostTotal = estimated.TotalCost * runCount;
            }
            else
            {
                AddReason(
                    preview,
                    ProductionReadinessCodes.EstimatedCostIncomplete,
                    "Giá vốn BOM ước tính chưa đầy đủ; không dùng 0 làm giá thay thế.",
                    blocking: false);
            }

            FinalizePreview(preview);
            return ServiceResult<ProductionReadinessPreviewDto>.Success(preview);
        }

        private async Task AddWriterReadinessAsync(ProductionReadinessPreviewDto preview)
        {
            var mode = await _writerModeService.GetStatusAsync(preview.StoreId);
            if (!mode.IsSuccess || mode.Data == null)
            {
                preview.WriterMode = "Chưa cấu hình";
                AddReason(preview, ProductionReadinessCodes.WriterMode, mode.Message);
            }
            else
            {
                preview.WriterMode = mode.Data.WriterMode.ToString();
                if (mode.Data.WriterMode != InventoryWriterMode.PreparedItem)
                {
                    AddReason(
                        preview,
                        ProductionReadinessCodes.WriterMode,
                        $"WriterMode hiện tại là {mode.Data.WriterMode}; production yêu cầu PreparedItem.");
                }
            }

            var capability = _capabilityProviders
                .Select(x => x.GetStatus())
                .FirstOrDefault(x => x.CapabilityId == InventoryWriterCapabilityIds.ProductionPreparedWriter);
            preview.WriterCapabilityReady = capability?.Ready == true;
            if (!preview.WriterCapabilityReady)
            {
                AddReason(
                    preview,
                    ProductionReadinessCodes.WriterCapability,
                    capability?.BlockerMessage ?? "PRODUCTION_PREPARED_WRITER chưa sẵn sàng.");
            }
        }

        private async Task<List<NormalizedInput>> NormalizeInputsAsync(
            Models.Drinks.Recipe recipe,
            decimal runCount,
            List<ProductionReadinessReasonDto> reasons)
        {
            var normalized = new List<NormalizedInput>();
            foreach (var detail in recipe.RecipeDetails.OrderBy(x => x.RecipeDetailId))
            {
                var hasIngredient = detail.IngredientId.HasValue;
                var hasChild = detail.ChildRecipeId.HasValue;
                if (hasIngredient == hasChild || detail.Quantity <= 0)
                {
                    reasons.Add(new ProductionReadinessReasonDto
                    {
                        Code = ProductionReadinessCodes.InvalidRecipe,
                        Message = $"RecipeDetail #{detail.RecipeDetailId} phải có đúng một nguồn và quantity > 0."
                    });
                    continue;
                }

                var rawTotal = detail.Quantity * runCount;
                if (hasIngredient)
                {
                    var ingredient = detail.Ingredient;
                    if (ingredient == null || !ingredient.Active)
                    {
                        reasons.Add(new ProductionReadinessReasonDto
                        {
                            Code = ProductionReadinessCodes.InvalidRecipe,
                            Message = $"Nguyên liệu #{detail.IngredientId} không tồn tại hoặc ngừng hoạt động."
                        });
                        continue;
                    }

                    var converted = await _unitConversion.ConvertAsync(
                        ingredient.IngredientId,
                        rawTotal,
                        detail.UnitId,
                        ingredient.BaseUnitId);
                    if (!converted.IsSuccess)
                    {
                        reasons.Add(new ProductionReadinessReasonDto
                        {
                            Code = ProductionReadinessCodes.ConversionFailed,
                            Message = converted.Message ?? $"Không quy đổi được {ingredient.Code}."
                        });
                        continue;
                    }

                    normalized.Add(new NormalizedInput
                    {
                        IngredientId = ingredient.IngredientId,
                        ItemCode = ingredient.Code,
                        ItemName = ingredient.Name,
                        RequiredTotal = converted.Data,
                        RequiredPerRun = converted.Data / runCount,
                        BaseUnitCode = ingredient.BaseUnit?.UnitCode ?? ""
                    });
                    continue;
                }

                var child = detail.ChildRecipe;
                var preparedItem = child?.PreparedItem;
                if (child == null || !child.PreparedItemId.HasValue || preparedItem == null || !preparedItem.Active)
                {
                    reasons.Add(new ProductionReadinessReasonDto
                    {
                        Code = ProductionReadinessCodes.InvalidRecipe,
                        Message = $"ChildRecipe #{detail.ChildRecipeId} chưa pin tới PreparedItem hoạt động."
                    });
                    continue;
                }

                var physical = await _physicalConversion.ConvertAsync(
                    rawTotal,
                    detail.UnitId,
                    preparedItem.BaseUnitId);
                if (!physical.IsSuccess)
                {
                    reasons.Add(new ProductionReadinessReasonDto
                    {
                        Code = ProductionReadinessCodes.ConversionFailed,
                        Message = physical.Message ?? $"Không quy đổi được {preparedItem.Code}."
                    });
                    continue;
                }

                normalized.Add(new NormalizedInput
                {
                    PreparedItemId = preparedItem.PreparedItemId,
                    ChildRecipeId = child.RecipeId,
                    ItemCode = preparedItem.Code,
                    ItemName = preparedItem.Name,
                    RequiredTotal = physical.Data,
                    RequiredPerRun = physical.Data / runCount,
                    BaseUnitCode = preparedItem.BaseUnit?.UnitCode ?? ""
                });
            }

            return normalized
                .GroupBy(x => new { x.IngredientId, x.PreparedItemId })
                .Select(group => new NormalizedInput
                {
                    IngredientId = group.Key.IngredientId,
                    PreparedItemId = group.Key.PreparedItemId,
                    ChildRecipeId = group.Select(x => x.ChildRecipeId).FirstOrDefault(x => x.HasValue),
                    ItemCode = group.First().ItemCode,
                    ItemName = group.First().ItemName,
                    BaseUnitCode = group.First().BaseUnitCode,
                    RequiredPerRun = group.Sum(x => x.RequiredPerRun),
                    RequiredTotal = group.Sum(x => x.RequiredTotal)
                })
                .OrderBy(x => x.PreparedItemId.HasValue)
                .ThenBy(x => x.ItemName)
                .ToList();
        }

        private async Task PopulateInventoryAndCostEvidenceAsync(
            ProductionReadinessPreviewDto preview,
            IReadOnlyList<NormalizedInput> normalizedInputs)
        {
            var ingredientIds = normalizedInputs
                .Where(x => x.IngredientId.HasValue)
                .Select(x => x.IngredientId!.Value)
                .Distinct()
                .ToList();
            var preparedItemIds = normalizedInputs
                .Where(x => x.PreparedItemId.HasValue)
                .Select(x => x.PreparedItemId!.Value)
                .Distinct()
                .ToList();

            var inventories = await _context.StoreInventories
                .AsNoTracking()
                .Where(x => x.StoreId == preview.StoreId
                    && ((x.IngredientId.HasValue && ingredientIds.Contains(x.IngredientId.Value))
                        || (x.PreparedItemId.HasValue && preparedItemIds.Contains(x.PreparedItemId.Value))))
                .OrderBy(x => x.StoreInventoryId)
                .ToListAsync();
            var costLayers = await _context.InventoryCostLayers
                .AsNoTracking()
                .Where(x => x.StoreId == preview.StoreId
                    && x.RemainingQuantity > 0
                    && ((x.IngredientId.HasValue && ingredientIds.Contains(x.IngredientId.Value))
                        || (x.PreparedItemId.HasValue && preparedItemIds.Contains(x.PreparedItemId.Value))))
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.InventoryCostLayerId)
                .ToListAsync();

            var maxRuns = decimal.MaxValue;
            decimal projectedTotal = 0m;
            var allCostComplete = true;

            foreach (var input in normalizedInputs)
            {
                var dto = new ProductionReadinessInputDto
                {
                    SourceType = input.IngredientId.HasValue ? "Nguyên liệu" : "Bán thành phẩm",
                    IngredientId = input.IngredientId,
                    PreparedItemId = input.PreparedItemId,
                    ChildRecipeId = input.ChildRecipeId,
                    ItemCode = input.ItemCode,
                    ItemName = input.ItemName,
                    RequiredPerRun = input.RequiredPerRun,
                    RequiredTotal = input.RequiredTotal,
                    BaseUnitCode = input.BaseUnitCode
                };

                var matchingRows = inventories.Where(x => input.IngredientId.HasValue
                    ? x.IngredientId == input.IngredientId
                    : x.PreparedItemId == input.PreparedItemId).ToList();
                StoreInventory? inventory;
                if (input.IngredientId.HasValue)
                {
                    inventory = matchingRows.FirstOrDefault();
                }
                else
                {
                    var canonical = matchingRows
                        .Where(x => x.BtpIdentityState == BtpIdentityState.Canonical)
                        .ToList();
                    inventory = canonical.Count == 1
                        && !matchingRows.Any(x => x.BtpIdentityState == BtpIdentityState.Legacy)
                        && canonical[0].QuantitySemanticsStatus == InventoryQuantitySemanticsStatus.BaseUnitConfirmed
                            ? canonical[0]
                            : null;
                }

                if (inventory == null)
                {
                    dto.Status = "Chưa có tồn kho canonical tại cửa hàng";
                    dto.ShortageQuantity = input.RequiredTotal;
                    dto.MaxSupportedRunCount = 0;
                    AddReason(
                        preview,
                        ProductionReadinessCodes.MissingInventory,
                        $"{input.ItemCode} chưa có dòng tồn kho hợp lệ tại {preview.StoreName}.");
                }
                else
                {
                    dto.InventoryResolved = true;
                    dto.StoreInventoryId = inventory.StoreInventoryId;
                    dto.CurrentQuantity = inventory.AvailableQty;
                    dto.ReservedQuantity = inventory.ReservedQty;
                    dto.UsableQuantity = inventory.AvailableQty - inventory.ReservedQty;
                    dto.ShortageQuantity = Math.Max(0m, input.RequiredTotal - dto.UsableQuantity);
                    dto.MaxSupportedRunCount = input.RequiredPerRun > 0
                        ? FloorThree(Math.Max(0m, dto.UsableQuantity) / input.RequiredPerRun)
                        : 0m;
                    dto.Status = dto.ShortageQuantity > 0 ? "Thiếu tồn khả dụng" : "Đủ tồn cho số mẻ yêu cầu";

                    if (dto.ShortageQuantity > 0)
                    {
                        AddReason(
                            preview,
                            input.IngredientId.HasValue
                                ? ProductionReadinessCodes.IngredientShortage
                                : ProductionReadinessCodes.PreparedItemShortage,
                            $"{input.ItemCode} thiếu {dto.ShortageQuantity:0.####} {input.BaseUnitCode}.");
                    }
                }

                maxRuns = Math.Min(maxRuns, dto.MaxSupportedRunCount);

                var matchingLayers = costLayers.Where(x => input.IngredientId.HasValue
                    ? x.IngredientId == input.IngredientId && !x.PreparedItemId.HasValue
                    : x.PreparedItemId == input.PreparedItemId && !x.IngredientId.HasValue).ToList();
                dto.CostLayerAvailableQuantity = matchingLayers
                    .Where(x => x.UnitCost > 0)
                    .Sum(x => x.RemainingQuantity);
                dto.CostEvidenceShortage = Math.Max(0m, input.RequiredTotal - dto.CostLayerAvailableQuantity);
                dto.CostEvidenceComplete = dto.CostEvidenceShortage <= 0;
                dto.ProjectedFifoCost = ProjectFifoCost(matchingLayers, input.RequiredTotal);

                if (!dto.CostEvidenceComplete || !dto.ProjectedFifoCost.HasValue)
                {
                    allCostComplete = false;
                    AddReason(
                        preview,
                        ProductionReadinessCodes.MissingCostEvidence,
                        $"{input.ItemCode} thiếu {dto.CostEvidenceShortage:0.####} {input.BaseUnitCode} bằng chứng giá vốn FIFO.");
                }
                else
                {
                    projectedTotal += dto.ProjectedFifoCost.Value;
                }

                preview.Inputs.Add(dto);
            }

            preview.MaxSupportedRunCount = maxRuns == decimal.MaxValue ? 0m : maxRuns;
            preview.CostEvidenceComplete = allCostComplete;
            preview.ProjectedFifoInputCost = allCostComplete ? projectedTotal : null;
        }

        private static decimal? ProjectFifoCost(
            IEnumerable<Models.Inventories.Costing.InventoryCostLayer> layers,
            decimal required)
        {
            var remaining = required;
            decimal total = 0m;
            foreach (var layer in layers.Where(x => x.UnitCost > 0)
                .OrderBy(x => x.CreatedAt)
                .ThenBy(x => x.InventoryCostLayerId))
            {
                if (remaining <= 0)
                    break;
                var take = Math.Min(remaining, layer.RemainingQuantity);
                if (take <= 0)
                    continue;
                total += take * layer.UnitCost;
                remaining -= take;
            }

            return remaining <= 0 ? total : null;
        }

        private static void FinalizePreview(ProductionReadinessPreviewDto preview)
        {
            preview.Reasons = preview.Reasons
                .GroupBy(x => new { x.Code, x.Message, x.Blocking })
                .Select(x => x.First())
                .ToList();
            preview.IsReady = preview.Reasons.All(x => !x.Blocking);
            preview.OverallStatus = preview.IsReady
                ? $"Sẵn sàng cho {preview.RunCount:0.###} mẻ"
                : "Chưa sẵn sàng";
        }

        private static void AddReason(
            ProductionReadinessPreviewDto preview,
            string code,
            string? message,
            bool blocking = true)
        {
            preview.Reasons.Add(new ProductionReadinessReasonDto
            {
                Code = code,
                Message = string.IsNullOrWhiteSpace(message) ? code : message,
                Blocking = blocking
            });
        }

        private static decimal FloorThree(decimal value)
            => Math.Floor(value * 1000m) / 1000m;

        private sealed class NormalizedInput
        {
            public int? IngredientId { get; init; }
            public int? PreparedItemId { get; init; }
            public int? ChildRecipeId { get; init; }
            public string ItemCode { get; init; } = "";
            public string ItemName { get; init; } = "";
            public string BaseUnitCode { get; init; } = "";
            public decimal RequiredPerRun { get; init; }
            public decimal RequiredTotal { get; init; }
        }
    }
}
