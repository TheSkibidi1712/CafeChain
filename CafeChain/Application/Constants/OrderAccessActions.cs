namespace CafeChain.Application.Constants;

public static class OrderAccessActions
{
    public const string AdminList = "ORDER_ADMIN_LIST";
    public const string AdminDetail = "ORDER_ADMIN_DETAIL";
    public const string AdminExport = "ORDER_ADMIN_EXPORT";
    public const string PosHistory = "ORDER_POS_HISTORY";
    public const string Reprint = "ORDER_REPRINT";
    public const string RefundRequest = "ORDER_REFUND_REQUEST";
    public const string RefundConfirm = "ORDER_REFUND_CONFIRM";
    public const string OfflineSync = "ORDER_OFFLINE_SYNC";
}

public static class OrderAccessErrorCodes
{
    public const string Forbidden = "ORDER_ACCESS_FORBIDDEN";
    public const string NotFound = "ORDER_NOT_FOUND";
    public const string WorkShiftNotFound = "WORK_SHIFT_NOT_FOUND";
    public const string OfflineAttributionMismatch = "OFFLINE_ATTRIBUTION_MISMATCH";
}
