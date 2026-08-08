namespace CafeChain.Application.Interfaces.AI;

public sealed record SupplierIntelligenceFeatureState(
    bool Enabled,
    bool ShadowMode,
    bool FullRollout,
    IReadOnlySet<int> StoreAllowlist,
    string Source)
{
    public bool IsEnabledForStore(int storeId) => Enabled
        && (FullRollout || StoreAllowlist.Contains(storeId));

    public string Mode => !Enabled
        ? "OFF"
        : ShadowMode ? "SHADOW" : "ACTIVE";
}

public interface ISupplierIntelligenceFeatureGate
{
    Task<SupplierIntelligenceFeatureState> GetStateAsync(
        CancellationToken cancellationToken = default);
}
