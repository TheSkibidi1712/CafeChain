using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.StaffHub;

public class StaffHubPosPreviewRequestDto
{
    [Required, StringLength(100)]
    public string TerminalId { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string RequestKey { get; set; } = string.Empty;
}

public sealed class StaffHubIssuePosRequestDto : StaffHubPosPreviewRequestDto
{
    [StringLength(500)]
    public string? Reason { get; set; }

    public Guid? OtpChallengePublicId { get; set; }
}

public sealed class StaffHubOpenOtpRequestDto : StaffHubPosPreviewRequestDto
{
    [Required, StringLength(500, MinimumLength = 10)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class StaffHubTerminalRegistrationRequestDto
{
    [Required, StringLength(100)]
    public string TerminalId { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string TerminalName { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string RequestKey { get; set; } = string.Empty;

    public Guid OtpChallengePublicId { get; set; }
}

public sealed class StaffHubTerminalOtpRequestDto
{
    [Required, StringLength(100)]
    public string TerminalId { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string TerminalName { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string RequestKey { get; set; } = string.Empty;
}
