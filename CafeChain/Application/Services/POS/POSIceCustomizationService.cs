using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.POS;

public sealed class POSIceCustomizationService : IPOSIceCustomizationService
{
    public const string InvalidIceLevel = "INVALID_ICE_LEVEL";
    public const string IceLevelRequired = "ICE_LEVEL_REQUIRED";
    public const string IceLevelNotAllowed = "ICE_LEVEL_NOT_ALLOWED";
    public const string IceBomInvalid = "ICE_BOM_INVALID";

    private const int MaxRecipeDepth = 16;

    private readonly AppDbContext _context;
    private readonly IUnitConversionService _unitConversion;
    private readonly IPhysicalUnitConversionService _physicalConversion;

    public POSIceCustomizationService(
        AppDbContext context,
        IUnitConversionService unitConversion,
        IPhysicalUnitConversionService physicalConversion)
    {
        _context = context;
        _unitConversion = unitConversion;
        _physicalConversion = physicalConversion;
    }

    public async Task<ServiceResult<POSIceEligibilityDto>> GetEligibilityAsync(
        int storeId,
        int drinkId,
        int? sizeId,
        CancellationToken cancellationToken = default)
    {
        if (storeId <= 0 || drinkId <= 0)
        {
            return ServiceResult<POSIceEligibilityDto>.Failure(
                "Thiếu cửa hàng hoặc sản phẩm để xác định mức đá.",
                errorCode: IceBomInvalid);
        }

        var canonicalIceIngredientId = await _context.IcePolicies
            .AsNoTracking()
            .Where(policy => policy.StoreId == storeId && policy.Active)
            .Select(policy => (int?)policy.IngredientId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!canonicalIceIngredientId.HasValue)
        {
            return ServiceResult<POSIceEligibilityDto>.Success(new POSIceEligibilityDto
            {
                SupportsIceCustomization = false
            });
        }

        var recipe = await GetActiveRecipeAsync(drinkId, sizeId, cancellationToken);
        if (recipe == null)
        {
            return ServiceResult<POSIceEligibilityDto>.Failure(
                "Sản phẩm chưa có công thức hoạt động để xác định mức đá.",
                errorCode: IceBomInvalid);
        }

        var amountResult = await CalculateCanonicalIceQuantityAsync(
            recipe,
            canonicalIceIngredientId.Value,
            1m,
            new HashSet<int>(),
            depth: 0,
            cancellationToken);
        if (!amountResult.IsSuccess)
        {
            return ServiceResult<POSIceEligibilityDto>.Failure(
                amountResult.Message,
                errorCode: amountResult.ErrorCode ?? IceBomInvalid);
        }

        var baseQuantity = amountResult.Data;
        return ServiceResult<POSIceEligibilityDto>.Success(new POSIceEligibilityDto
        {
            SupportsIceCustomization = baseQuantity > 0m,
            IceIngredientId = baseQuantity > 0m ? canonicalIceIngredientId : null,
            BaseIceQuantityBaseUnit = baseQuantity > 0m ? baseQuantity : null
        });
    }

    public async Task<ServiceResult<POSIceOrderSnapshotDto?>> CreateOrderSnapshotAsync(
        int storeId,
        int drinkId,
        int? sizeId,
        int quantity,
        int? iceLevelPercent,
        CancellationToken cancellationToken = default)
    {
        if (quantity <= 0)
        {
            return ServiceResult<POSIceOrderSnapshotDto?>.Failure(
                "Số lượng món phải lớn hơn 0.",
                errorCode: IceBomInvalid);
        }

        if (iceLevelPercent.HasValue && !POSIceLevels.IsAllowed(iceLevelPercent.Value))
        {
            return ServiceResult<POSIceOrderSnapshotDto?>.Failure(
                "Mức đá chỉ chấp nhận 0%, 50% hoặc 100%.",
                errorCode: InvalidIceLevel);
        }

        var eligibility = await GetEligibilityAsync(storeId, drinkId, sizeId, cancellationToken);
        if (!eligibility.IsSuccess || eligibility.Data == null)
        {
            return ServiceResult<POSIceOrderSnapshotDto?>.Failure(
                eligibility.Message,
                errorCode: eligibility.ErrorCode ?? IceBomInvalid);
        }

        if (!eligibility.Data.SupportsIceCustomization)
        {
            if (iceLevelPercent.HasValue)
            {
                return ServiceResult<POSIceOrderSnapshotDto?>.Failure(
                    "Món hoặc kích cỡ này không sử dụng nguyên liệu đá canonical.",
                    errorCode: IceLevelNotAllowed);
            }

            return ServiceResult<POSIceOrderSnapshotDto?>.Success(null);
        }

        if (!iceLevelPercent.HasValue)
        {
            return ServiceResult<POSIceOrderSnapshotDto?>.Failure(
                "Vui lòng chọn mức đá cho món này.",
                errorCode: IceLevelRequired);
        }

        var baseQuantity = eligibility.Data.BaseIceQuantityBaseUnit!.Value * quantity;
        var appliedQuantity = baseQuantity * iceLevelPercent.Value / 100m;
        return ServiceResult<POSIceOrderSnapshotDto?>.Success(new POSIceOrderSnapshotDto
        {
            IceLevelPercent = iceLevelPercent.Value,
            IceIngredientId = eligibility.Data.IceIngredientId!.Value,
            BaseIceQuantityBaseUnit = baseQuantity,
            AppliedIceQuantityBaseUnit = appliedQuantity
        });
    }

    private async Task<ServiceResult<decimal>> CalculateCanonicalIceQuantityAsync(
        Recipe recipe,
        int canonicalIceIngredientId,
        decimal multiplier,
        HashSet<int> path,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth > MaxRecipeDepth || !path.Add(recipe.RecipeId))
        {
            return ServiceResult<decimal>.Failure(
                "BOM có vòng lặp hoặc vượt quá độ sâu cho phép.",
                errorCode: IceBomInvalid);
        }

        try
        {
            decimal total = 0m;
            foreach (var detail in recipe.RecipeDetails.OrderBy(detail => detail.RecipeDetailId))
            {
                if (detail.IngredientId.HasValue)
                {
                    if (detail.IngredientId.Value != canonicalIceIngredientId)
                        continue;

                    var converted = await _unitConversion.ConvertAsync(
                        canonicalIceIngredientId,
                        detail.Quantity * multiplier,
                        detail.UnitId);
                    if (!converted.IsSuccess)
                    {
                        return ServiceResult<decimal>.Failure(
                            converted.Message,
                            errorCode: IceBomInvalid);
                    }

                    total += converted.Data;
                    continue;
                }

                if (!detail.ChildRecipeId.HasValue)
                {
                    return ServiceResult<decimal>.Failure(
                        $"Dòng BOM #{detail.RecipeDetailId} không có nguyên liệu hoặc ChildRecipe.",
                        errorCode: IceBomInvalid);
                }

                var child = await _context.Recipes
                    .AsNoTracking()
                    .Include(candidate => candidate.RecipeDetails)
                    .SingleOrDefaultAsync(
                        candidate => candidate.RecipeId == detail.ChildRecipeId.Value
                            && candidate.Active
                            && candidate.Status == "Active",
                        cancellationToken);
                if (child == null || child.OutputQuantity is null or <= 0 || !child.OutputUnitId.HasValue)
                {
                    return ServiceResult<decimal>.Failure(
                        $"ChildRecipe #{detail.ChildRecipeId} thiếu output contract hoạt động để flatten BOM đá.",
                        errorCode: IceBomInvalid);
                }

                var convertedOutput = await _physicalConversion.ConvertAsync(
                    detail.Quantity,
                    detail.UnitId,
                    child.OutputUnitId.Value);
                if (!convertedOutput.IsSuccess)
                {
                    return ServiceResult<decimal>.Failure(
                        convertedOutput.Message,
                        errorCode: IceBomInvalid);
                }

                var childMultiplier = multiplier * convertedOutput.Data / child.OutputQuantity.Value;
                var childResult = await CalculateCanonicalIceQuantityAsync(
                    child,
                    canonicalIceIngredientId,
                    childMultiplier,
                    path,
                    depth + 1,
                    cancellationToken);
                if (!childResult.IsSuccess)
                    return childResult;

                total += childResult.Data;
            }

            return ServiceResult<decimal>.Success(total);
        }
        finally
        {
            path.Remove(recipe.RecipeId);
        }
    }

    private async Task<Recipe?> GetActiveRecipeAsync(
        int drinkId,
        int? sizeId,
        CancellationToken cancellationToken)
    {
        var query = _context.Recipes
            .AsNoTracking()
            .Include(recipe => recipe.RecipeDetails)
            .Where(recipe => recipe.Active
                && recipe.Status == "Active"
                && recipe.DrinkId == drinkId
                && recipe.ToppingId == null);

        var sized = await query.FirstOrDefaultAsync(
            recipe => recipe.SizeId == sizeId,
            cancellationToken);
        return sized ?? await query.FirstOrDefaultAsync(
            recipe => recipe.SizeId == null,
            cancellationToken);
    }
}
