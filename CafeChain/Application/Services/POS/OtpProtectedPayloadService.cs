using System.Text.Json;
using CafeChain.Application.Interfaces.POS;
using Microsoft.AspNetCore.DataProtection;

namespace CafeChain.Application.Services.POS;

public sealed class OtpProtectedPayloadService : IOtpProtectedPayloadService
{
    private const string Purpose = "CafeChain.OperationalOtp.NotificationReview.v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector _protector;
    private readonly IOtpCodeGenerator _codeGenerator;

    public OtpProtectedPayloadService(
        IDataProtectionProvider dataProtectionProvider,
        IOtpCodeGenerator codeGenerator)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _codeGenerator = codeGenerator;
    }

    public string Protect(
        Guid challengePublicId,
        int approverStaffId,
        string otpCode,
        DateTime expiresAtUtc)
    {
        var normalized = _codeGenerator.NormalizeAndValidate(otpCode)
            ?? throw new ArgumentException("OTP is invalid.", nameof(otpCode));
        if (challengePublicId == Guid.Empty || approverStaffId <= 0)
            throw new ArgumentOutOfRangeException(nameof(approverStaffId));

        var payload = new Payload(
            challengePublicId,
            approverStaffId,
            normalized,
            DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc));
        return _protector.Protect(JsonSerializer.Serialize(payload, JsonOptions));
    }

    public bool TryUnprotect(
        string? protectedPayload,
        Guid expectedChallengePublicId,
        int expectedApproverStaffId,
        DateTime expectedExpiresAtUtc,
        DateTime nowUtc,
        out string otpCode)
    {
        otpCode = string.Empty;
        if (string.IsNullOrWhiteSpace(protectedPayload)
            || expectedChallengePublicId == Guid.Empty
            || expectedApproverStaffId <= 0
            || expectedExpiresAtUtc <= nowUtc)
        {
            return false;
        }

        try
        {
            var json = _protector.Unprotect(protectedPayload);
            var payload = JsonSerializer.Deserialize<Payload>(json, JsonOptions);
            if (payload == null
                || payload.ChallengePublicId != expectedChallengePublicId
                || payload.ApproverStaffId != expectedApproverStaffId
                || payload.ExpiresAtUtc != DateTime.SpecifyKind(expectedExpiresAtUtc, DateTimeKind.Utc)
                || payload.ExpiresAtUtc <= nowUtc)
            {
                return false;
            }

            var normalized = _codeGenerator.NormalizeAndValidate(payload.OtpCode);
            if (normalized == null)
                return false;

            otpCode = normalized;
            return true;
        }
        catch
        {
            // Invalid/rotated ciphertext is treated as unavailable and is never logged.
            return false;
        }
    }

    private sealed record Payload(
        Guid ChallengePublicId,
        int ApproverStaffId,
        string OtpCode,
        DateTime ExpiresAtUtc);
}
