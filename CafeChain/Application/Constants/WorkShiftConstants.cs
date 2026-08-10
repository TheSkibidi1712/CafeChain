namespace CafeChain.Application.Constants;

public static class WorkShiftErrorCodes
{
    public const string PosPermissionRequired = "POS_PERMISSION_REQUIRED";
    public const string StoreScopeDenied = "STORE_SCOPE_DENIED";
    public const string TerminalNotFound = "TERMINAL_NOT_FOUND";
    public const string TerminalInactive = "TERMINAL_INACTIVE";
    public const string TerminalStoreMismatch = "TERMINAL_STORE_MISMATCH";
    public const string TerminalAlreadyHasOpenShift = "TERMINAL_ALREADY_HAS_OPEN_SHIFT";
    public const string WorkShiftTerminalMismatch = "WORKSHIFT_TERMINAL_MISMATCH";
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
    public const string StaffHubOpenRequired = "STAFFHUB_OPEN_REQUIRED";
    public const string OpeningCashRequired = "OPENING_CASH_REQUIRED";
    public const string LateOpenApprovalPending = "LATE_OPEN_APPROVAL_PENDING";
    public const string LateOpenApprovalRejected = "LATE_OPEN_APPROVAL_REJECTED";
    public const string LateOpenApprovalExpired = "LATE_OPEN_APPROVAL_EXPIRED";
    public const string LateOpenRequiresOutsideSchedule = "LATE_OPEN_REQUIRES_OUTSIDE_SCHEDULE";
    public const string PosSessionRevoked = "POS_SESSION_REVOKED";
    public const string PosSessionEnded = "POS_SESSION_ENDED";
    public const string PosSessionExpired = "POS_SESSION_EXPIRED";
    public const string PosTerminalLocked = "POS_TERMINAL_LOCKED";
    public const string OperatorNotAuthorized = "OPERATOR_NOT_AUTHORIZED";
    public const string OperatorPinNotConfigured = "OPERATOR_PIN_NOT_CONFIGURED";
    public const string OperatorPinInvalid = "OPERATOR_PIN_INVALID";
    public const string OperatorPinLocked = "OPERATOR_PIN_LOCKED";
    public const string PosAccessDenied = "POS_ACCESS_DENIED";
    public const string ShiftNotOpened = "SHIFT_NOT_OPENED";
    public const string ShiftAlreadyClosed = "SHIFT_ALREADY_CLOSED";
    public const string ShiftTooEarly = "SHIFT_TOO_EARLY";
    public const string ShiftScheduleChanged = "SHIFT_SCHEDULE_CHANGED";
    public const string TerminalApprovalNotFound = "TERMINAL_APPROVAL_NOT_FOUND";
    public const string TerminalAlreadyApproved = "TERMINAL_ALREADY_APPROVED";
    public const string TerminalNotPending = "TERMINAL_NOT_PENDING";
    public const string TerminalApprovalForbidden = "TERMINAL_APPROVAL_FORBIDDEN";
    public const string TerminalStoreScopeInvalid = "TERMINAL_STORE_SCOPE_INVALID";
    public const string TerminalApprovalConflict = "TERMINAL_APPROVAL_CONFLICT";
    public const string TerminalRejectionForbidden = "TERMINAL_REJECTION_FORBIDDEN";
    public const string TerminalAlreadyRejected = "TERMINAL_ALREADY_REJECTED";
    public const string TerminalRejectionReasonInvalid = "TERMINAL_REJECTION_REASON_INVALID";
}

public static class WorkShiftOpenResultCodes
{
    public const string OpenedNewWorkShift = "OPENED_NEW_WORKSHIFT";
    public const string ResumeExistingWorkShift = "RESUME_EXISTING_WORKSHIFT";
    public const string OpeningCashConfirmed = "OPENING_CASH_CONFIRMED";
    public const string WorkShiftClosingStarted = "WORKSHIFT_CLOSING_STARTED";
    public const string WorkShiftClosed = "WORKSHIFT_CLOSED";
    public const string WorkShiftReconciliationRequired = "WORKSHIFT_RECONCILIATION_REQUIRED";
    public const string WorkShiftReconciled = "WORKSHIFT_RECONCILED";
}

public static class WorkShiftRecommendedActions
{
    public const string OpenStaffHub = "OPEN_STAFFHUB";
    public const string EnterOpeningCash = "ENTER_OPENING_CASH";
    public const string ContinuePos = "CONTINUE_POS";
    public const string ResumeExistingWorkShift = "RESUME_EXISTING_WORKSHIFT";
    public const string SwitchCurrentOperator = "SWITCH_CURRENT_OPERATOR";
    public const string CompleteClosing = "COMPLETE_CLOSING";
    public const string CountAndClose = "COUNT_AND_CLOSE";
}
