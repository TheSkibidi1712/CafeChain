using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Inventories.Ingredients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Ingredient-context conversion: same unit → physical global → ACTIVE ingredient rows.
    /// Fail-closed; never silent raw quantity. Issue #110.
    /// </summary>
    public class UnitConversionService : IUnitConversionService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<UnitConversionService> _logger;
        private readonly IPhysicalUnitConversionService _physical;

        private readonly Dictionary<(int IngredientId, int From, int To), ServiceResult<decimal>> _factorCache = new();

        public UnitConversionService(
            AppDbContext context,
            ILogger<UnitConversionService> logger,
            IPhysicalUnitConversionService physical)
        {
            _context = context;
            _logger = logger;
            _physical = physical;
        }

        public async Task<ServiceResult<decimal>> ConvertAsync(
            int ingredientId,
            decimal quantity,
            int fromUnitId,
            int? toUnitId = null)
        {
            if (ingredientId <= 0)
            {
                return ServiceResult<decimal>.Failure(
                    "IngredientId không hợp lệ.",
                    errorCode: UnitConversionErrorCodes.InvalidIngredient);
            }

            if (fromUnitId <= 0)
            {
                return ServiceResult<decimal>.Failure(
                    "FromUnitId không hợp lệ.",
                    errorCode: UnitConversionErrorCodes.InvalidUnit);
            }

            var ingredient = await _context.Ingredients
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.IngredientId == ingredientId);

            if (ingredient == null)
            {
                return ServiceResult<decimal>.Failure(
                    $"Không tìm thấy nguyên liệu #{ingredientId}.",
                    errorCode: UnitConversionErrorCodes.InvalidIngredient);
            }

            var targetUnitId = toUnitId ?? ingredient.BaseUnitId;
            if (targetUnitId <= 0)
            {
                return ServiceResult<decimal>.Failure(
                    $"BaseUnit không hợp lệ cho nguyên liệu #{ingredientId}.",
                    errorCode: UnitConversionErrorCodes.InvalidUnit);
            }

            if (fromUnitId == targetUnitId)
                return ServiceResult<decimal>.Success(quantity);

            var cacheKey = (ingredientId, fromUnitId, targetUnitId);
            if (_factorCache.TryGetValue(cacheKey, out var cachedFactor))
            {
                if (!cachedFactor.IsSuccess)
                {
                    return ServiceResult<decimal>.Failure(
                        cachedFactor.Message,
                        errorCode: cachedFactor.ErrorCode);
                }

                return MultiplyQuantity(quantity, cachedFactor.Data, ingredientId, fromUnitId, targetUnitId);
            }

            // 1) Global physical conversion (kg↔g, l↔ml)
            var physical = await _physical.ConvertAsync(quantity, fromUnitId, targetUnitId);
            if (physical.IsSuccess)
            {
                var conflict = await CheckPhysicalConflictAsync(
                    ingredientId,
                    fromUnitId,
                    targetUnitId,
                    quantity,
                    physical.Data);

                if (conflict != null)
                {
                    _factorCache[cacheKey] = conflict;
                    return conflict;
                }

                // Cache unit factor when quantity != 0 for reuse
                if (quantity != 0m)
                {
                    try
                    {
                        var factor = physical.Data / quantity;
                        if (factor > 0m)
                            _factorCache[cacheKey] = ServiceResult<decimal>.Success(factor);
                    }
                    catch (OverflowException)
                    {
                        // Skip caching factor; still return success below
                    }
                }

                return physical;
            }

            // Hard physical failures must not be hidden by ingredient fallback.
            // Compatibility fallback is only for "not a universal physical pair" cases.
            if (!IsPhysicalCompatibilityFallback(physical.ErrorCode))
            {
                _factorCache[cacheKey] = physical;
                return ServiceResult<decimal>.Failure(
                    physical.Message,
                    errorCode: physical.ErrorCode);
            }

            // MISSING_PHYSICAL_CONVERSION / INCOMPATIBLE_DIMENSION → ACTIVE ingredient-specific only
            var direct = await _context.UnitConversions
                .AsNoTracking()
                .FirstOrDefaultAsync(uc =>
                    uc.IngredientId == ingredientId &&
                    uc.Active &&
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
                {
                    return ServiceResult<decimal>.Failure(
                        factorResult.Message,
                        errorCode: factorResult.ErrorCode ?? UnitConversionErrorCodes.InvalidFactor);
                }

                return MultiplyQuantity(quantity, factorResult.Data, ingredientId, fromUnitId, targetUnitId);
            }

            var reverse = await _context.UnitConversions
                .AsNoTracking()
                .FirstOrDefaultAsync(uc =>
                    uc.IngredientId == ingredientId &&
                    uc.Active &&
                    uc.FromUnitId == targetUnitId &&
                    uc.ToUnitId == fromUnitId);

            if (reverse != null)
            {
                var factorResult = BuildPositiveFactor(
                    reverse.ToQuantity,
                    reverse.FromQuantity,
                    ingredientId,
                    fromUnitId,
                    targetUnitId,
                    alsoRequirePositive: (reverse.FromQuantity, reverse.ToQuantity));
                _factorCache[cacheKey] = factorResult;
                if (!factorResult.IsSuccess)
                {
                    return ServiceResult<decimal>.Failure(
                        factorResult.Message,
                        errorCode: factorResult.ErrorCode ?? UnitConversionErrorCodes.InvalidFactor);
                }

                return MultiplyQuantity(quantity, factorResult.Data, ingredientId, fromUnitId, targetUnitId);
            }

            return MissingConversion(ingredientId, fromUnitId, targetUnitId, physical.ErrorCode);
        }

        public async Task<ServiceResult<IReadOnlyList<InventoryUnitOptionDTO>>> GetActiveUnitOptionsAsync(
            int ingredientId,
            CancellationToken cancellationToken = default)
        {
            var ingredient = await _context.Ingredients
                .AsNoTracking()
                .Include(x => x.BaseUnit)
                .FirstOrDefaultAsync(x => x.IngredientId == ingredientId, cancellationToken);
            if (ingredient == null)
            {
                return ServiceResult<IReadOnlyList<InventoryUnitOptionDTO>>.Failure(
                    $"Không tìm thấy nguyên liệu #{ingredientId}.",
                    errorCode: UnitConversionErrorCodes.InvalidIngredient);
            }

            if (ingredient.BaseUnit == null || !ingredient.BaseUnit.Active)
            {
                return ServiceResult<IReadOnlyList<InventoryUnitOptionDTO>>.Failure(
                    $"Đơn vị cơ sở của nguyên liệu #{ingredientId} không hoạt động.",
                    errorCode: UnitConversionErrorCodes.InvalidUnit);
            }

            var configuredUnitIds = await _context.UnitConversions
                .AsNoTracking()
                .Where(x => x.IngredientId == ingredientId && x.Active
                    && (x.FromUnitId == ingredient.BaseUnitId || x.ToUnitId == ingredient.BaseUnitId))
                .Select(x => x.FromUnitId == ingredient.BaseUnitId ? x.ToUnitId : x.FromUnitId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var baseDimension = ingredient.BaseUnit.Type;
            var physicalUnitIds = await _context.Units
                .AsNoTracking()
                .Where(x => x.Active && x.Type == baseDimension)
                .Where(x => x.UnitCode == PhysicalUnitConversionRegistry.CodeGram
                    || x.UnitCode == PhysicalUnitConversionRegistry.CodeKilogram
                    || x.UnitCode == PhysicalUnitConversionRegistry.CodeMilliliter
                    || x.UnitCode == PhysicalUnitConversionRegistry.CodeLiter)
                .Select(x => x.UnitId)
                .ToListAsync(cancellationToken);

            var candidateIds = configuredUnitIds
                .Concat(physicalUnitIds)
                .Append(ingredient.BaseUnitId)
                .Distinct()
                .ToList();
            var units = await _context.Units
                .AsNoTracking()
                .Where(x => candidateIds.Contains(x.UnitId) && x.Active)
                .ToDictionaryAsync(x => x.UnitId, cancellationToken);

            var options = new List<InventoryUnitOptionDTO>();
            foreach (var unitId in candidateIds)
            {
                if (!units.TryGetValue(unitId, out var unit))
                    continue;

                var converted = await ConvertAsync(ingredientId, 1m, unitId, ingredient.BaseUnitId);
                if (!converted.IsSuccess || converted.Data <= 0m)
                {
                    return ServiceResult<IReadOnlyList<InventoryUnitOptionDTO>>.Failure(
                        converted.Message,
                        errorCode: converted.ErrorCode ?? UnitConversionErrorCodes.MissingConversion);
                }

                options.Add(new InventoryUnitOptionDTO
                {
                    UnitId = unit.UnitId,
                    UnitCode = unit.UnitCode,
                    UnitName = unit.Name,
                    UnitType = unit.Type,
                    ConversionFactorToBase = converted.Data,
                    IsBaseUnit = unit.UnitId == ingredient.BaseUnitId
                });
            }

            return ServiceResult<IReadOnlyList<InventoryUnitOptionDTO>>.Success(options
                .OrderByDescending(x => x.IsBaseUnit)
                .ThenBy(x => x.UnitCode, StringComparer.OrdinalIgnoreCase)
                .ToList());
        }

        /// <summary>
        /// When physical succeeds, any ACTIVE ingredient row for the same pair must match or be absent.
        /// Conflicting or invalid ingredient factors fail closed.
        /// </summary>
        private async Task<ServiceResult<decimal>?> CheckPhysicalConflictAsync(
            int ingredientId,
            int fromUnitId,
            int toUnitId,
            decimal quantity,
            decimal physicalConverted)
        {
            var direct = await _context.UnitConversions
                .AsNoTracking()
                .FirstOrDefaultAsync(uc =>
                    uc.IngredientId == ingredientId &&
                    uc.Active &&
                    uc.FromUnitId == fromUnitId &&
                    uc.ToUnitId == toUnitId);

            var reverse = await _context.UnitConversions
                .AsNoTracking()
                .FirstOrDefaultAsync(uc =>
                    uc.IngredientId == ingredientId &&
                    uc.Active &&
                    uc.FromUnitId == toUnitId &&
                    uc.ToUnitId == fromUnitId);

            if (direct == null && reverse == null)
                return null;

            decimal physicalFactor;
            try
            {
                // Normalize against unit quantity 1 to avoid quantity-dependent comparison issues
                var unitPhysical = await _physical.ConvertAsync(1m, fromUnitId, toUnitId);
                if (!unitPhysical.IsSuccess || unitPhysical.Data <= 0m)
                {
                    return Conflicting(
                        ingredientId,
                        fromUnitId,
                        toUnitId,
                        "physical factor unavailable for conflict check");
                }

                physicalFactor = unitPhysical.Data;
            }
            catch (OverflowException)
            {
                return Conflicting(ingredientId, fromUnitId, toUnitId, "physical factor overflow");
            }

            if (direct != null)
            {
                var directFactor = BuildPositiveFactor(
                    direct.FromQuantity,
                    direct.ToQuantity,
                    ingredientId,
                    fromUnitId,
                    toUnitId);
                if (!directFactor.IsSuccess || directFactor.Data != physicalFactor)
                {
                    return Conflicting(ingredientId, fromUnitId, toUnitId, "active direct row differs from physical");
                }
            }

            if (reverse != null)
            {
                var reverseFactor = BuildPositiveFactor(
                    reverse.ToQuantity,
                    reverse.FromQuantity,
                    ingredientId,
                    fromUnitId,
                    toUnitId,
                    alsoRequirePositive: (reverse.FromQuantity, reverse.ToQuantity));
                if (!reverseFactor.IsSuccess || reverseFactor.Data != physicalFactor)
                {
                    return Conflicting(ingredientId, fromUnitId, toUnitId, "active reverse row differs from physical");
                }
            }

            // Silence unused warning — quantity reserved for future diagnostics
            _ = quantity;
            _ = physicalConverted;
            return null;
        }

        /// <summary>
        /// Ingredient fallback is allowed only when physical path is simply not applicable
        /// (unsupported pair / package dimension), not for invalid/inactive units or overflow.
        /// </summary>
        private static bool IsPhysicalCompatibilityFallback(string? errorCode)
        {
            return errorCode == UnitConversionErrorCodes.MissingPhysicalConversion
                || errorCode == UnitConversionErrorCodes.IncompatibleDimension
                // Defensive: older paths without ErrorCode still allow ingredient try
                || string.IsNullOrEmpty(errorCode);
        }

        private ServiceResult<decimal> Conflicting(
            int ingredientId,
            int fromUnitId,
            int toUnitId,
            string reason)
        {
            _logger.LogError(
                "[UnitConversion] CONFLICTING_CONVERSION IngredientId={IngredientId} FromUnitId={FromUnitId} ToUnitId={ToUnitId}: {Reason}",
                ingredientId,
                fromUnitId,
                toUnitId,
                reason);

            var fail = ServiceResult<decimal>.Failure(
                $"Quy đổi đơn vị xung đột với quy đổi vật lý cho nguyên liệu #{ingredientId} (Unit {fromUnitId} → {toUnitId}).",
                errorCode: UnitConversionErrorCodes.ConflictingConversion);
            return fail;
        }

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

        private ServiceResult<decimal> MultiplyQuantity(
            decimal quantity,
            decimal factor,
            int ingredientId,
            int fromUnitId,
            int toUnitId)
        {
            if (factor <= 0)
                return InvalidFactor(ingredientId, fromUnitId, toUnitId, "factor ≤ 0");

            try
            {
                return ServiceResult<decimal>.Success(quantity * factor);
            }
            catch (OverflowException ex)
            {
                _logger.LogError(
                    ex,
                    "[UnitConversion] Overflow IngredientId={IngredientId} FromUnitId={FromUnitId} ToUnitId={ToUnitId}",
                    ingredientId,
                    fromUnitId,
                    toUnitId);

                return ServiceResult<decimal>.Failure(
                    $"Tràn số khi quy đổi đơn vị cho nguyên liệu #{ingredientId} (Unit {fromUnitId} → {toUnitId}).",
                    errorCode: UnitConversionErrorCodes.ConversionOverflow);
            }
        }

        private ServiceResult<decimal> MissingConversion(
            int ingredientId,
            int fromUnitId,
            int toUnitId,
            string? physicalErrorCode)
        {
            _logger.LogError(
                "[UnitConversion] Missing conversion IngredientId={IngredientId} FromUnitId={FromUnitId} ToUnitId={ToUnitId} PhysicalCode={PhysicalCode}",
                ingredientId,
                fromUnitId,
                toUnitId,
                physicalErrorCode);

            var fail = ServiceResult<decimal>.Failure(
                $"Thiếu quy đổi đơn vị cho nguyên liệu #{ingredientId} (Unit {fromUnitId} → {toUnitId}).",
                errorCode: UnitConversionErrorCodes.MissingConversion);
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
                ingredientId,
                fromUnitId,
                toUnitId,
                reason);

            return ServiceResult<decimal>.Failure(
                $"Quy đổi đơn vị không hợp lệ cho nguyên liệu #{ingredientId} (Unit {fromUnitId} → {toUnitId}).",
                errorCode: UnitConversionErrorCodes.InvalidFactor);
        }
    }
}
