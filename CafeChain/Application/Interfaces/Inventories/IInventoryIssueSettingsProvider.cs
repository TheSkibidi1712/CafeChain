using CafeChain.Application.DTOs.Inventories;

namespace CafeChain.Application.Interfaces.Inventories;

public interface IInventoryIssueSettingsProvider
{
    Task<InventoryManualNegativeSettings> GetManualExternalExportSettingsAsync(
        CancellationToken cancellationToken = default);
}
