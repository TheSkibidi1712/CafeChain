namespace CafeChain.Application.Constants;

public static class OperationalIcePermissions
{
    public const string View = "OperationalIce.View";
    public const string Manage = "OperationalIce.Manage";
    public const string Approve = "OperationalIce.Approve";
    public const string Policy = "OperationalIce.Policy";
}

public static class OperationalIceStatuses
{
    public const string Draft = "Draft";
    public const string Open = "Open";
    public const string PendingApproval = "PendingApproval";
    public const string ReconciliationRequired = "ReconciliationRequired";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Draft, Open, PendingApproval, ReconciliationRequired, Closed, Cancelled];
}

public static class OperationalIceCreationSources
{
    public const string Manual = "Manual";
    public const string StaffSchedule = "StaffSchedule";
    public static readonly string[] All = [Manual, StaffSchedule];
}

public static class IceSupplementalIssueStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
}

public static class IceCarryOverStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
}

public static class IcePostingTypes
{
    public const string VarianceOut = "VarianceOut";
}

public static class IceReturnConditions
{
    public const string SealedIntact = "SEALED_INTACT";
}

public static class IceCostSnapshotStatuses
{
    public const string Available = "Available";
    public const string Missing = "Missing";
}

public static class OperationalIceErrorCodes
{
    public const string Forbidden = "OPERATIONAL_ICE_FORBIDDEN";
    public const string StoreScopeForbidden = "OPERATIONAL_ICE_STORE_SCOPE_FORBIDDEN";
    public const string NotFound = "OPERATIONAL_ICE_NOT_FOUND";
    public const string InvalidRequest = "OPERATIONAL_ICE_INVALID_REQUEST";
    public const string InvalidState = "OPERATIONAL_ICE_INVALID_STATE";
    public const string InsufficientUsableStock = "OPERATIONAL_ICE_INSUFFICIENT_USABLE_STOCK";
    public const string WorkShiftAlreadyLinked = "OPERATIONAL_ICE_WORKSHIFT_ALREADY_LINKED";
    public const string ConcurrencyConflict = "OPERATIONAL_ICE_CONCURRENCY_CONFLICT";
    public const string ScheduleShiftAlreadyUsed = "OPERATIONAL_ICE_SCHEDULE_SHIFT_ALREADY_USED";
}
