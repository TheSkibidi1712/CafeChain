using CafeChain.Application.DTOs.Admin.Settings;

namespace CafeChain.ViewModels.Admin.Settings;

public sealed class AdminSettingIndexVM
{
    public NegativeInventorySettingsDTO? NegativeInventory { get; init; }
    public AIImportOcrSettingsDTO? Ocr { get; init; }
    public string ActiveTab { get; init; } = "negative-stock";
}
