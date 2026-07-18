using CafeChain.Application.DTOs.AppLauncher;

namespace CafeChain.Application.Interfaces.AppLauncher;

public interface IPosLaunchCoordinator
{
    Task<PosLaunchResultDTO> EnsureReadyAsync(int storeId, CancellationToken cancellationToken = default);
    Task<PosLaunchResultDTO> GetStatusAsync(int storeId, CancellationToken cancellationToken = default);
}

public interface IPrintBridgePresenceTracker
{
    void MarkConnected(int storeId, string connectionId);
    void MarkHeartbeat(int storeId, string connectionId);
    void MarkDisconnected(string connectionId);
    bool IsOnline(int storeId, TimeSpan maximumAge);
}
