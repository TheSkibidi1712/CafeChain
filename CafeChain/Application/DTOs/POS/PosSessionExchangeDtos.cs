using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.POS;

public sealed class PosSessionExchangeRequestDto
{
    [Required, StringLength(200, MinimumLength = 32)]
    public string ExchangeCode { get; set; } = string.Empty;
}

public static class PosSessionPurposes
{
    public const string OpenWorkShift = "OPEN_WORKSHIFT";
    public const string ResumeWorkShift = "RESUME_WORKSHIFT";
}

public static class PosSessionExchangeErrorCodes
{
    public const string Expired = "POS_EXCHANGE_CODE_EXPIRED";
    public const string AlreadyUsed = "POS_EXCHANGE_CODE_ALREADY_USED";
    public const string Invalid = "POS_EXCHANGE_CODE_INVALID";
    public const string ContextRequired = "POS_OPEN_CONTEXT_REQUIRED";
    public const string ContextInvalid = "POS_OPEN_CONTEXT_INVALID";
}

public sealed class PosSessionExchangeContextDto
{
    public string Purpose { get; set; } = PosSessionPurposes.OpenWorkShift;
    public int AccountId { get; set; }
    public int StaffId { get; set; }
    public int StoreId { get; set; }
    public string? TerminalId { get; set; }
    public string? RequestKey { get; set; }
    public string? OpenContext { get; set; }
    public int? SourceStaffShiftId { get; set; }
    public DateTime? PlannedStartUtc { get; set; }
    public DateTime? PlannedEndUtc { get; set; }
    public string? Reason { get; set; }
    public Guid? OtpChallengePublicId { get; set; }
    public int? WorkShiftId { get; set; }
}

public sealed record PosSessionExchangeTicketDto(string ExchangeCode, DateTime ExpiresAtUtc, int ContextId);

public sealed record PosSessionTokenDto(string Token, DateTime ExpiresAtUtc, int ContextId, string Purpose);
