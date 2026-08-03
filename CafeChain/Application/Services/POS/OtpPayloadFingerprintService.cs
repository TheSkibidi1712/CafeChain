using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;

namespace CafeChain.Application.Services.POS
{
    public sealed class OtpPayloadFingerprintService : IOtpPayloadFingerprintService
    {
        public string BuildCashDifferenceFingerprint(
            int storeId,
            int actorStaffId,
            int workShiftId,
            decimal actualEndingCash,
            string reason)
        {
            var normalizedReason = NormalizeReason(reason);
            var cash = FormatDecimal(actualEndingCash);

            var canonical = string.Join('\n', new[]
            {
                "version=1",
                $"action={OtpConstants.ActionTypes.CashDifference}",
                $"store={storeId}",
                $"actor={actorStaffId}",
                $"targetType={OtpConstants.TargetTypes.Shifts}",
                $"targetId={workShiftId}",
                $"workShiftId={workShiftId}",
                $"actualEndingCash={cash}",
                $"reason={normalizedReason}"
            });

            return Sha256Hex(canonical);
        }

        public string BuildCloseShiftExceptionFingerprint(
            int storeId,
            int actorStaffId,
            int workShiftId,
            decimal actualEndingCash,
            string exceptionReason,
            string? discrepancyReason,
            OfflineQueueSummaryDto offlineSummary)
        {
            offlineSummary ??= new OfflineQueueSummaryDto();
            var cash = FormatDecimal(actualEndingCash);
            var estimated = FormatDecimal(offlineSummary.EstimatedTotal);
            var localCash = FormatDecimal(offlineSummary.LocalCashTotal);

            var canonical = string.Join('\n', new[]
            {
                "version=1",
                $"action={OtpConstants.ActionTypes.CloseShiftException}",
                $"store={storeId}",
                $"actor={actorStaffId}",
                $"targetType={OtpConstants.TargetTypes.Shifts}",
                $"targetId={workShiftId}",
                $"workShiftId={workShiftId}",
                $"actualEndingCash={cash}",
                $"exceptionReason={NormalizeReason(exceptionReason)}",
                $"discrepancyReason={NormalizeReason(discrepancyReason)}",
                $"offlineOrderCount={offlineSummary.OfflineOrderCount}",
                $"estimatedTotal={estimated}",
                $"localCashTotal={localCash}"
            });

            return Sha256Hex(canonical);
        }

        public string BuildOpenShiftLateFingerprint(
            int storeId,
            int actorStaffId,
            decimal startingCash,
            string reason,
            string scheduledStartCanonical) =>
            BuildOpenShiftBoundFingerprint(storeId, actorStaffId, startingCash, reason,
                scheduledStartCanonical, OtpConstants.ActionTypes.OpenShiftLate, null, null);

        public string BuildOpenShiftBoundFingerprint(
            int storeId,
            int actorStaffId,
            decimal startingCash,
            string reason,
            string scheduledStartCanonical,
            string actionType,
            string? terminalId,
            string? requestKey)
        {
            var cash = FormatDecimal(startingCash);
            var scheduled = string.IsNullOrWhiteSpace(scheduledStartCanonical)
                ? string.Empty
                : scheduledStartCanonical.Trim();

            // targetId = actor staff (no WorkShift yet at request time).
            var canonicalParts = new List<string>
            {
                "version=1",
                $"action={actionType}",
                $"store={storeId}",
                $"actor={actorStaffId}",
                $"targetType={OtpConstants.TargetTypes.Shifts}",
                $"targetId={actorStaffId}",
                $"staffId={actorStaffId}",
                $"startingCash={cash}",
                $"reason={NormalizeReason(reason)}",
                $"scheduledStart={scheduled}",
                $"lateThresholdMinutes={OtpConstants.LateOpenThresholdMinutes}"
            };
            if (!string.IsNullOrWhiteSpace(terminalId))
                canonicalParts.Add($"terminalId={terminalId.Trim()}");
            if (!string.IsNullOrWhiteSpace(requestKey))
                canonicalParts.Add($"requestKey={requestKey.Trim()}");
            var canonical = string.Join('\n', canonicalParts);

            return Sha256Hex(canonical);
        }

        public bool FixedTimeEquals(string? a, string? b)
        {
            if (a is null || b is null)
                return false;

            var ba = Encoding.UTF8.GetBytes(a);
            var bb = Encoding.UTF8.GetBytes(b);
            if (ba.Length != bb.Length)
                return false;

            return CryptographicOperations.FixedTimeEquals(ba, bb);
        }

        private static string FormatDecimal(decimal value)
            => value.ToString("0.00", CultureInfo.InvariantCulture);

        private static string Sha256Hex(string canonical)
        {
            var bytes = Encoding.UTF8.GetBytes(canonical);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }

        private static string NormalizeReason(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return string.Empty;

            var trimmed = reason.Trim();
            var sb = new StringBuilder(trimmed.Length);
            var prevSpace = false;
            foreach (var ch in trimmed)
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!prevSpace)
                    {
                        sb.Append(' ');
                        prevSpace = true;
                    }
                }
                else
                {
                    sb.Append(ch);
                    prevSpace = false;
                }
            }

            return sb.ToString();
        }
    }
}
