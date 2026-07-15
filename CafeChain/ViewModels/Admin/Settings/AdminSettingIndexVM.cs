using CafeChain.Application.DTOs.Admin.Settings;

namespace CafeChain.ViewModels.Admin.Settings;

public sealed class AdminSettingIndexVM
{
    public Dictionary<string, string> Settings { get; init; } = [];
    public bool CanManageNegativeInventory { get; init; }
    public NegativeInventorySettingsDTO? NegativeInventory { get; init; }
}
