using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.DTOs.Admin.Recipes;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Production;
using CafeChain.Application.Interfaces.Admin.Recipes;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Admin.Recipes;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Production;

/// <summary>
/// Authoritative resolver for offering and accepting the PRODUCTION source.
/// UI callers and mutation handlers must use the same result.
/// </summary>
public sealed class ProductionSourceEligibilityService : IProductionSourceEligibilityService
{
    private readonly AppDbContext _context;
    private readonly IRecipeOutputNormalizer _outputNormalizer;
    private readonly IAdminPermissionService _permissions;
    private readonly ICurrentRecipeResolver _currentRecipeResolver;
    private readonly TimeProvider _timeProvider;

    public ProductionSourceEligibilityService(
        AppDbContext context,
        IRecipeOutputNormalizer outputNormalizer,
        IAdminPermissionService permissions,
        ICurrentRecipeResolver? currentRecipeResolver = null,
        TimeProvider? timeProvider = null)
    {
        _context = context;
        _outputNormalizer = outputNormalizer;
        _permissions = permissions;
        _currentRecipeResolver = currentRecipeResolver ?? new CurrentRecipeResolver(context);
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ServiceResult<ProductionSourceEligibilityDto>> EvaluateAsync(
        ProductionSourceEligibilityRequest request)
    {
        var result = NewResult(request);
        var hasIngredient = request.IngredientId.HasValue && request.IngredientId.Value > 0;
        var hasPreparedItem = request.PreparedItemId.HasValue && request.PreparedItemId.Value > 0;
        if (request.StoreId <= 0
            || request.ActorAccountId <= 0
            || string.IsNullOrWhiteSpace(request.RequiredPermissionCode)
            || hasIngredient == hasPreparedItem)
        {
            return EligibleResult(result, false,
                ProductionEligibilityReasonCodes.InvalidRequest,
                "Thông tin kiểm tra nguồn sản xuất chưa hợp lệ.");
        }

        var atUtc = request.AtUtc ?? _timeProvider.GetUtcNow().UtcDateTime;
        var storeExists = await _context.Stores
            .AsNoTracking()
            .AnyAsync(x => x.StoreId == request.StoreId && x.Active);
        if (!storeExists)
        {
            return EligibleResult(result, false,
                ProductionEligibilityReasonCodes.StoreUnavailable,
                "Cửa hàng không tồn tại hoặc đã ngừng hoạt động.");
        }

        var itemExists = hasIngredient
            ? await _context.Ingredients.AsNoTracking()
                .AnyAsync(x => x.IngredientId == request.IngredientId && x.Active)
            : await _context.PreparedItems.AsNoTracking()
                .AnyAsync(x => x.PreparedItemId == request.PreparedItemId && x.Active);
        if (!itemExists)
        {
            return EligibleResult(result, false,
                ProductionEligibilityReasonCodes.ItemUnavailable,
                "Mặt hàng tồn kho không tồn tại hoặc đã ngừng hoạt động.");
        }

        var globalCapability = await _context.InventoryItemSourceCapabilities
            .AsNoTracking()
            .AnyAsync(x => x.Active
                && x.CanProduce
                && x.EffectiveFromUtc <= atUtc
                && (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc > atUtc)
                && x.IngredientId == request.IngredientId
                && x.PreparedItemId == request.PreparedItemId);
        if (!globalCapability)
        {
            return EligibleResult(result, false,
                ProductionEligibilityReasonCodes.ItemCapabilityMissing,
                "Mặt hàng chưa được cấu hình cho phép sản xuất nội bộ.");
        }

        var storeCapability = await _context.StoreProductionCapabilities
            .AsNoTracking()
            .AnyAsync(x => x.Active
                && x.StoreId == request.StoreId
                && x.EffectiveFromUtc <= atUtc
                && (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc > atUtc)
                && x.IngredientId == request.IngredientId
                && x.PreparedItemId == request.PreparedItemId);
        if (!storeCapability)
        {
            return EligibleResult(result, false,
                ProductionEligibilityReasonCodes.StoreCapabilityMissing,
                "Cửa hàng chưa được cấu hình năng lực sản xuất mặt hàng này.");
        }

        var permission = await _permissions.HasPermissionAsync(
            request.ActorAccountId,
            request.RequiredPermissionCode,
            request.StoreId);
        if (!permission.IsSuccess || permission.Data?.Allowed != true)
        {
            return EligibleResult(result, false,
                ProductionEligibilityReasonCodes.PermissionDenied,
                "Bạn không có quyền chọn nguồn sản xuất tại cửa hàng này.");
        }

        if (!request.PreparedItemId.HasValue)
        {
            return EligibleResult(result, false,
                ProductionEligibilityReasonCodes.RecipeMissing,
                "Mặt hàng chưa có bán thành phẩm đầu ra để xác định công thức sản xuất.");
        }

        var resolution = await _currentRecipeResolver.ResolveAsync(
            new RecipeTarget.PreparedItem(request.PreparedItemId.Value),
            atUtc);
        if (resolution.Status != CurrentRecipeResolutionStatus.Found
            || resolution.Recipe == null)
        {
            return EligibleResult(result, false,
                ProductionEligibilityReasonCodes.RecipeMissing,
                resolution.Status == CurrentRecipeResolutionStatus.Ambiguous
                    ? "Bán thành phẩm có nhiều công thức đang áp dụng; cần xử lý trước khi sản xuất."
                    : "Bán thành phẩm chưa có công thức sản xuất đang áp dụng.");
        }

        var recipe = await _context.Recipes
            .AsNoTracking()
            .Where(x => x.RecipeId == resolution.Recipe.RecipeId)
            .Select(x => new
            {
                x.RecipeId,
                x.PreparedItemId,
                x.OutputQuantity,
                x.OutputUnitId
            })
            .SingleOrDefaultAsync();
        if (recipe == null)
        {
            return EligibleResult(result, false,
                ProductionEligibilityReasonCodes.RecipeMissing,
                "Bán thành phẩm chưa có công thức sản xuất đang áp dụng.");
        }

        if (!recipe.PreparedItemId.HasValue
            || !recipe.OutputQuantity.HasValue
            || recipe.OutputQuantity <= 0
            || !recipe.OutputUnitId.HasValue)
        {
            return EligibleResult(result, false,
                ProductionEligibilityReasonCodes.OutputContractInvalid,
                "Công thức sản xuất chưa có sản lượng dự kiến hợp lệ.");
        }

        var normalized = await _outputNormalizer.NormalizeAsync(
            recipe.PreparedItemId.Value,
            recipe.OutputQuantity.Value,
            recipe.OutputUnitId.Value);
        if (!normalized.IsSuccess || normalized.Data == null)
        {
            return EligibleResult(result, false,
                ProductionEligibilityReasonCodes.OutputContractInvalid,
                normalized.Message ?? "Không thể quy đổi sản lượng dự kiến về đơn vị tồn kho cơ sở.");
        }

        result.RecipeId = recipe.RecipeId;
        result.ExpectedOutputPerBatchBase = normalized.Data.NormalizedQuantityInBase;
        result.OutputBaseUnitId = normalized.Data.BaseUnitId;
        result.OutputBaseUnitCode = normalized.Data.BaseUnitCode;
        return EligibleResult(result, true,
            ProductionEligibilityReasonCodes.Eligible,
            "Có thể chọn nguồn sản xuất nội bộ.");
    }

    private static ProductionSourceEligibilityDto NewResult(
        ProductionSourceEligibilityRequest request)
        => new()
        {
            StoreId = request.StoreId,
            IngredientId = request.IngredientId,
            PreparedItemId = request.PreparedItemId
        };

    private static ServiceResult<ProductionSourceEligibilityDto> EligibleResult(
        ProductionSourceEligibilityDto result,
        bool eligible,
        string reasonCode,
        string message)
    {
        result.Eligible = eligible;
        result.ReasonCode = reasonCode;
        result.Message = message;
        return ServiceResult<ProductionSourceEligibilityDto>.Success(result);
    }
}
