namespace CafeChain.Application.DTOs.Admin.Profiles;

public sealed class AdminProfileUpdateResult
{
    public string AvatarUrl { get; init; } = string.Empty;
    public bool AvatarChanged { get; init; }
}
