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

        /// <summary>Constant-time equality for fingerprint strings.</summary>
        bool FixedTimeEquals(string? a, string? b);
    }
}
