using CafeChain.Models.Enums.Inventory;

namespace CafeChain.Application.DTOs.POS
{
    public sealed class RequestFullOrderRefundDto
    {
        public int OrderId { get; set; }
        public Guid? RefundKey { get; set; }
        public string? Reason { get; set; }
    }

    public sealed class ConfirmCashRefundDto
    {
        public int OrderRefundId { get; set; }
        public bool CashReturnedToCustomer { get; set; }
        public string? Reason { get; set; }
    }

    public sealed class OrderRefundResultDto
    {
        public int OrderRefundId { get; set; }
        public int OrderId { get; set; }
        public int StoreId { get; set; }
        public Guid RefundKey { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal RefundAmount { get; set; }
        public string CostStatus { get; set; } = string.Empty;
        public decimal? ReversedCogs { get; set; }
        public string InventoryReversalStatus { get; set; } = string.Empty;
        public bool WasReplay { get; set; }
        public string MessageKey { get; set; } = string.Empty;
        public DateTime? CompletedAtUtc { get; set; }
    }

    public static class OrderRefundFailureCodes
    {
        public const string InvalidRequest = "INVALID_REQUEST";
        public const string OrderNotFound = "ORDER_NOT_FOUND";
        public const string StoreUnauthorized = "STORE_UNAUTHORIZED";
        public const string RoleUnauthorized = "ROLE_UNAUTHORIZED";
        public const string InvalidOrderStatus = "INVALID_ORDER_STATUS";
        public const string InvalidPaymentStatus = "INVALID_PAYMENT_STATUS";
        public const string PaymentProviderNotSupported = "REFUND_PAYMENT_PROVIDER_NOT_SUPPORTED";
        public const string LoyaltyReversalNotSupported = "REFUND_LOYALTY_REVERSAL_NOT_SUPPORTED";
        public const string PartialAmountRejected = "REFUND_PARTIAL_AMOUNT_REJECTED";
        public const string RefundKeyReused = "REFUND_KEY_REUSED";
        public const string AlreadyRefunded = "ORDER_ALREADY_REFUNDED";
        public const string RefundActive = "REFUND_ALREADY_ACTIVE";
        public const string CashConfirmRequired = "CASH_CONFIRM_REQUIRED";
        public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
        public const string ExecutionFailed = "REFUND_EXECUTION_FAILED";
        public const string RefundNotFound = "REFUND_NOT_FOUND";
    }
}
