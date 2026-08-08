using CafeChain.Application.Interfaces.AI;
using CafeChain.Application.Options;
using CafeChain.Infrastrusture.Interfaces.Systems;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.AI;

public sealed class SupplierIntelligenceFeatureGate : ISupplierIntelligenceFeatureGate
{
    public const string EnabledKey = "supplier_intelligence_enabled";
    public const string ShadowModeKey = "supplier_intelligence_shadow_mode";
    public const string FullRolloutKey = "supplier_intelligence_full_rollout";
    public const string StoreAllowlistKey = "supplier_intelligence_store_allowlist";

    private static readonly string[] Keys =
    [
        EnabledKey,
        ShadowModeKey,
        FullRolloutKey,
        StoreAllowlistKey
    ];

    private readonly ISystemSettingRepository _settings;
    private readonly SupplierIntelligenceOptions _fallback;

    public SupplierIntelligenceFeatureGate(
        ISystemSettingRepository settings,
        IOptions<SupplierIntelligenceOptions> fallback)
    {
        _settings = settings;
        _fallback = fallback.Value;
    }

    public async Task<SupplierIntelligenceFeatureState> GetStateAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var values = await _settings.GetValuesAsync(Keys);
        cancellationToken.ThrowIfCancellationRequested();

        if (values.Count == 0)
        {
            return new SupplierIntelligenceFeatureState(
                _fallback.Enabled || _fallback.ScoringEnabled,
                _fallback.ShadowMode,
                _fallback.FullRollout,
                _fallback.StoreAllowlist.ToHashSet(),
                "CONFIGURATION");
        }

        return new SupplierIntelligenceFeatureState(
            ParseBoolean(values, EnabledKey, defaultValue: false),
            ParseBoolean(values, ShadowModeKey, defaultValue: true),
            ParseBoolean(values, FullRolloutKey, defaultValue: false),
            ParseStoreAllowlist(values.GetValueOrDefault(StoreAllowlistKey)),
            "SYSTEM_SETTINGS");
    }

    private static bool ParseBoolean(
        IReadOnlyDictionary<string, string> values,
        string key,
        bool defaultValue)
    {
        return values.TryGetValue(key, out var raw)
            && bool.TryParse(raw, out var parsed)
                ? parsed
                : defaultValue;
    }

    private static IReadOnlySet<int> ParseStoreAllowlist(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new HashSet<int>();

        return raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => int.TryParse(value, out var storeId) ? storeId : 0)
            .Where(storeId => storeId > 0)
            .ToHashSet();
    }
}
