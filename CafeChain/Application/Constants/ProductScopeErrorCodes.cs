namespace CafeChain.Application.Constants
{
    /// <summary>
    /// Temporary product-scope removals (soft). Prefer FEATURE_NOT_AVAILABLE over silent ignore.
    /// Voucher and loyalty/điểm thưởng are out of active product scope.
    /// </summary>
    public static class ProductScopeErrorCodes
    {
        public const string FeatureNotAvailable = "FEATURE_NOT_AVAILABLE";

        public const string VoucherNotAvailableMessage =
            "Voucher tạm thời không còn được hỗ trợ trong phạm vi sản phẩm hiện tại.";

        public const string LoyaltyNotAvailableMessage =
            "Điểm thưởng (loyalty) tạm thời không còn được hỗ trợ trong phạm vi sản phẩm hiện tại.";

        public const string VoucherOrLoyaltyNotAvailableMessage =
            "Voucher và điểm thưởng tạm thời không còn được hỗ trợ trong phạm vi sản phẩm hiện tại.";
    }
}
