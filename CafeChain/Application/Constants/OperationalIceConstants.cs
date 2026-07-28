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

public static class IceCostSnapshotStatuses
{
    public const string Available = "Available";
    public const string Missing = "Missing";
}
