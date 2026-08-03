using CafeChain.Application.DTOs.POS;

namespace CafeChain.Application.Interfaces.POS
{
    public interface IOtpPayloadFingerprintService
    {
        string BuildCashDifferenceFingerprint(
            int storeId,
            int actorStaffId,
            int workShiftId,
            decimal actualEndingCash,
            string reason);

        string BuildCloseShiftExceptionFingerprint(
            int storeId,
            int actorStaffId,
            int workShiftId,
            decimal actualEndingCash,
            string exceptionReason,
            string? discrepancyReason,
            OfflineQueueSummaryDto offlineSummary);

        string BuildOpenShiftLateFingerprint(
            int storeId,
            int actorStaffId,
            decimal startingCash,
            string reason,
            string scheduledStartCanonical);

        string BuildOpenShiftBoundFingerprint(
            int storeId,
            int actorStaffId,
            decimal startingCash,
            string reason,
            string scheduledStartCanonical,
            string actionType,
            string? terminalId,
            string? requestKey);

        /// <summary>Constant-time equality for fingerprint strings.</summary>
        bool FixedTimeEquals(string? a, string? b);
    }
}
