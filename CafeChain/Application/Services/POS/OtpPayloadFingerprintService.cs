using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CafeChain.Application.Constants;
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
            var cash = actualEndingCash.ToString("0.00", CultureInfo.InvariantCulture);

            // Canonical lines — fixed order, invariant culture, no raw JSON.
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

            var bytes = Encoding.UTF8.GetBytes(canonical);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
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

        private static string NormalizeReason(string? reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return string.Empty;

            // Trim ends; collapse internal whitespace runs to single space; keep case as-is after trim.
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
