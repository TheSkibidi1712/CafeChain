using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Global physical unit conversion (kg↔g, l↔ml) via UnitCode + Unit.Type. Issue #110.
    /// </summary>
    public class PhysicalUnitConversionService : IPhysicalUnitConversionService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PhysicalUnitConversionService> _logger;

        /// <summary>Request-scoped unit metadata cache (UnitId → entity).</summary>
        private readonly Dictionary<int, Unit?> _unitCache = new();

        public PhysicalUnitConversionService(
            AppDbContext context,
            ILogger<PhysicalUnitConversionService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ServiceResult<decimal>> ConvertAsync(
            decimal quantity,
            int fromUnitId,
            int toUnitId)
        {
            if (fromUnitId <= 0 || toUnitId <= 0)
            {
                return ServiceResult<decimal>.Failure(
                    "UnitId không hợp lệ.",
                    errorCode: UnitConversionErrorCodes.InvalidUnit);
            }

            var fromUnit = await GetUnitAsync(fromUnitId);
            if (fromUnit == null)
            {
                return ServiceResult<decimal>.Failure(
                    $"Không tìm thấy đơn vị #{fromUnitId}.",
                    errorCode: UnitConversionErrorCodes.InvalidUnit);
            }

            if (!fromUnit.Active)
            {
                return ServiceResult<decimal>.Failure(
                    $"Đơn vị nguồn #{fromUnitId} không còn hiệu lực.",
                    errorCode: UnitConversionErrorCodes.InactiveUnit);
            }

            var toUnit = fromUnitId == toUnitId
                ? fromUnit
                : await GetUnitAsync(toUnitId);

            if (toUnit == null)
            {
                return ServiceResult<decimal>.Failure(
                    $"Không tìm thấy đơn vị #{toUnitId}.",
                    errorCode: UnitConversionErrorCodes.InvalidUnit);
            }

            if (!toUnit.Active)
            {
                return ServiceResult<decimal>.Failure(
                    $"Đơn vị đích #{toUnitId} không còn hiệu lực.",
                    errorCode: UnitConversionErrorCodes.InactiveUnit);
            }

            if (fromUnitId == toUnitId)
                return ServiceResult<decimal>.Success(quantity);

            // Count/package: only same-unit pass-through (already handled). No physical Dem conversion.
            if (fromUnit.Type == UnitType.Dem || toUnit.Type == UnitType.Dem)
            {
                return ServiceResult<decimal>.Failure(
                    $"Không thể quy đổi vật lý giữa đơn vị đếm/đóng gói (Unit {fromUnitId} → {toUnitId}).",
                    errorCode: UnitConversionErrorCodes.IncompatibleDimension);
            }

            if (fromUnit.Type != toUnit.Type)
            {
                return ServiceResult<decimal>.Failure(
                    $"Không tương thích chiều đơn vị (Unit {fromUnitId} → {toUnitId}).",
                    errorCode: UnitConversionErrorCodes.IncompatibleDimension);
            }

            if (fromUnit.Type != UnitType.KhoiLuong && fromUnit.Type != UnitType.TheTich)
            {
                return ServiceResult<decimal>.Failure(
                    $"Loại đơn vị không hỗ trợ quy đổi vật lý (Unit {fromUnitId} → {toUnitId}).",
                    errorCode: UnitConversionErrorCodes.IncompatibleDimension);
            }

            if (!PhysicalUnitConversionRegistry.TryGetPairFactor(
                    fromUnit.UnitCode,
                    toUnit.UnitCode,
                    fromUnit.Type,
                    toUnit.Type,
                    out var factor))
            {
                _logger.LogError(
                    "[PhysicalUnitConversion] Missing physical conversion FromUnitId={FromUnitId} ToUnitId={ToUnitId} FromCode={FromCode} ToCode={ToCode}",
                    fromUnitId,
                    toUnitId,
                    PhysicalUnitConversionRegistry.NormalizeUnitCode(fromUnit.UnitCode),
                    PhysicalUnitConversionRegistry.NormalizeUnitCode(toUnit.UnitCode));

                return ServiceResult<decimal>.Failure(
                    $"Thiếu quy đổi vật lý (Unit {fromUnitId} → {toUnitId}).",
                    errorCode: UnitConversionErrorCodes.MissingPhysicalConversion);
            }

            if (factor <= 0m)
            {
                return ServiceResult<decimal>.Failure(
                    $"Hệ số quy đổi vật lý không hợp lệ (Unit {fromUnitId} → {toUnitId}).",
                    errorCode: UnitConversionErrorCodes.InvalidFactor);
            }

            try
            {
                var converted = quantity * factor;
                return ServiceResult<decimal>.Success(converted);
            }
            catch (OverflowException ex)
            {
                _logger.LogError(
                    ex,
                    "[PhysicalUnitConversion] Overflow FromUnitId={FromUnitId} ToUnitId={ToUnitId}",
                    fromUnitId,
                    toUnitId);

                return ServiceResult<decimal>.Failure(
                    $"Tràn số khi quy đổi đơn vị (Unit {fromUnitId} → {toUnitId}).",
                    errorCode: UnitConversionErrorCodes.ConversionOverflow);
            }
        }

        private async Task<Unit?> GetUnitAsync(int unitId)
        {
            if (_unitCache.TryGetValue(unitId, out var cached))
                return cached;

            var unit = await _context.Units
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UnitId == unitId);

            _unitCache[unitId] = unit;
            return unit;
        }
    }
}
