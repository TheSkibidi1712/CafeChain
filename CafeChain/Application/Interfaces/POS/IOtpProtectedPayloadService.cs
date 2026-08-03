namespace CafeChain.Application.Interfaces.POS;

public interface IOtpProtectedPayloadService
{
    string Protect(Guid challengePublicId, int approverStaffId, string otpCode, DateTime expiresAtUtc);

    bool TryUnprotect(
        string? protectedPayload,
        Guid expectedChallengePublicId,
        int expectedApproverStaffId,
        DateTime expectedExpiresAtUtc,
        DateTime nowUtc,
        out string otpCode);
}
