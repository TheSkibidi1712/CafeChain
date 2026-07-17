using CafeChain.Application.DTOs.AppLauncher;

namespace CafeChain.Application.Interfaces.AppLauncher;

public interface IAppLauncherService
{
    Task<AppLauncherVM> GetAppsAsync(
        int accountId,
        string? displayName,
        CancellationToken cancellationToken = default);
}
