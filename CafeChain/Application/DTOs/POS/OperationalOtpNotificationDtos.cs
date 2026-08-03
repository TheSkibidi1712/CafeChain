namespace CafeChain.Application.DTOs.POS;

/// <summary>
/// Ephemeral payload sent only to the selected approver's private SignalR group.
/// OtpCode must never be persisted or logged.
/// </summary>
public sealed record OperationalOtpIssuedDto(
    string EventId,
    string OtpCode,
    DateTime ExpiresAtUtc,
    string ActionLabel,
    string RequesterName,
    string StoreName);

/// <summary>Sanitized badge/list refresh event. Contains no OTP or request secret.</summary>
public sealed record OperationalOtpNotificationChangedDto(
    string EventId,
    int NotificationId,
    string ChangeKind,
    DateTime OccurredAtUtc);
