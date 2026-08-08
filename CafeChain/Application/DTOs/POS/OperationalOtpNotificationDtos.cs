namespace CafeChain.Application.DTOs.POS;

/// <summary>Sanitized badge/list refresh event. Contains no OTP or request secret.</summary>
public sealed record OperationalOtpNotificationChangedDto(
    string EventId,
    int NotificationId,
    string ChangeKind,
    DateTime OccurredAtUtc);

/// <summary>Sanitized requester-side state event for terminal registration.</summary>
public sealed record TerminalRegistrationChangedDto(
    Guid OtpChallengePublicId,
    string Status,
    string? TerminalId,
    DateTime ExpiresAtUtc,
    DateTime ServerNowUtc);
