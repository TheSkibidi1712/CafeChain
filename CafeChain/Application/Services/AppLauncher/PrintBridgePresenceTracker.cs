using System.Collections.Concurrent;
using CafeChain.Application.Interfaces.AppLauncher;

namespace CafeChain.Application.Services.AppLauncher;

public sealed class PrintBridgePresenceTracker : IPrintBridgePresenceTracker
{
    private readonly ConcurrentDictionary<string, Presence> _connections = new();

    public void MarkConnected(int storeId, string connectionId) =>
        _connections[connectionId] = new Presence(storeId, DateTimeOffset.UtcNow);

    public void MarkHeartbeat(int storeId, string connectionId)
    {
        if (!_connections.TryGetValue(connectionId, out var current) || current.StoreId != storeId)
            return;

        _connections.TryUpdate(
            connectionId,
            current with { LastSeen = DateTimeOffset.UtcNow },
            current);
    }

    public void MarkDisconnected(string connectionId) => _connections.TryRemove(connectionId, out _);

    public bool IsOnline(int storeId, TimeSpan maximumAge)
    {
        var threshold = DateTimeOffset.UtcNow - maximumAge;
        foreach (var pair in _connections)
        {
            if (pair.Value.LastSeen < threshold)
            {
                _connections.TryRemove(pair.Key, out _);
                continue;
            }

            if (pair.Value.StoreId == storeId)
                return true;
        }

        return false;
    }

    private sealed record Presence(int StoreId, DateTimeOffset LastSeen);
}
