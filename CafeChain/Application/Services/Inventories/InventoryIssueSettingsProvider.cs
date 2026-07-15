using System.Globalization;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Infrastrusture.Interfaces.Systems;

namespace CafeChain.Application.Services.Inventories;

public sealed class InventoryIssueSettingsProvider : IInventoryIssueSettingsProvider
{
    public const string EnabledKey = "inventory_manual_external_export_negative_enabled";
    public const string ApprovalRequiredKey = "inventory_manual_external_export_approval_required";
    public const string DefaultLimitKey = "inventory_manual_external_export_default_max_negative_quantity";
    public const string PolicyVersionKey = "inventory_manual_external_export_policy_version";

    private static readonly string[] Keys =
    [
        EnabledKey,
        ApprovalRequiredKey,
        DefaultLimitKey,
        PolicyVersionKey
    ];

    private readonly ISystemSettingRepository _repository;

    public InventoryIssueSettingsProvider(ISystemSettingRepository repository)
    {
        _repository = repository;
    }

    public async Task<InventoryManualNegativeSettings> GetManualExternalExportSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var values = await _repository.GetValuesAsync(Keys);

        if (!TryReadBool(values, EnabledKey, out var enabled)
            || !TryReadBool(values, ApprovalRequiredKey, out var approvalRequired)
            || !TryReadNonNegativeDecimal(values, DefaultLimitKey, out var defaultLimit)
            || !values.TryGetValue(PolicyVersionKey, out var policyVersion)
            || string.IsNullOrWhiteSpace(policyVersion))
        {
            return Invalid();
        }

        policyVersion = policyVersion.Trim();
        if (enabled && !approvalRequired)
            return Invalid();

        return new(true, enabled, approvalRequired, defaultLimit, policyVersion);
    }

    private static InventoryManualNegativeSettings Invalid() =>
        new(false, false, true, 0, string.Empty, InventoryIssueReasonCodes.NegativeSettingInvalid);

    private static bool TryReadBool(
        IReadOnlyDictionary<string, string> values,
        string key,
        out bool result)
    {
        result = false;
        return values.TryGetValue(key, out var value)
            && bool.TryParse(value, out result);
    }

    private static bool TryReadNonNegativeDecimal(
        IReadOnlyDictionary<string, string> values,
        string key,
        out decimal result)
    {
        result = 0;
        return values.TryGetValue(key, out var value)
            && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result)
            && result >= 0;
    }
}
