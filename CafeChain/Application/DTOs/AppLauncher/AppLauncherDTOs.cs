namespace CafeChain.Application.DTOs.AppLauncher;

public enum AppCode
{
    AdminDashboard = 1,
    StaffHub = 2,
    Pos = 3
}

public sealed class AppLauncherCardDTO
{
    public AppCode Code { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public string Route { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public bool IsAvailable { get; init; }
    public string? DenialReason { get; init; }
}

public sealed class AppLauncherVM
{
    public string DisplayName { get; init; } = string.Empty;
    public IReadOnlyList<AppLauncherCardDTO> Apps { get; init; } = [];
    public bool HasAvailableApps => Apps.Any(x => x.IsAvailable);
}
