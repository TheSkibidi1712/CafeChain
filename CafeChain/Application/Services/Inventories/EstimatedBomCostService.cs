using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Costing;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Package-normalized EstimatedBomCost (Issue #117 / ADR-0005, ADR-0006).
    /// </summary>
    public class EstimatedBomCostService : IEstimatedBomCostService
    {
        private const int MaxBomDepth = 5;

        private readonly AppDbContext _context;
        private readonly IUnitConversionService _unitConversion;
        private readonly IPhysicalUnitConversionService _physical;
        private readonly IRecipeOutputNormalizer _outputNormalizer;
        private readonly ILogger<EstimatedBomCostService> _logger;

        public EstimatedBomCostService(
            AppDbContext context,
            IUnitConversionService unitConversion,
            IPhysicalUnitConversionService physical,
            IRecipeOutputNormalizer outputNormalizer,
            ILogger<EstimatedBomCostService> logger)
        {
            _context = context;
            _unitConversion = unitConversion;
            _physical = physical;
            _outputNormalizer = outputNormalizer;
            _logger = logger;
        }

        public async Task<IngredientBaseUnitCostResult> ResolveIngredientBaseUnitCostAsync(int ingredientId)
        {
            if (ingredientId <= 0)
            {
                return IncompleteIngredient(ingredientId, CostIssueCodes.MissingSupplierOffer,
                    "Mã nguyên liệu không hợp lệ.");
            }

            var ingredient = await _context.Ingredients
                .AsNoTracking()
                .Include(i => i.BaseUnit)
                .FirstOrDefaultAsync(i => i.IngredientId == ingredientId);

            if (ingredient == null)
            {
                return IncompleteIngredient(ingredientId, CostIssueCodes.MissingSupplierOffer,
                    $"Không tìm thấy nguyên liệu #{ingredientId}.");
            }

            var offers = await _context.IngredientSuppliers
                .AsNoTracking()
                .Include(s => s.Unit)
                .Where(s => s.IngredientId == ingredientId && s.Active)
                .ToListAsync();

            var primaries = offers.Where(s => s.IsPrimary).ToList();
            if (primaries.Count > 1)
            {
                return IncompleteIngredient(ingredientId, CostIssueCodes.MultiplePrimarySuppliers,
                    $"Nguyên liệu #{ingredientId} có nhiều NCC chính Active — không thể ước tính giá vốn.",
                    ingredient.BaseUnit);
            }

            if (primaries.Count == 0)
            {
                return IncompleteIngredient(ingredientId, CostIssueCodes.MissingSupplierOffer,
                    $"Nguyên liệu #{ingredientId} không có NCC chính Active để ước tính giá vốn.",
                    ingredient.BaseUnit);
            }

            return await BuildBaseUnitCostFromOfferAsync(ingredient, primaries[0]);
        }

        public async Task<CostCalculationResult> CalculateRecipeEstimatedCostAsync(int recipeId)
        {
            var memo = new Dictionary<int, CostCalculationResult>();
            var path = new HashSet<int>();
            return await CalculateRecipeInternalAsync(recipeId, path, depth: 0, memo);
        }

        private async Task<CostCalculationResult> CalculateRecipeInternalAsync(
            int recipeId,
            HashSet<int> path,
            int depth,
            Dictionary<int, CostCalculationResult> memo)
        {
            if (depth > MaxBomDepth)
            {
                return CostCalculationResult.Incomplete(
                    Array.Empty<CostLineResult>(),
                    new[]
                    {
                        Issue(CostIssueCodes.MaxDepthExceeded,
                            $"Cây BOM vượt quá {MaxBomDepth} tầng khi tính giá vốn ước tính (Recipe #{recipeId}).",
                            recipeId: recipeId)
                    });
            }

            if (!path.Add(recipeId))
            {
                return CostCalculationResult.Incomplete(
                    Array.Empty<CostLineResult>(),
                    new[]
                    {
                        Issue(CostIssueCodes.RecipeCycle,
                            $"Phát hiện vòng lặp công thức khi tính giá vốn (Recipe #{recipeId}).",
                            recipeId: recipeId)
                    });
            }

            try
            {
                if (memo.TryGetValue(recipeId, out var cached))
                    return cached;

                var recipe = await _context.Recipes
                    .AsNoTracking()
                    .Include(r => r.RecipeDetails)
                        .ThenInclude(d => d.Unit)
                    .Include(r => r.PreparedItem)
                        .ThenInclude(p => p!.BaseUnit)
                    .FirstOrDefaultAsync(r => r.RecipeId == recipeId);

                if (recipe == null)
                {
                    var missing = CostCalculationResult.Incomplete(
                        Array.Empty<CostLineResult>(),
                        new[]
                        {
                            Issue(CostIssueCodes.MissingRecipe,
                                $"Không tìm thấy công thức #{recipeId}.",
                                recipeId: recipeId)
                        });
                    memo[recipeId] = missing;
                    return missing;
                }

                if (recipe.RecipeDetails == null || recipe.RecipeDetails.Count == 0)
                {
                    var empty = CostCalculationResult.Incomplete(
                        Array.Empty<CostLineResult>(),
                        new[]
                        {
                            Issue(CostIssueCodes.MissingRecipeDetails,
                                $"Công thức #{recipeId} không có thành phần BOM.",
                                recipeId: recipeId)
                        });
                    memo[recipeId] = empty;
                    return empty;
                }

                var lines = new List<CostLineResult>();
                var issues = new List<CostIssue>();
                decimal total = 0m;
                var allComplete = true;

                foreach (var detail in recipe.RecipeDetails)
                {
                    if (detail.IngredientId.HasValue)
                    {
                        var line = await CostIngredientLineAsync(detail, recipeId);
                        lines.Add(line.Line);
                        issues.AddRange(line.Issues);
                        if (line.Line.Status == CostCompletenessStatus.Complete && line.Line.LineCost.HasValue)
                            total += line.Line.LineCost.Value;
                        else
                            allComplete = false;
                    }
                    else if (detail.ChildRecipeId.HasValue)
                    {
                        var line = await CostChildRecipeLineAsync(
                            detail, recipeId, path, depth, memo);
                        lines.Add(line.Line);
                        issues.AddRange(line.Issues);
                        if (line.Line.Status == CostCompletenessStatus.Complete && line.Line.LineCost.HasValue)
                            total += line.Line.LineCost.Value;
                        else
                            allComplete = false;
                    }
                    else
                    {
                        allComplete = false;
                        issues.Add(Issue(CostIssueCodes.MissingRecipeDetails,
                            $"Dòng BOM #{detail.RecipeDetailId} thiếu IngredientId/ChildRecipeId.",
                            recipeId: recipeId,
                            recipeDetailId: detail.RecipeDetailId));
                        lines.Add(new CostLineResult
                        {
                            RecipeDetailId = detail.RecipeDetailId,
                            Quantity = detail.Quantity,
                            UnitId = detail.UnitId,
                            Status = CostCompletenessStatus.Incomplete
                        });
                    }
                }

                // ADR-0006 / #117: never apply YieldPercentage as second cost factor.
                // OutputQuantity (when present) is already net expected output for BTP.

                CostCalculationResult result;
                if (allComplete && issues.Count == 0)
                {
                    result = CostCalculationResult.Complete(total, lines, issues);
                }
                else
                {
                    // Incomplete even if some lines have costs — TotalCost stays null for authority
                    result = CostCalculationResult.Incomplete(lines, issues);
                }

                memo[recipeId] = result;
                return result;
            }
            finally
            {
                path.Remove(recipeId);
            }
        }

        private async Task<(CostLineResult Line, List<CostIssue> Issues)> CostIngredientLineAsync(
            RecipeDetail detail,
            int recipeId)
        {
            var issues = new List<CostIssue>();
            var ingredientId = detail.IngredientId!.Value;

            var cost = await ResolveIngredientBaseUnitCostAsync(ingredientId);
            issues.AddRange(cost.Issues.Select(i => new CostIssue
            {
                Code = i.Code,
                Message = i.Message,
                IngredientId = ingredientId,
                RecipeId = recipeId,
                RecipeDetailId = detail.RecipeDetailId,
                IngredientSupplierId = i.IngredientSupplierId ?? cost.IngredientSupplierId
            }));

            if (!cost.IsComplete)
            {
                return (new CostLineResult
                {
                    RecipeDetailId = detail.RecipeDetailId,
                    ComponentKind = CostComponentKind.Ingredient,
                    IngredientId = ingredientId,
                    Quantity = detail.Quantity,
                    UnitId = detail.UnitId,
                    UnitCode = detail.Unit?.UnitCode,
                    BaseUnitCode = cost.BaseUnitCode,
                    PackagePrice = cost.PackagePrice,
                    PackageQuantity = cost.PackageQuantity,
                    PackageUnitCode = cost.PackageUnitCode,
                    IngredientSupplierId = cost.IngredientSupplierId,
                    Status = CostCompletenessStatus.Incomplete,
                    DisplaySummary = "Chưa đủ dữ liệu giá gói / NCC"
                }, issues);
            }

            var convert = await _unitConversion.ConvertAsync(
                ingredientId, detail.Quantity, detail.UnitId, cost.BaseUnitId);

            if (!convert.IsSuccess)
            {
                issues.Add(Issue(CostIssueCodes.MissingUnitConversion,
                    convert.Message ?? $"Thiếu quy đổi đơn vị cho nguyên liệu #{ingredientId}.",
                    ingredientId: ingredientId,
                    recipeId: recipeId,
                    recipeDetailId: detail.RecipeDetailId));

                return (new CostLineResult
                {
                    RecipeDetailId = detail.RecipeDetailId,
                    ComponentKind = CostComponentKind.Ingredient,
                    IngredientId = ingredientId,
                    Quantity = detail.Quantity,
                    UnitId = detail.UnitId,
                    BaseUnitCost = cost.BaseUnitCost,
                    PackagePrice = cost.PackagePrice,
                    PackageQuantity = cost.PackageQuantity,
                    PackageUnitCode = cost.PackageUnitCode,
                    IngredientSupplierId = cost.IngredientSupplierId,
                    Status = CostCompletenessStatus.Incomplete,
                    DisplaySummary = "Thiếu quy đổi đơn vị định lượng"
                }, issues);
            }

            var qtyBase = convert.Data;
            var lineCost = qtyBase * cost.BaseUnitCost!.Value;
            var packageLabel = cost.PackageQuantity.HasValue && !string.IsNullOrEmpty(cost.PackageUnitCode)
                ? $"{cost.PackagePrice:N0} ₫ / gói {cost.PackageQuantity:0.####} {cost.PackageUnitCode}"
                : $"{cost.PackagePrice:N0} ₫ / gói";

            return (new CostLineResult
            {
                RecipeDetailId = detail.RecipeDetailId,
                ComponentKind = CostComponentKind.Ingredient,
                IngredientId = ingredientId,
                Quantity = detail.Quantity,
                UnitId = detail.UnitId,
                UnitCode = detail.Unit?.UnitCode,
                QuantityInBase = qtyBase,
                BaseUnitCode = cost.BaseUnitCode,
                BaseUnitCost = cost.BaseUnitCost,
                LineCost = lineCost,
                PackagePrice = cost.PackagePrice,
                PackageQuantity = cost.PackageQuantity,
                PackageUnitCode = cost.PackageUnitCode,
                IngredientSupplierId = cost.IngredientSupplierId,
                Status = CostCompletenessStatus.Complete,
                DisplaySummary =
                    $"{packageLabel} = {cost.BaseUnitCost:N0} ₫/{cost.BaseUnitCode}; " +
                    $"{qtyBase:0.####} {cost.BaseUnitCode} = {lineCost:N0} ₫"
            }, issues);
        }

        private async Task<(CostLineResult Line, List<CostIssue> Issues)> CostChildRecipeLineAsync(
            RecipeDetail detail,
            int parentRecipeId,
            HashSet<int> path,
            int depth,
            Dictionary<int, CostCalculationResult> memo)
        {
            var issues = new List<CostIssue>();
            var childId = detail.ChildRecipeId!.Value;

            var childRecipe = await _context.Recipes
                .AsNoTracking()
                .Include(r => r.PreparedItem)
                    .ThenInclude(p => p!.BaseUnit)
                .FirstOrDefaultAsync(r => r.RecipeId == childId);

            if (childRecipe == null)
            {
                issues.Add(Issue(CostIssueCodes.MissingChildRecipe,
                    $"Không tìm thấy công thức con #{childId}.",
                    recipeId: parentRecipeId,
                    recipeDetailId: detail.RecipeDetailId));
                return (IncompleteChildLine(detail, childId, null), issues);
            }

            // BTP output contract required for authoritative child cost allocation
            if (!childRecipe.PreparedItemId.HasValue
                || !childRecipe.OutputQuantity.HasValue
                || childRecipe.OutputQuantity.Value <= 0
                || !childRecipe.OutputUnitId.HasValue)
            {
                issues.Add(Issue(CostIssueCodes.LegacyChildRecipeWithoutOutput,
                    $"Công thức con #{childId} chưa có PreparedItem/sản lượng đầu ra — không ước tính giá BTP (batch count bị cấm).",
                    recipeId: childId,
                    preparedItemId: childRecipe.PreparedItemId,
                    recipeDetailId: detail.RecipeDetailId));
                return (IncompleteChildLine(detail, childId, childRecipe.PreparedItemId), issues);
            }

            var childCost = await CalculateRecipeInternalAsync(childId, path, depth + 1, memo);
            issues.AddRange(childCost.Issues);

            if (!childCost.IsComplete || !childCost.TotalCost.HasValue)
            {
                return (IncompleteChildLine(detail, childId, childRecipe.PreparedItemId,
                    "Giá vốn đầu vào của BTP chưa đủ dữ liệu"), issues);
            }

            var norm = await _outputNormalizer.NormalizeAsync(
                childRecipe.PreparedItemId.Value,
                childRecipe.OutputQuantity.Value,
                childRecipe.OutputUnitId.Value);

            if (!norm.IsSuccess || norm.Data.NormalizedQuantityInBase <= 0)
            {
                issues.Add(Issue(CostIssueCodes.InvalidRecipeOutput,
                    norm.Message ?? $"Sản lượng đầu ra không hợp lệ cho Recipe #{childId}.",
                    recipeId: childId,
                    preparedItemId: childRecipe.PreparedItemId,
                    recipeDetailId: detail.RecipeDetailId));
                return (IncompleteChildLine(detail, childId, childRecipe.PreparedItemId), issues);
            }

            var outputBase = norm.Data.NormalizedQuantityInBase;
            var costPerBase = childCost.TotalCost.Value / outputBase;

            // Parent consumption → PreparedItem base unit (physical)
            var consume = await _physical.ConvertAsync(
                detail.Quantity,
                detail.UnitId,
                norm.Data.BaseUnitId);

            if (!consume.IsSuccess)
            {
                issues.Add(Issue(CostIssueCodes.MissingUnitConversion,
                    consume.Message
                    ?? $"Không quy đổi định lượng tiêu thụ BTP về {norm.Data.BaseUnitCode}.",
                    recipeId: parentRecipeId,
                    preparedItemId: childRecipe.PreparedItemId,
                    recipeDetailId: detail.RecipeDetailId));
                return (IncompleteChildLine(detail, childId, childRecipe.PreparedItemId,
                    "Thiếu quy đổi đơn vị tiêu thụ BTP"), issues);
            }

            var consumptionBase = consume.Data;
            var lineCost = consumptionBase * costPerBase;

            return (new CostLineResult
            {
                RecipeDetailId = detail.RecipeDetailId,
                ComponentKind = CostComponentKind.ChildRecipe,
                ChildRecipeId = childId,
                PreparedItemId = childRecipe.PreparedItemId,
                Quantity = detail.Quantity,
                UnitId = detail.UnitId,
                UnitCode = detail.Unit?.UnitCode,
                QuantityInBase = consumptionBase,
                BaseUnitCode = norm.Data.BaseUnitCode,
                BaseUnitCost = costPerBase,
                LineCost = lineCost,
                Status = CostCompletenessStatus.Complete,
                DisplaySummary =
                    $"BTP #{childId}: {costPerBase:N4} ₫/{norm.Data.BaseUnitCode}; " +
                    $"{consumptionBase:0.####} {norm.Data.BaseUnitCode} = {lineCost:N0} ₫"
            }, issues);
        }

        private async Task<IngredientBaseUnitCostResult> BuildBaseUnitCostFromOfferAsync(
            Ingredient ingredient,
            IngredientSupplier offer)
        {
            var issues = new List<CostIssue>();

            if (!offer.Active)
            {
                issues.Add(Issue(CostIssueCodes.InactiveSupplierOffer,
                    $"Offer NCC #{offer.IngredientSupplierId} không Active.",
                    ingredientId: ingredient.IngredientId,
                    supplierId: offer.IngredientSupplierId));
                return IncompleteFrom(ingredient, offer, issues);
            }

            if (!offer.PackageQuantity.HasValue)
            {
                issues.Add(Issue(CostIssueCodes.MissingPackageQuantity,
                    $"Offer #{offer.IngredientSupplierId} thiếu PackageQuantity.",
                    ingredientId: ingredient.IngredientId,
                    supplierId: offer.IngredientSupplierId));
                return IncompleteFrom(ingredient, offer, issues);
            }

            if (offer.PackageQuantity.Value <= 0)
            {
                issues.Add(Issue(CostIssueCodes.InvalidPackageQuantity,
                    $"PackageQuantity phải > 0 (offer #{offer.IngredientSupplierId}).",
                    ingredientId: ingredient.IngredientId,
                    supplierId: offer.IngredientSupplierId));
                return IncompleteFrom(ingredient, offer, issues);
            }

            // CurrentPrice is non-nullable decimal; zero is incomplete (not complete free stock)
            if (offer.CurrentPrice <= 0)
            {
                issues.Add(Issue(CostIssueCodes.ZeroPackagePrice,
                    $"Giá gói = 0 — không ghi nhận giá vốn ước tính đầy đủ (offer #{offer.IngredientSupplierId}).",
                    ingredientId: ingredient.IngredientId,
                    supplierId: offer.IngredientSupplierId));
                return IncompleteFrom(ingredient, offer, issues);
            }

            var unit = offer.Unit ?? await _context.Units.AsNoTracking()
                .FirstOrDefaultAsync(u => u.UnitId == offer.UnitId);

            if (unit == null)
            {
                issues.Add(Issue(CostIssueCodes.MissingPackageUnit,
                    $"Đơn vị gói #{offer.UnitId} không tồn tại.",
                    ingredientId: ingredient.IngredientId,
                    supplierId: offer.IngredientSupplierId));
                return IncompleteFrom(ingredient, offer, issues);
            }

            if (!unit.Active)
            {
                issues.Add(Issue(CostIssueCodes.InactivePackageUnit,
                    $"Đơn vị gói '{unit.UnitCode}' không còn hiệu lực.",
                    ingredientId: ingredient.IngredientId,
                    supplierId: offer.IngredientSupplierId));
                return IncompleteFrom(ingredient, offer, issues);
            }

            if (PackageUnitCodes.IsRejectedCommercialPackaging(unit.UnitCode))
            {
                issues.Add(Issue(CostIssueCodes.RejectedPackagingUnit,
                    $"Đơn vị đóng gói thương mại '{unit.UnitCode}' không dùng làm đơn vị nội dung gói cho giá vốn.",
                    ingredientId: ingredient.IngredientId,
                    supplierId: offer.IngredientSupplierId));
                return IncompleteFrom(ingredient, offer, issues);
            }

            // Prefer ingredient-context conversion (physical-first via #110)
            var convert = await _unitConversion.ConvertAsync(
                ingredient.IngredientId,
                offer.PackageQuantity.Value,
                offer.UnitId,
                ingredient.BaseUnitId);

            if (!convert.IsSuccess || convert.Data <= 0)
            {
                // Try pure physical as secondary message path
                var phys = await _physical.ConvertAsync(
                    offer.PackageQuantity.Value,
                    offer.UnitId,
                    ingredient.BaseUnitId);

                issues.Add(Issue(CostIssueCodes.MissingUnitConversion,
                    convert.Message ?? phys.Message
                    ?? $"Không quy đổi PackageQuantity sang đơn vị tồn kho của nguyên liệu #{ingredient.IngredientId}.",
                    ingredientId: ingredient.IngredientId,
                    supplierId: offer.IngredientSupplierId));
                return IncompleteFrom(ingredient, offer, issues);
            }

            var baseQtyPerPackage = convert.Data;
            var baseUnitCost = offer.CurrentPrice / baseQtyPerPackage;

            return new IngredientBaseUnitCostResult
            {
                Status = CostCompletenessStatus.Complete,
                IngredientId = ingredient.IngredientId,
                BaseUnitCost = baseUnitCost,
                BaseQuantityPerPackage = baseQtyPerPackage,
                BaseUnitId = ingredient.BaseUnitId,
                BaseUnitCode = ingredient.BaseUnit?.UnitCode,
                BaseUnitName = ingredient.BaseUnit?.Name,
                PackagePrice = offer.CurrentPrice,
                PackageQuantity = offer.PackageQuantity,
                PackageUnitId = offer.UnitId,
                PackageUnitCode = unit.UnitCode,
                IngredientSupplierId = offer.IngredientSupplierId,
                Issues = issues
            };
        }

        private static CostLineResult IncompleteChildLine(
            RecipeDetail detail,
            int childId,
            int? preparedItemId,
            string? summary = null)
            => new()
            {
                RecipeDetailId = detail.RecipeDetailId,
                ComponentKind = CostComponentKind.ChildRecipe,
                ChildRecipeId = childId,
                PreparedItemId = preparedItemId,
                Quantity = detail.Quantity,
                UnitId = detail.UnitId,
                UnitCode = detail.Unit?.UnitCode,
                Status = CostCompletenessStatus.Incomplete,
                DisplaySummary = summary ?? "Chưa đủ dữ liệu giá BTP"
            };

        private static IngredientBaseUnitCostResult IncompleteIngredient(
            int ingredientId,
            string code,
            string message,
            Unit? baseUnit = null)
            => new()
            {
                Status = CostCompletenessStatus.Incomplete,
                IngredientId = ingredientId,
                BaseUnitId = baseUnit?.UnitId,
                BaseUnitCode = baseUnit?.UnitCode,
                BaseUnitName = baseUnit?.Name,
                Issues = new[] { Issue(code, message, ingredientId: ingredientId) }
            };

        private static IngredientBaseUnitCostResult IncompleteFrom(
            Ingredient ingredient,
            IngredientSupplier offer,
            List<CostIssue> issues)
            => new()
            {
                Status = CostCompletenessStatus.Incomplete,
                IngredientId = ingredient.IngredientId,
                BaseUnitId = ingredient.BaseUnitId,
                BaseUnitCode = ingredient.BaseUnit?.UnitCode,
                BaseUnitName = ingredient.BaseUnit?.Name,
                PackagePrice = offer.CurrentPrice,
                PackageQuantity = offer.PackageQuantity,
                PackageUnitId = offer.UnitId,
                PackageUnitCode = offer.Unit?.UnitCode,
                IngredientSupplierId = offer.IngredientSupplierId,
                Issues = issues
            };

        private static CostIssue Issue(
            string code,
            string message,
            int? ingredientId = null,
            int? preparedItemId = null,
            int? recipeId = null,
            int? recipeDetailId = null,
            int? supplierId = null)
            => new()
            {
                Code = code,
                Message = message,
                IngredientId = ingredientId,
                PreparedItemId = preparedItemId,
                RecipeId = recipeId,
                RecipeDetailId = recipeDetailId,
                IngredientSupplierId = supplierId
            };
    }
}
