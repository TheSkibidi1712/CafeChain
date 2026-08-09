using CafeChain.Application.Constants;
using CafeChain.Models.Operations;

namespace CafeChain.Application.Services.POS;

/// <summary>Single source of truth for operational OTP failure lock and resend recovery.</summary>
public static class OperationalOtpChallengePolicy
{
    public static DateTime? GetLockedUntilUtc(OtpChallenge challenge) =>
        challenge.LockedAt?.AddSeconds(OtpConstants.LockoutCooldownSeconds);

    public static int GetRetryAfterSeconds(OtpChallenge challenge, DateTime nowUtc)
    {
        var until = GetLockedUntilUtc(challenge);
        return until.HasValue
            ? Math.Max(0, (int)Math.Ceiling((until.Value - nowUtc).TotalSeconds))
            : 0;
    }

    public static bool RegisterFailedAttempt(OtpChallenge challenge, DateTime nowUtc)
    {
        challenge.FailedAttempts++;
        if (challenge.FailedAttempts < OtpConstants.MaxFailedAttempts) return false;
        challenge.Status = OtpConstants.Statuses.Locked;
        challenge.LockedAt = nowUtc;
        challenge.ProtectedOtpPayload = null;
        return true;
    }

    public static bool CanResend(OtpChallenge challenge, DateTime nowUtc, out int retryAfter)
    {
        var availableAt = challenge.Status == OtpConstants.Statuses.Locked
            ? GetLockedUntilUtc(challenge) ?? nowUtc.AddSeconds(OtpConstants.LockoutCooldownSeconds)
            : challenge.LastSentAt.AddSeconds(OtpConstants.ResendCooldownSeconds);
        retryAfter = Math.Max(0, (int)Math.Ceiling((availableAt - nowUtc).TotalSeconds));
        return retryAfter == 0;
    }

    public static void ResetAfterResend(OtpChallenge challenge)
    {
        challenge.Status = OtpConstants.Statuses.Pending;
        challenge.FailedAttempts = 0;
        challenge.LockedAt = null;
        challenge.ApprovedAt = null;
        challenge.CancelledAt = null;
    }
}
