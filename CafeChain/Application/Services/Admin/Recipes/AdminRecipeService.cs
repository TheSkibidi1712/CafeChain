using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Results;
using CafeChain.Application.Constants;
using CafeChain.Application.Services.Inventories;
using CafeChain.ViewModels.Admin.Recipes;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Unit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.Admin.Recipes
{
    public class AdminRecipeService : IAdminRecipeService
    {
        private readonly AppDbContext _context;
        private readonly IRecipeOutputNormalizer _outputNormalizer;
        private readonly ILogger<AdminRecipeService> _logger;

        // Giới hạn tối đa 5 tầng BOM để tránh StackOverflow
        private const int MAX_BOM_DEPTH = 5;

        public AdminRecipeService(
            AppDbContext context,
            IRecipeOutputNormalizer outputNormalizer,
            ILogger<AdminRecipeService>? logger = null)
        {
            _context = context;
            _outputNormalizer = outputNormalizer;
            _logger = logger ?? NullLogger<AdminRecipeService>.Instance;
        }

        // ============================================================
        // CREATE: Hardened V4 — Zero-Trust Validation + #112 BTP output
        // ============================================================
        public async Task<ServiceResult> CreateRecipeAsync(RecipeCreateVM model)
        {
            if (model.Details == null || model.Details.Count == 0)
            {
                return ServiceResult.Failure("Công thức phải chứa ít nhất một thành phần (Details trống).");
            }

            var targetValidation = await ValidateRecipeTargetAsync(model, existing: null);
            if (!targetValidation.IsSuccess)
                return targetValidation;

            // === VALIDATION LAYER (Zero-Trust) ===
            var validationResult = ValidateDetails(model.Details);
            if (!validationResult.IsSuccess)
                return validationResult;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate tồn tại trong DB
                var dbValidation = await ValidateDetailsExistInDbAsync(model.Details);
                if (!dbValidation.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return dbValidation;
                }

                // Validate UnitId hợp lệ cho từng Ingredient
                var unitValidation = await ValidateUnitMappingsAsync(model.Details);
                if (!unitValidation.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return unitValidation;
                }

                var childRecipeIds = model.Details
                    .Where(d => d.ItemCode.StartsWith("REC_", StringComparison.Ordinal))
                    .Select(d => int.Parse(d.ItemCode.Substring(4)))
                    .ToList();

                var circularValidation = await ValidateCreateCircularDependencyAsync(model, childRecipeIds);
                if (!circularValidation.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return circularValidation;
                }

                RecipeOutputNormalizationResult? output = null;
                if (IsBtpType(model.RecipeType))
                {
                    var uniqueness = await EnsureNoOtherActiveForPreparedItemAsync(
                        model.PreparedItemId!.Value,
                        excludeRecipeId: null);
                    if (!uniqueness.IsSuccess)
                    {
                        await transaction.RollbackAsync();
                        return uniqueness;
                    }

                    var norm = await _outputNormalizer.NormalizeAsync(
                        model.PreparedItemId.Value,
                        model.ExpectedYield!.Value,
                        model.OutputUnitId!.Value);
                    if (!norm.IsSuccess)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult.Failure(norm.Message, norm.Errors, norm.ErrorCode);
                    }

                    output = norm.Data;
                }

                string recipeName = await ResolveRecipeNameAsync(model, output);

                var recipe = new Recipe
                {
                    RecipeCode = GenerateRecipeCode(model),
                    Name = recipeName,
                    DrinkId = model.RecipeType == "POS" ? model.DrinkId : null,
                    SizeId = model.RecipeType == "POS" ? model.SizeId : null,
                    ToppingId = model.RecipeType == "TOPPING" ? model.ToppingId : null,
                    PreparedItemId = output?.PreparedItemId,
                    OutputQuantity = output?.OutputQuantity,
                    OutputUnitId = output?.OutputUnitId,
                    YieldPercentage = 100,
                    Active = model.Active,
                    Status = model.Active ? "Active" : "Archived",
                    EffectiveDate = model.EffectiveDate,
                    RecipeDetails = new List<RecipeDetail>()
                };

                // Keep Status/Active synchronized for inactive create edge cases
                if (!recipe.Active)
                    recipe.Status = "Archived";
                else
                    recipe.Status = "Active";

                foreach (var detailVM in model.Details)
                {
                    var detail = ParseRecipeDetail(detailVM);
                    recipe.RecipeDetails.Add(detail);
                }

                if (childRecipeIds.Any())
                {
                    foreach (var childId in childRecipeIds)
                    {
                        var depthResult = await CheckDepthLimitAsync(childId, 1);
                        if (!depthResult.IsSuccess)
                        {
                            await transaction.RollbackAsync();
                            return depthResult;
                        }
                    }
                }

                await _context.Recipes.AddAsync(recipe);
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex) when (IsActiveDrinkSizeUniqueViolation(ex))
                {
                    await transaction.RollbackAsync();
                    _logger.LogWarning(
                        ex,
                        "BOM create conflict for DrinkId={DrinkId}, SizeId={SizeId}",
                        model.DrinkId,
                        model.SizeId);
                    return ServiceResult.Failure(
                        "Món và size này đã có công thức đang hoạt động. Hãy tạo phiên bản kế tiếp từ công thức hiện có.",
                        errorCode: BomRecipeErrorCodes.RecipeOverlap);
                }
                catch (DbUpdateException ex) when (IsActivePreparedItemUniqueViolation(ex))
                {
                    await transaction.RollbackAsync();
                    _logger.LogWarning(ex, "BOM create conflict for PreparedItemId={PreparedItemId}", model.PreparedItemId);
                    return ServiceResult.Failure(
                        "Đã có công thức đang hoạt động cho bán thành phẩm này. Hãy tạo phiên bản kế tiếp từ công thức hiện có.",
                        errorCode: BomRecipeErrorCodes.RecipeOverlap);
                }

                await transaction.CommitAsync();
                return ServiceResult.Success("Tạo mới công thức (BOM) thành công!");
            }
            catch (ArgumentException ex)
            {
                await transaction.RollbackAsync();
                return ServiceResult.Failure(
                    ex.Message,
                    errorCode: BomRecipeErrorCodes.InvalidPayload);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Unexpected BOM create failure.");
                return ServiceResult.Failure(
                    "Không thể lưu công thức lúc này. Vui lòng thử lại hoặc liên hệ quản trị viên.",
                    errorCode: BomRecipeErrorCodes.TechnicalError);
            }
        }

        // ============================================================
        // UPDATE: Versioning — Soft-Delete cũ → Insert mới
        // NEVER OVERWRITE active records
        // ============================================================
        public async Task<ServiceResult> UpdateRecipeAsync(int recipeId, RecipeCreateVM model)
        {
            if (model.Details == null || model.Details.Count == 0)
            {
                return ServiceResult.Failure("Công thức phải chứa ít nhất một thành phần.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Load current Active Recipe
                var oldRecipe = await _context.Recipes
                    .Include(r => r.RecipeDetails)
                    .FirstOrDefaultAsync(r => r.RecipeId == recipeId && r.Status == "Active");

                if (oldRecipe == null)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult.Failure("Không tìm thấy công thức hoặc công thức đã bị lưu trữ (Archived).");
                }

                // Legacy SUBRECIPE without PreparedItem: block new Active version until explicit mapping
                bool isLegacyUnmapped =
                    !oldRecipe.DrinkId.HasValue
                    && !oldRecipe.ToppingId.HasValue
                    && !oldRecipe.PreparedItemId.HasValue;

                if (isLegacyUnmapped)
                {
                    // Force BTP mapping path for next Active version
                    model.RecipeType = "SUBRECIPE";
                    if (!model.PreparedItemId.HasValue
                        || !model.ExpectedYield.HasValue
                        || model.ExpectedYield <= 0
                        || !model.OutputUnitId.HasValue)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult.Failure(
                            "Công thức BTP chưa liên kết (legacy). Cần chọn Bán thành phẩm, " +
                            "Sản lượng dự kiến sau hao hụt chuẩn và Đơn vị đầu ra trước khi tạo phiên bản Active mới.");
                    }
                }

                // PreparedItem identity immutability within a version chain
                if (oldRecipe.PreparedItemId.HasValue)
                {
                    model.RecipeType = "SUBRECIPE";
                    if (model.PreparedItemId.HasValue
                        && model.PreparedItemId.Value != oldRecipe.PreparedItemId.Value)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult.Failure(
                            "Không được đổi Bán thành phẩm đầu ra trong cùng chuỗi phiên bản. " +
                            "Hãy tạo công thức mới cho BTP khác.");
                    }

                    model.PreparedItemId = oldRecipe.PreparedItemId;
                }

                // 2. Validate category and output
                var targetValidation = await ValidateRecipeTargetAsync(model, existing: oldRecipe);
                if (!targetValidation.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return targetValidation;
                }

                var validationResult = ValidateDetails(model.Details);
                if (!validationResult.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return validationResult;
                }

                var dbValidation = await ValidateDetailsExistInDbAsync(model.Details);
                if (!dbValidation.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return dbValidation;
                }

                var unitValidation = await ValidateUnitMappingsAsync(model.Details);
                if (!unitValidation.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return unitValidation;
                }

                RecipeOutputNormalizationResult? output = null;
                if (IsBtpType(model.RecipeType) || oldRecipe.PreparedItemId.HasValue)
                {
                    int preparedItemId = oldRecipe.PreparedItemId ?? model.PreparedItemId!.Value;
                    decimal qty = model.ExpectedYield
                        ?? oldRecipe.OutputQuantity
                        ?? 0m;
                    int unitId = model.OutputUnitId
                        ?? oldRecipe.OutputUnitId
                        ?? 0;

                    var uniqueness = await EnsureNoOtherActiveForPreparedItemAsync(
                        preparedItemId,
                        excludeRecipeId: oldRecipe.RecipeId);
                    if (!uniqueness.IsSuccess)
                    {
                        await transaction.RollbackAsync();
                        return uniqueness;
                    }

                    var norm = await _outputNormalizer.NormalizeAsync(preparedItemId, qty, unitId);
                    if (!norm.IsSuccess)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult.Failure(norm.Message, norm.Errors, norm.ErrorCode);
                    }

                    output = norm.Data;
                }

                // Build child list for cycle/depth
                var newChildRecipeIds = model.Details
                    .Where(d => !string.IsNullOrEmpty(d.ItemCode) && d.ItemCode.StartsWith("REC_"))
                    .Select(d => int.Parse(d.ItemCode.Substring(4)))
                    .ToList();

                foreach (var childId in newChildRecipeIds)
                {
                    if (childId == recipeId)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult.Failure("Không thể thêm chính mình làm bán thành phẩm (Self-reference)!");
                    }

                    if (await HasCircularDependencyAsync(childId, recipeId, new HashSet<int>(), 0))
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult.Failure(
                            $"Phát hiện vòng lặp công thức! Bán thành phẩm #{childId} đã sử dụng công thức này làm thành phần.");
                    }

                    var depthResult = await CheckDepthLimitAsync(childId, 1);
                    if (!depthResult.IsSuccess)
                    {
                        await transaction.RollbackAsync();
                        return depthResult;
                    }
                }

                // 4–5. Archive old + insert new (atomic)
                oldRecipe.Status = "Archived";
                oldRecipe.Active = false;

                string recipeName = await ResolveRecipeNameAsync(model, output, oldRecipe);

                var newRecipe = new Recipe
                {
                    RecipeCode = GenerateRecipeCode(model),
                    Name = recipeName,
                    YieldPercentage = 100,
                    Active = true,
                    Status = "Active",
                    EffectiveDate = model.EffectiveDate,
                    ParentVersionId = oldRecipe.RecipeId,
                    DrinkId = model.RecipeType == "POS" ? model.DrinkId : null,
                    SizeId = model.RecipeType == "POS" ? model.SizeId : null,
                    ToppingId = model.RecipeType == "TOPPING" ? model.ToppingId : null,
                    PreparedItemId = output?.PreparedItemId,
                    OutputQuantity = output?.OutputQuantity,
                    OutputUnitId = output?.OutputUnitId,
                    RecipeDetails = new List<RecipeDetail>()
                };

                // POS/Topping version: preserve identity fields from model already set; clear BTP fields
                if (model.RecipeType == "POS" || model.RecipeType == "TOPPING")
                {
                    newRecipe.PreparedItemId = null;
                    newRecipe.OutputQuantity = null;
                    newRecipe.OutputUnitId = null;
                }

                foreach (var detailVM in model.Details)
                {
                    var detail = ParseRecipeDetail(detailVM);
                    newRecipe.RecipeDetails.Add(detail);
                }

                await _context.Recipes.AddAsync(newRecipe);

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException ex) when (IsActivePreparedItemUniqueViolation(ex))
                {
                    await transaction.RollbackAsync();
                    return ServiceResult.Failure(
                        "Đã có công thức đang hoạt động cho bán thành phẩm này. Mỗi BTP chỉ được một phiên bản Active.");
                }

                await transaction.CommitAsync();
                return ServiceResult.Success(
                    $"Cập nhật công thức thành công! (Phiên bản mới #{newRecipe.RecipeId}, bản cũ #{oldRecipe.RecipeId} đã lưu trữ)");
            }
            catch (ArgumentException ex)
            {
                await transaction.RollbackAsync();
                return ServiceResult.Failure(ex.Message);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ServiceResult.Failure($"Lỗi hệ thống khi cập nhật: {ex.Message}");
            }
        }

        // ============================================================
        // DELETE: Kiểm tra ràng buộc → Soft/Hard Delete
        // ============================================================
        public async Task<ServiceResult> DeleteRecipeAsync(int recipeId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var recipe = await _context.Recipes
                    .Include(r => r.RecipeDetails)
                    .FirstOrDefaultAsync(r => r.RecipeId == recipeId);

                if (recipe == null)
                {
                    return ServiceResult.Failure("Không tìm thấy công thức cần xóa.");
                }

                bool isUsedAsSubRecipe = await _context.RecipeDetails
                    .AnyAsync(rd => rd.ChildRecipeId == recipeId
                                 && rd.Recipe.Status == "Active");

                if (isUsedAsSubRecipe)
                {
                    return ServiceResult.Failure(
                        "Không thể xóa! Bán thành phẩm này đang được sử dụng bởi công thức khác đang hoạt động.");
                }

                bool hasOrderHistory = false;

                if (recipe.DrinkId.HasValue)
                {
                    hasOrderHistory = await _context.OrderDetails
                        .AnyAsync(od => od.DrinkId == recipe.DrinkId.Value);
                }

                if (!hasOrderHistory && recipe.ToppingId.HasValue)
                {
                    hasOrderHistory = await _context.OrderToppings
                        .AnyAsync(ot => ot.ToppingId == recipe.ToppingId.Value);
                }

                if (hasOrderHistory)
                {
                    recipe.Status = "Archived";
                    recipe.Active = false;
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return ServiceResult.Success(
                        $"Công thức #{recipeId} đã được lưu trữ (Archived) vì có liên kết lịch sử đơn hàng.");
                }
                else
                {
                    _context.RecipeDetails.RemoveRange(recipe.RecipeDetails);
                    _context.Recipes.Remove(recipe);
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return ServiceResult.Success($"Đã xóa hoàn toàn công thức #{recipeId}.");
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return ServiceResult.Failure($"Lỗi hệ thống khi xóa: {ex.Message}");
            }
        }

        // ============================================================
        // PRIVATE: Category + output rules (#112)
        // ============================================================
        private static bool IsBtpType(string? recipeType)
            => string.Equals(recipeType, "SUBRECIPE", StringComparison.OrdinalIgnoreCase);

        private async Task<ServiceResult> ValidateRecipeTargetAsync(
            RecipeCreateVM model,
            Recipe? existing)
        {
            // Reject forged BTP fields on POS / Topping
            if (model.RecipeType == "POS" || model.RecipeType == "TOPPING")
            {
                if (model.PreparedItemId.HasValue
                    || model.ExpectedYield.HasValue
                    || model.OutputUnitId.HasValue)
                {
                    return ServiceResult.Failure(
                        "Công thức món bán/topping không được gán Bán thành phẩm hoặc sản lượng đầu ra BTP.");
                }
            }

            if (model.RecipeType == "POS")
            {
                if (!model.DrinkId.HasValue)
                    return ServiceResult.Failure("Công thức món bán phải chọn sản phẩm.");

                if (!model.SizeId.HasValue)
                    return ServiceResult.Failure("Công thức món bán phải chọn size.");

                if (model.ToppingId.HasValue)
                    return ServiceResult.Failure("Công thức món bán không được gán ToppingId.");

                var hasDrinkSize = await _context.DrinkSizes
                    .AnyAsync(ds => ds.DrinkId == model.DrinkId.Value
                                 && ds.SizeId == model.SizeId.Value
                                 && ds.Active);

                if (!hasDrinkSize)
                    return ServiceResult.Failure("Size không hợp lệ hoặc không hoạt động cho sản phẩm đã chọn.");

                if (model.Active)
                {
                    var overlap = await _context.Recipes
                        .AsNoTracking()
                        .Include(r => r.Size)
                        .Where(r => r.DrinkId == model.DrinkId.Value
                            && r.SizeId == model.SizeId.Value
                            && r.ToppingId == null
                            && r.Active
                            && r.Status == "Active")
                        .Where(r => existing == null || r.RecipeId != existing.RecipeId)
                        .Select(r => new
                        {
                            r.RecipeId,
                            r.Name,
                            SizeName = r.Size != null ? r.Size.Name : null
                        })
                        .FirstOrDefaultAsync();

                    if (overlap != null)
                    {
                        return ServiceResult.Failure(
                            $"{overlap.Name} size {overlap.SizeName ?? model.SizeId.Value.ToString()} đã có công thức đang hoạt động. Hãy chỉnh ngày hiệu lực hoặc tạo phiên bản kế tiếp từ công thức #{overlap.RecipeId}.",
                            errorCode: BomRecipeErrorCodes.RecipeOverlap);
                    }
                }
            }
            else if (model.RecipeType == "TOPPING")
            {
                if (!model.ToppingId.HasValue)
                    return ServiceResult.Failure("Công thức topping phải chọn topping.");

                if (model.DrinkId.HasValue || model.SizeId.HasValue)
                    return ServiceResult.Failure("Công thức topping không được gán DrinkId/SizeId.");

                var hasTopping = await _context.Toppings
                    .AnyAsync(t => t.ToppingId == model.ToppingId.Value && t.Active);

                if (!hasTopping)
                    return ServiceResult.Failure("Topping không tồn tại hoặc đã ngưng hoạt động.");

                if (model.Active)
                {
                    var overlap = await _context.Recipes
                        .AsNoTracking()
                        .Where(r => r.ToppingId == model.ToppingId.Value
                            && r.Active
                            && r.Status == "Active")
                        .Where(r => existing == null || r.RecipeId != existing.RecipeId)
                        .Select(r => new { r.RecipeId, r.Name })
                        .FirstOrDefaultAsync();

                    if (overlap != null)
                    {
                        return ServiceResult.Failure(
                            $"Topping {overlap.Name} đã có công thức đang hoạt động. Hãy tạo phiên bản kế tiếp từ công thức #{overlap.RecipeId}.",
                            errorCode: BomRecipeErrorCodes.RecipeOverlap);
                    }
                }
            }
            else if (IsBtpType(model.RecipeType))
            {
                if (model.DrinkId.HasValue || model.SizeId.HasValue || model.ToppingId.HasValue)
                {
                    return ServiceResult.Failure(
                        "Công thức BTP không được gán DrinkId, SizeId hoặc ToppingId.");
                }

                if (!model.PreparedItemId.HasValue)
                    return ServiceResult.Failure("Công thức BTP phải chọn Bán thành phẩm đầu ra.");

                if (!model.ExpectedYield.HasValue || model.ExpectedYield.Value <= 0)
                {
                    return ServiceResult.Failure(
                        "Sản lượng dự kiến sau hao hụt chuẩn phải lớn hơn 0.");
                }

                if (!model.OutputUnitId.HasValue)
                    return ServiceResult.Failure("Công thức BTP phải chọn đơn vị đầu ra.");
            }
            else
            {
                return ServiceResult.Failure("Loại công thức không hợp lệ.");
            }

            return ServiceResult.Success();
        }

        private async Task<ServiceResult> EnsureNoOtherActiveForPreparedItemAsync(
            int preparedItemId,
            int? excludeRecipeId)
        {
            var query = _context.Recipes.AsNoTracking()
                .Where(r => r.PreparedItemId == preparedItemId && r.Active);

            if (excludeRecipeId.HasValue)
                query = query.Where(r => r.RecipeId != excludeRecipeId.Value);

            if (await query.AnyAsync())
            {
                return ServiceResult.Failure(
                    "Đã có công thức đang hoạt động cho bán thành phẩm này. Hãy tạo phiên bản kế tiếp từ công thức hiện có.",
                    errorCode: BomRecipeErrorCodes.RecipeOverlap);
            }

            return ServiceResult.Success();
        }

        private async Task<string> ResolveRecipeNameAsync(
            RecipeCreateVM model,
            RecipeOutputNormalizationResult? output,
            Recipe? oldRecipe = null)
        {
            if (model.RecipeType == "POS" && model.DrinkId.HasValue)
            {
                var drink = await _context.Drinks.FindAsync(model.DrinkId.Value);
                var size = model.SizeId.HasValue ? await _context.Sizes.FindAsync(model.SizeId.Value) : null;
                return size == null
                    ? drink?.Name ?? $"POS_Recipe_{model.DrinkId}"
                    : $"{drink?.Name ?? "POS"} - Size {size.Name}";
            }

            if (model.RecipeType == "TOPPING" && model.ToppingId.HasValue)
            {
                var topping = await _context.Toppings.FindAsync(model.ToppingId.Value);
                return topping?.Name ?? $"Topping_Recipe_{model.ToppingId}";
            }

            if (IsBtpType(model.RecipeType) && output != null)
            {
                // Stable display name from PreparedItem (not free-text identity)
                return string.IsNullOrWhiteSpace(output.PreparedItemName)
                    ? output.PreparedItemCode
                    : output.PreparedItemName;
            }

            if (oldRecipe != null && !string.IsNullOrWhiteSpace(oldRecipe.Name))
                return oldRecipe.Name;

            if (!string.IsNullOrWhiteSpace(model.SubRecipeName))
                return model.SubRecipeName.Trim();

            return $"Recipe_{DateTime.Now:yyyyMMdd_HHmmss}";
        }

        private string GenerateRecipeCode(RecipeCreateVM model)
        {
            var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            return model.RecipeType switch
            {
                "POS" => $"RCP_D{model.DrinkId}_S{model.SizeId}_{stamp}",
                "TOPPING" => $"RCP_T{model.ToppingId}_{stamp}",
                "SUBRECIPE" => model.PreparedItemId.HasValue
                    ? $"RCP_PI{model.PreparedItemId}_{stamp}"
                    : $"RCP_SUB_{stamp}",
                _ => $"RCP_{stamp}"
            };
        }

        private static bool IsActivePreparedItemUniqueViolation(DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return msg.Contains("IX_Recipes_OneActive_PreparedItem", StringComparison.OrdinalIgnoreCase)
                || (msg.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                    && msg.Contains("PreparedItem", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsActiveDrinkSizeUniqueViolation(DbUpdateException ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            return msg.Contains("UX_Recipes_OneActive_Drink_Size", StringComparison.OrdinalIgnoreCase);
        }

        // ============================================================
        // PRIVATE: Validate Details — Quantity, YieldPercentage, trùng lặp
        // ============================================================
        private ServiceResult ValidateDetails(List<RecipeDetailVM> details)
        {
            foreach (var d in details)
            {
                if (string.IsNullOrWhiteSpace(d.ItemCode)
                    || (!d.ItemCode.StartsWith("ING_", StringComparison.Ordinal)
                        && !d.ItemCode.StartsWith("REC_", StringComparison.Ordinal)))
                {
                    return ServiceResult.Failure(
                        "Có thành phần không có mã hợp lệ. Vui lòng chọn lại thành phần.",
                        errorCode: BomRecipeErrorCodes.InvalidPayload);
                }

                if (d.Quantity <= 0)
                {
                    return ServiceResult.Failure(
                        $"Định lượng của {d.ItemCode} phải lớn hơn 0.",
                        errorCode: BomRecipeErrorCodes.InvalidPayload);
                }

                if (decimal.Round(d.Quantity, 3) != d.Quantity)
                {
                    return ServiceResult.Failure(
                        $"Định lượng của {d.ItemCode} chỉ được tối đa 3 chữ số thập phân.",
                        errorCode: BomRecipeErrorCodes.InvalidPayload);
                }

                if (d.UnitId <= 0)
                {
                    return ServiceResult.Failure(
                        $"Thành phần {d.ItemCode} chưa có đơn vị hợp lệ.",
                        errorCode: BomRecipeErrorCodes.ComponentUomIncompatible);
                }

                if (d.YieldPercentage <= 0 || d.YieldPercentage > 100)
                {
                    return ServiceResult.Failure(
                        $"Tỷ lệ thu hồi của {d.ItemCode} phải lớn hơn 0% và không vượt quá 100%.",
                        errorCode: BomRecipeErrorCodes.InvalidPayload);
                }
            }

            var duplicateItems = details
                .GroupBy(d => d.ItemCode.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateItems.Any())
            {
                return ServiceResult.Failure(
                    $"Trùng thành phần trong cùng một cấp BOM: {string.Join(", ", duplicateItems)}. Mỗi nguyên liệu/bán thành phẩm chỉ được xuất hiện 1 lần.",
                    errorCode: BomRecipeErrorCodes.ComponentDuplicate);
            }

            return ServiceResult.Success();
        }

        private async Task<ServiceResult> ValidateDetailsExistInDbAsync(List<RecipeDetailVM> details)
        {
            foreach (var d in details)
            {
                if (d.ItemCode.StartsWith("ING_"))
                {
                    if (!int.TryParse(d.ItemCode.Substring(4), out int ingId))
                    {
                        return ServiceResult.Failure(
                            $"Mã nguyên liệu không hợp lệ: {d.ItemCode}.",
                            errorCode: BomRecipeErrorCodes.InvalidPayload);
                    }

                    bool exists = await _context.Ingredients
                        .AnyAsync(i => i.IngredientId == ingId && i.Active);
                    if (!exists)
                    {
                        return ServiceResult.Failure(
                            $"Nguyên liệu #{ingId} không tồn tại hoặc đã ngưng hoạt động.",
                            errorCode: BomRecipeErrorCodes.ComponentInactive);
                    }
                }
                else if (d.ItemCode.StartsWith("REC_"))
                {
                    if (!int.TryParse(d.ItemCode.Substring(4), out int recId))
                    {
                        return ServiceResult.Failure(
                            $"Mã công thức con không hợp lệ: {d.ItemCode}.",
                            errorCode: BomRecipeErrorCodes.InvalidPayload);
                    }

                    bool exists = await _context.Recipes
                        .AnyAsync(r => r.RecipeId == recId
                            && r.Active
                            && r.Status == "Active"
                            && r.PreparedItemId.HasValue
                            && r.OutputQuantity.HasValue
                            && r.OutputQuantity > 0
                            && r.OutputUnitId.HasValue
                            && r.PreparedItem != null
                            && r.PreparedItem.Active);
                    if (!exists)
                    {
                        return ServiceResult.Failure(
                            $"Bán thành phẩm #{recId} không tồn tại, đã lưu trữ hoặc chưa cấu hình đầu ra hợp lệ.",
                            errorCode: BomRecipeErrorCodes.ComponentInactive);
                    }
                }
            }

            return ServiceResult.Success();
        }

        private async Task<ServiceResult> ValidateUnitMappingsAsync(List<RecipeDetailVM> details)
        {
            foreach (var d in details)
            {
                if (d.ItemCode.StartsWith("ING_", StringComparison.Ordinal)
                    && int.TryParse(d.ItemCode.Substring(4), out int ingId))
                {
                    var ingredient = await _context.Ingredients
                        .AsNoTracking()
                        .Include(i => i.BaseUnit)
                        .Include(i => i.UnitConversions.Where(uc => uc.Active))
                        .FirstOrDefaultAsync(i => i.IngredientId == ingId);
                    if (ingredient == null)
                        continue;

                    var selectedUnit = await _context.Units
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.UnitId == d.UnitId);
                    var unitValidation = ValidateUnitPair(
                        selectedUnit,
                        ingredient.BaseUnit,
                        ingredient.UnitConversions,
                        ingredient.Name);
                    if (!unitValidation.IsSuccess)
                        return unitValidation;
                }
                else if (d.ItemCode.StartsWith("REC_", StringComparison.Ordinal)
                    && int.TryParse(d.ItemCode.Substring(4), out int childRecipeId))
                {
                    var child = await _context.Recipes
                        .AsNoTracking()
                        .Include(r => r.OutputUnit)
                        .FirstOrDefaultAsync(r => r.RecipeId == childRecipeId);
                    if (child?.OutputUnit == null)
                    {
                        return ServiceResult.Failure(
                            $"Bán thành phẩm #{childRecipeId} chưa có đơn vị đầu ra hợp lệ.",
                            errorCode: BomRecipeErrorCodes.ComponentConversionMissing);
                    }

                    var selectedUnit = await _context.Units
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.UnitId == d.UnitId);
                    var unitValidation = ValidateUnitPair(
                        selectedUnit,
                        child.OutputUnit,
                        Array.Empty<Models.Inventories.Ingredients.UnitConversion>(),
                        child.Name ?? $"BTP #{childRecipeId}");
                    if (!unitValidation.IsSuccess)
                        return unitValidation;
                }
            }

            return ServiceResult.Success();
        }

        private static ServiceResult ValidateUnitPair(
            Models.Inventories.Ingredients.Unit? selectedUnit,
            Models.Inventories.Ingredients.Unit? baseUnit,
            IEnumerable<Models.Inventories.Ingredients.UnitConversion> ingredientConversions,
            string componentName)
        {
            if (selectedUnit == null || baseUnit == null || !selectedUnit.Active || !baseUnit.Active)
            {
                return ServiceResult.Failure(
                    $"Đơn vị của thành phần '{componentName}' không tồn tại hoặc đã ngưng hoạt động.",
                    errorCode: BomRecipeErrorCodes.ComponentInactive);
            }

            if (selectedUnit.UnitId == baseUnit.UnitId)
                return ServiceResult.Success();

            if (selectedUnit.Type != baseUnit.Type)
            {
                return ServiceResult.Failure(
                    $"Đơn vị {FormatUnitLabel(selectedUnit.UnitCode)} không cùng loại với đơn vị tồn kho {FormatUnitLabel(baseUnit.UnitCode)} của '{componentName}'.",
                    errorCode: BomRecipeErrorCodes.ComponentUomIncompatible);
            }

            var hasIngredientConversion = ingredientConversions.Any(c =>
                c.Active
                && c.FromQuantity > 0
                && c.ToQuantity > 0
                && ((c.FromUnitId == selectedUnit.UnitId && c.ToUnitId == baseUnit.UnitId)
                    || (c.ToUnitId == selectedUnit.UnitId && c.FromUnitId == baseUnit.UnitId)));
            if (hasIngredientConversion)
                return ServiceResult.Success();

            if (selectedUnit.Type != UnitType.Dem
                && PhysicalUnitConversionRegistry.TryGetPairFactor(
                    selectedUnit.UnitCode,
                    baseUnit.UnitCode,
                    selectedUnit.Type,
                    baseUnit.Type,
                    out var factor)
                && factor > 0)
            {
                return ServiceResult.Success();
            }

            return ServiceResult.Failure(
                $"Thiếu quy đổi từ {FormatUnitLabel(selectedUnit.UnitCode)} sang {FormatUnitLabel(baseUnit.UnitCode)} cho thành phần '{componentName}'.",
                errorCode: BomRecipeErrorCodes.ComponentConversionMissing);
        }

        private async Task<ServiceResult> ValidateCreateCircularDependencyAsync(
            RecipeCreateVM model,
            IEnumerable<int> childRecipeIds)
        {
            if (!IsBtpType(model.RecipeType) || !model.PreparedItemId.HasValue)
                return ServiceResult.Success();

            foreach (var childRecipeId in childRecipeIds)
            {
                if (await RecipeProducesOrDependsOnPreparedItemAsync(
                    childRecipeId,
                    model.PreparedItemId.Value,
                    new HashSet<int>(),
                    0))
                {
                    return ServiceResult.Failure(
                        $"Bán thành phẩm được chọn tạo vòng lặp qua công thức con #{childRecipeId}. Hãy bỏ thành phần gây vòng lặp.",
                        errorCode: BomRecipeErrorCodes.CircularDependency);
                }
            }

            return ServiceResult.Success();
        }

        private async Task<bool> RecipeProducesOrDependsOnPreparedItemAsync(
            int recipeId,
            int preparedItemId,
            HashSet<int> visited,
            int depth)
        {
            if (depth > MAX_BOM_DEPTH || !visited.Add(recipeId))
                return false;

            var recipe = await _context.Recipes
                .AsNoTracking()
                .Where(r => r.RecipeId == recipeId)
                .Select(r => new
                {
                    r.PreparedItemId,
                    ChildIds = r.RecipeDetails
                        .Where(d => d.ChildRecipeId.HasValue)
                        .Select(d => d.ChildRecipeId!.Value)
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (recipe == null)
                return false;
            if (recipe.PreparedItemId == preparedItemId)
                return true;

            foreach (var childId in recipe.ChildIds)
            {
                if (await RecipeProducesOrDependsOnPreparedItemAsync(
                    childId,
                    preparedItemId,
                    visited,
                    depth + 1))
                    return true;
            }

            return false;
        }

        private static string FormatUnitLabel(string? unitCode)
        {
            return PhysicalUnitConversionRegistry.NormalizeUnitCode(unitCode) switch
            {
                "g" => "g",
                "gram" => "g",
                "kg" => "kg",
                "kilogram" => "kg",
                "ml" => "ml",
                "milliliter" => "ml",
                "l" => "L",
                "liter" => "L",
                "pcs" => "cái",
                "piece" => "cái",
                _ => string.IsNullOrWhiteSpace(unitCode) ? "ĐVT" : unitCode.Trim()
            };
        }

        private async Task<bool> HasCircularDependencyAsync(
            int currentRecipeId, int targetRecipeId, HashSet<int> visited, int currentDepth)
        {
            if (currentDepth > MAX_BOM_DEPTH)
                return false;

            if (!visited.Add(currentRecipeId))
                return false;

            var childRecipeIds = await _context.RecipeDetails
                .Where(rd => rd.RecipeId == currentRecipeId && rd.ChildRecipeId.HasValue)
                .Select(rd => rd.ChildRecipeId!.Value)
                .ToListAsync();

            foreach (var childId in childRecipeIds)
            {
                if (childId == targetRecipeId)
                    return true;

                if (await HasCircularDependencyAsync(childId, targetRecipeId, visited, currentDepth + 1))
                    return true;
            }

            return false;
        }

        private async Task<ServiceResult> CheckDepthLimitAsync(int recipeId, int currentDepth)
        {
            if (currentDepth > MAX_BOM_DEPTH)
            {
                return ServiceResult.Failure(
                    $"Cây BOM vượt quá {MAX_BOM_DEPTH} tầng đệ quy (tại Recipe #{recipeId}). " +
                    $"Hệ thống giới hạn tối đa {MAX_BOM_DEPTH} cấp để đảm bảo hiệu suất.");
            }

            var childRecipeIds = await _context.RecipeDetails
                .Where(rd => rd.RecipeId == recipeId && rd.ChildRecipeId.HasValue)
                .Select(rd => rd.ChildRecipeId!.Value)
                .ToListAsync();

            foreach (var childId in childRecipeIds)
            {
                var result = await CheckDepthLimitAsync(childId, currentDepth + 1);
                if (!result.IsSuccess)
                    return result;
            }

            return ServiceResult.Success();
        }

        private RecipeDetail ParseRecipeDetail(RecipeDetailVM detailVM)
        {
            var detail = new RecipeDetail
            {
                Quantity = detailVM.Quantity,
                UnitId = detailVM.UnitId
            };

            if (string.IsNullOrWhiteSpace(detailVM.ItemCode))
            {
                throw new ArgumentException("Mã cấu trúc thành phần (ItemCode) bị rỗng.");
            }

            if (detailVM.ItemCode.StartsWith("ING_"))
            {
                if (int.TryParse(detailVM.ItemCode.Substring(4), out int ingredientId))
                {
                    detail.IngredientId = ingredientId;
                    detail.ChildRecipeId = null;
                }
                else
                {
                    throw new ArgumentException($"Mã tham chiếu nguyên liệu không hợp lệ: {detailVM.ItemCode}");
                }
            }
            else if (detailVM.ItemCode.StartsWith("REC_"))
            {
                if (int.TryParse(detailVM.ItemCode.Substring(4), out int childRecipeId))
                {
                    detail.ChildRecipeId = childRecipeId;
                    detail.IngredientId = null;
                }
                else
                {
                    throw new ArgumentException($"Mã tham chiếu công thức con không hợp lệ: {detailVM.ItemCode}");
                }
            }
            else
            {
                throw new ArgumentException(
                    $"Định dạng ItemCode không hợp lệ: {detailVM.ItemCode}. Bắt buộc tiền tố ING_ hoặc REC_.");
            }

            return detail;
        }
    }
}
