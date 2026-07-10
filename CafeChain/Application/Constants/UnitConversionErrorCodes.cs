namespace CafeChain.Application.Constants
{
    /// <summary>
    /// Named error codes for physical and ingredient unit conversion (Issue #110).
    /// </summary>
    public static class UnitConversionErrorCodes
    {
        public const string InvalidUnit = "INVALID_UNIT";
        public const string InactiveUnit = "INACTIVE_UNIT";
        public const string IncompatibleDimension = "INCOMPATIBLE_DIMENSION";
        public const string MissingPhysicalConversion = "MISSING_PHYSICAL_CONVERSION";
        public const string MissingConversion = "MISSING_CONVERSION";
        public const string InvalidFactor = "INVALID_FACTOR";
        public const string ConversionOverflow = "CONVERSION_OVERFLOW";
        public const string ConflictingConversion = "CONFLICTING_CONVERSION";
        public const string InvalidIngredient = "INVALID_INGREDIENT";
    }
}
