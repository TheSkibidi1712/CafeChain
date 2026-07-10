using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Shared conversion: direct map, then safe reverse map.
    /// Missing or invalid conversion returns Failure (never silent raw quantity).
    /// </summary>
    public class UnitConversionService : IUnitConversionService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UnitConversionService> _logger;

        private readonly Dictionary<(int IngredientId, int From, int To), ServiceResult<decimal>> _factorCache = new();

        public UnitConversionService(AppDbContext context, ILogger<UnitConversionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ServiceResult<decimal>> ConvertAsync(
            int ingredientId,
            decimal quantity,
            int fromUnitId,
            int? toUnitId = null)
        {
            if (ingredientId <= 0)
                return ServiceResult<decimal>.Failure("IngredientId không hợp lệ.");
            if (fromUnitId <= 0)
                return ServiceResult<decimal>.Failure("FromUnitId không hợp lệ.");

            var ingredient = await _context.Ingredients
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.IngredientId == ingredientId);

            if (ingredient == null)
                return ServiceResult<decimal>.Failure($"Không tìm thấy nguyên liệu #{ingredientId}.");

            var targetUnitId = toUnitId ?? ingredient.BaseUnitId;
            if (targetUnitId <= 0)
                return ServiceResult<decimal>.Failure($"BaseUnit không hợp lệ cho nguyên liệu #{ingredientId}.");

            if (fromUnitId == targetUnitId)
                return ServiceResult<decimal>.Success(quantity);

            var cacheKey = (ingredientId, fromUnitId, targetUnitId);
            if (_factorCache.TryGetValue(cacheKey, out var cachedFactor))
            {
                if (!cachedFactor.IsSuccess)
                    return ServiceResult<decimal>.Failure(cachedFactor.Message);
                return Multiply(quantity, cachedFactor.Data, ingredientId, fromUnitId, targetUnitId);
            }

            // Direct: From → To  (factor = ToQuantity / FromQuantity)
            var direct = await _context.UnitConversions
                .AsNoTracking()
                .FirstOrDefaultAsync(uc =>
                    uc.IngredientId == ingredientId &&
                    uc.FromUnitId == fromUnitId &&
                    uc.ToUnitId == targetUnitId);

            if (direct != null)
            {
                var factorResult = BuildPositiveFactor(
                    direct.FromQuantity,
                    direct.ToQuantity,
                    ingredientId,
                    fromUnitId,
                    targetUnitId);
                _factorCache[cacheKey] = factorResult;
                if (!factorResult.IsSuccess)
                    return ServiceResult<decimal>.Failure(factorResult.Message);
                return Multiply(quantity, factorResult.Data, ingredientId, fromUnitId, targetUnitId);
            }

            // Reverse: record maps target → from; invert (factor = FromQuantity / ToQuantity of reverse row)
            var reverse = await _context.UnitConversions
                .AsNoTracking()
                .FirstOrDefaultAsync(uc =>
                    uc.IngredientId == ingredientId &&
                    uc.FromUnitId == targetUnitId &&
                    uc.ToUnitId == fromUnitId);

            if (reverse != null)
            {
                // Validate both sides of reverse record, then factor = reverse.From / reverse.To
                var factorResult = BuildPositiveFactor(
                    reverse.ToQuantity,
                    reverse.FromQuantity,
                    ingredientId,
                    fromUnitId,
                    targetUnitId,
                    alsoRequirePositive: (reverse.FromQuantity, reverse.ToQuantity));
                _factorCache[cacheKey] = factorResult;
                if (!factorResult.IsSuccess)
                    return ServiceResult<decimal>.Failure(factorResult.Message);
                return Multiply(quantity, factorResult.Data, ingredientId, fromUnitId, targetUnitId);
            }

            return MissingConversion(ingredientId, fromUnitId, targetUnitId);
        }

        /// <summary>
        /// factor = toSide / fromSide; both sides of the ratio and optional raw pair must be &gt; 0.
        /// </summary>
        private ServiceResult<decimal> BuildPositiveFactor(
            decimal fromSide,
            decimal toSide,
            int ingredientId,
            int fromUnitId,
            int toUnitId,
            (decimal From, decimal To)? alsoRequirePositive = null)
        {
            if (alsoRequirePositive.HasValue)
            {
                var (rawFrom, rawTo) = alsoRequirePositive.Value;
                if (rawFrom == 0 || rawTo == 0)
                    return InvalidFactor(ingredientId, fromUnitId, toUnitId, "FromQuantity/ToQuantity = 0");
                if (rawFrom < 0 || rawTo < 0)
                    return InvalidFactor(ingredientId, fromUnitId, toUnitId, "FromQuantity/ToQuantity < 0");
            }

            if (fromSide == 0 || toSide == 0)
                return InvalidFactor(ingredientId, fromUnitId, toUnitId, "FromQuantity/ToQuantity = 0");
            if (fromSide < 0 || toSide < 0)
                return InvalidFactor(ingredientId, fromUnitId, toUnitId, "FromQuantity/ToQuantity < 0");

            var factor = toSide / fromSide;
            if (factor <= 0)
                return InvalidFactor(ingredientId, fromUnitId, toUnitId, "factor ≤ 0");

            return ServiceResult<decimal>.Success(factor);
        }

        private ServiceResult<decimal> Multiply(
            decimal quantity,
            decimal factor,
            int ingredientId,
            int fromUnitId,
            int toUnitId)
        {
            if (factor <= 0)
                return InvalidFactor(ingredientId, fromUnitId, toUnitId, "factor ≤ 0");

            return ServiceResult<decimal>.Success(quantity * factor);
        }

        private ServiceResult<decimal> MissingConversion(int ingredientId, int fromUnitId, int toUnitId)
        {
            _logger.LogError(
                "[UnitConversion] Missing conversion IngredientId={IngredientId} FromUnitId={FromUnitId} ToUnitId={ToUnitId}",
                ingredientId, fromUnitId, toUnitId);

            var fail = ServiceResult<decimal>.Failure(
                $"Thiếu quy đổi đơn vị cho nguyên liệu #{ingredientId} (Unit {fromUnitId} → {toUnitId}).");
            _factorCache[(ingredientId, fromUnitId, toUnitId)] = fail;
            return fail;
        }

        private ServiceResult<decimal> InvalidFactor(
            int ingredientId,
            int fromUnitId,
            int toUnitId,
            string reason)
        {
            _logger.LogError(
                "[UnitConversion] Invalid conversion factor IngredientId={IngredientId} FromUnitId={FromUnitId} ToUnitId={ToUnitId}: {Reason}",
                ingredientId, fromUnitId, toUnitId, reason);

            return ServiceResult<decimal>.Failure(
                $"Quy đổi đơn vị không hợp lệ cho nguyên liệu #{ingredientId} (Unit {fromUnitId} → {toUnitId}).");
        }
    }
}
