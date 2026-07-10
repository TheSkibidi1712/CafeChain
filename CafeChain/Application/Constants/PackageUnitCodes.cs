using System;
using System.Collections.Generic;

namespace CafeChain.Application.Constants
{
    /// <summary>
    /// Commercial packaging unit codes rejected as structured package content units (#111).
    /// Countable inventory units such as pcs are allowed only when equal to ingredient base unit.
    /// </summary>
    public static class PackageUnitCodes
    {
        public static readonly HashSet<string> RejectedCommercialPackaging = new(StringComparer.OrdinalIgnoreCase)
        {
            "bottle",
            "can",
            "pack"
        };

        public static string Normalize(string? unitCode)
        {
            if (string.IsNullOrWhiteSpace(unitCode))
                return string.Empty;
            return unitCode.Trim().ToLowerInvariant();
        }

        public static bool IsRejectedCommercialPackaging(string? unitCode)
            => RejectedCommercialPackaging.Contains(Normalize(unitCode));
    }
}
