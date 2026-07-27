namespace CafeChain.Application.Interfaces.Admin.Staffs;

public interface IStaffScheduleGapNotificationService
{
    Task<StaffScheduleGapScanResult> ScanStoreAsync(
        int storeId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);
}

public sealed record StaffScheduleGapScanResult(
    int StoreId,
    int AlertsCreated,
    int AlertsUpdated,
    int AlertsResolved,
    int MissingRequirementCount);
