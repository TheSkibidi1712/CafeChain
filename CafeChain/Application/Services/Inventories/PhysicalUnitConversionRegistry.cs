using System;
using System.Collections.Generic;
using CafeChain.Models.Enums.Unit;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Immutable graduation-MVP physical conversion graph keyed by normalized UnitCode (not UnitId).
    /// Mass canonical: g. Volume canonical: ml.
    /// </summary>
    public static class PhysicalUnitConversionRegistry
    {
        public const string CodeGram = "g";
        public const string CodeKilogram = "kg";
        public const string CodeMilliliter = "ml";
        public const string CodeLiter = "l";

        /// <summary>
        /// Multiply quantity by this factor to express it in the dimension canonical unit.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, (UnitType Dimension, decimal ToCanonical)> Factors =
            new Dictionary<string, (UnitType, decimal)>(StringComparer.Ordinal)
            {
                [CodeGram] = (UnitType.KhoiLuong, 1m),
                [CodeKilogram] = (UnitType.KhoiLuong, 1000m),
                [CodeMilliliter] = (UnitType.TheTich, 1m),
                [CodeLiter] = (UnitType.TheTich, 1000m)
            };

        public static string NormalizeUnitCode(string? unitCode)
        {
            if (string.IsNullOrWhiteSpace(unitCode))
                return string.Empty;

            return unitCode.Trim().ToLowerInvariant();
        }

        public static bool TryGetToCanonicalFactor(
            string? unitCode,
            out UnitType dimension,
            out decimal toCanonicalFactor)
        {
            dimension = default;
            toCanonicalFactor = 0m;

            var code = NormalizeUnitCode(unitCode);
            if (code.Length == 0)
                return false;

            if (!Factors.TryGetValue(code, out var entry))
                return false;

            if (entry.ToCanonical <= 0m)
                return false;

            dimension = entry.Dimension;
            toCanonicalFactor = entry.ToCanonical;
            return true;
        }

        /// <summary>
        /// Factor that converts one unit of <paramref name="fromCode"/> into one unit of <paramref name="toCode"/>.
        /// </summary>
        public static bool TryGetPairFactor(
            string? fromCode,
            string? toCode,
            UnitType fromType,
            UnitType toType,
            out decimal factor)
        {
            factor = 0m;

            if (fromType != toType)
                return false;

            if (fromType != UnitType.KhoiLuong && fromType != UnitType.TheTich)
                return false;

            if (!TryGetToCanonicalFactor(fromCode, out var fromDim, out var fromToCanon))
                return false;
            if (!TryGetToCanonicalFactor(toCode, out var toDim, out var toToCanon))
                return false;

            if (fromDim != fromType || toDim != toType || fromDim != toDim)
                return false;

            if (fromToCanon <= 0m || toToCanon <= 0m)
                return false;

            // qty_to = qty_from * (from→canonical) / (to→canonical)
            factor = fromToCanon / toToCanon;
            return factor > 0m;
        }
    }
}
