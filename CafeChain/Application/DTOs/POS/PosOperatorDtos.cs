using System.ComponentModel.DataAnnotations;

namespace CafeChain.Application.DTOs.POS;

public sealed class SetOperatorPinRequestDto
{
    [Required, MinLength(1), MaxLength(200)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, RegularExpression("^[0-9]{6}$")]
    public string Pin { get; set; } = string.Empty;
}

public sealed class SwitchOperatorRequestDto
{
    [Range(1, int.MaxValue)]
    public int OperatorStaffId { get; set; }

    [Required, RegularExpression("^[0-9]{6}$")]
    public string Pin { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string RequestKey { get; set; } = string.Empty;

    public string? RowVersion { get; set; }
}

public sealed class PosOperatorCandidateDto
{
    public int StaffId { get; set; }
    public string FullName { get; set; } = string.Empty;
}
