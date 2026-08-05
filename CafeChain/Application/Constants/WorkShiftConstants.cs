namespace CafeChain.Application.Constants;

public static class WorkShiftErrorCodes
{
    public const string PosPermissionRequired = "POS_PERMISSION_REQUIRED";
    public const string StoreScopeDenied = "STORE_SCOPE_DENIED";
    public const string TerminalNotFound = "TERMINAL_NOT_FOUND";
    public const string TerminalInactive = "TERMINAL_INACTIVE";
    public const string TerminalStoreMismatch = "TERMINAL_STORE_MISMATCH";
    public const string TerminalAlreadyHasOpenShift = "TERMINAL_ALREADY_HAS_OPEN_SHIFT";
    public const string StaffAlreadyHasOpenShift = "STAFF_ALREADY_HAS_OPEN_SHIFT";
    public const string OutsideScheduleReasonRequired = "OUTSIDE_SCHEDULE_REASON_REQUIRED";
    public const string OutsideScheduleApprovalRequired = "OUTSIDE_SCHEDULE_APPROVAL_REQUIRED";
    public const string ApprovalExpired = "APPROVAL_EXPIRED";
    public const string ApprovalAlreadyUsed = "APPROVAL_ALREADY_USED";
    public const string InvalidApproverScope = "INVALID_APPROVER_SCOPE";
    public const string WorkShiftExpired = "WORKSHIFT_EXPIRED";
    public const string WorkShiftNotOpen = "WORKSHIFT_NOT_OPEN";
    public const string WorkShiftPendingClose = "WORKSHIFT_PENDING_CLOSE";
    public const string WorkShiftAlreadyClosed = "WORKSHIFT_ALREADY_CLOSED";
    public const string PaymentInProgress = "PAYMENT_IN_PROGRESS";
    public const string OfflineOrdersPending = "OFFLINE_ORDERS_PENDING";
    public const string CashDiscrepancyReasonRequired = "CASH_DISCREPANCY_REASON_REQUIRED";
    public const string CashDiscrepancyApprovalRequired = "CASH_DISCREPANCY_APPROVAL_REQUIRED";
    public const string DuplicateRequest = "DUPLICATE_REQUEST";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string InvalidRequestKey = "INVALID_REQUEST_KEY";
    public const string InvalidCashAmount = "INVALID_CASH_AMOUNT";
    public const string OutsideScheduleOfflineNotAllowed = "OUTSIDE_SCHEDULE_OFFLINE_NOT_ALLOWED";
    public const string PosOpenContextRequired = "POS_OPEN_CONTEXT_REQUIRED";
    public const string PosOpenContextInvalid = "POS_OPEN_CONTEXT_INVALID";
}
