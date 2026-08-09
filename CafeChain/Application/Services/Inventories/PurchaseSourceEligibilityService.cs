using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories;

/// <summary>
/// Keeps the established raw-ingredient purchase path while requiring an explicit
/// capability and supplier contract before a prepared item can be bought externally.
/// </summary>
public sealed class PurchaseSourceEligibilityService : IPurchaseSourceEligibilityService
{
    private readonly AppDbContext _context;

    public PurchaseSourceEligibilityService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceResult<PurchaseSourceEligibilityDto>> EvaluateAsync(
        PurchaseSourceEligibilityRequest request)
    {
        var hasIngredient = request.IngredientId.HasValue && request.IngredientId.Value > 0;
        var hasPreparedItem = request.PreparedItemId.HasValue && request.PreparedItemId.Value > 0;
        if (request.StoreId <= 0 || hasIngredient == hasPreparedItem)
        {
            return Result(false, PurchaseEligibilityReasonCodes.InvalidRequest,
                "Thông tin kiểm tra nguồn mua ngoài chưa hợp lệ.");
        }

        if (hasIngredient)
        {
            var ingredientActive = await _context.Ingredients
                .AsNoTracking()
                .AnyAsync(x => x.IngredientId == request.IngredientId && x.Active);
            return ingredientActive
                ? Result(true, PurchaseEligibilityReasonCodes.Eligible,
                    "Có thể tiếp tục lập đề nghị mua theo quy trình hiện tại.")
                : Result(false, PurchaseEligibilityReasonCodes.ItemUnavailable,
                    "Nguyên liệu không tồn tại hoặc đã ngừng hoạt động.");
        }

        var preparedItemActive = await _context.PreparedItems
            .AsNoTracking()
            .AnyAsync(x => x.PreparedItemId == request.PreparedItemId && x.Active);
        if (!preparedItemActive)
        {
            return Result(false, PurchaseEligibilityReasonCodes.ItemUnavailable,
                "Bán thành phẩm không tồn tại hoặc đã ngừng hoạt động.");
        }

        var atUtc = request.AtUtc ?? DateTime.UtcNow;
        var canPurchase = await _context.InventoryItemSourceCapabilities
            .AsNoTracking()
            .AnyAsync(x => x.Active
                && x.CanPurchase
                && x.PreparedItemId == request.PreparedItemId
                && !x.IngredientId.HasValue
                && x.EffectiveFromUtc <= atUtc
                && (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc > atUtc));
        if (!canPurchase)
        {
            return Result(false, PurchaseEligibilityReasonCodes.CapabilityMissing,
                "Bán thành phẩm chưa được cấu hình cho phép mua ngoài.");
        }

        // The current supplier-package aggregate only identifies Ingredient. It cannot
        // prove a package belongs to this PreparedItem, so the safe v2 answer is blocked
        // until that additive supplier contract exists.
        return Result(false, PurchaseEligibilityReasonCodes.PackageMissing,
            "Bán thành phẩm chưa có gói mua đang hoạt động và phù hợp với cửa hàng.");
    }

    private static ServiceResult<PurchaseSourceEligibilityDto> Result(
        bool eligible,
        string reasonCode,
        string message)
        => ServiceResult<PurchaseSourceEligibilityDto>.Success(new PurchaseSourceEligibilityDto
        {
            Eligible = eligible,
            ReasonCode = reasonCode,
            Message = message
        });
}
