using System.Collections.Generic;
using System.Threading.Tasks;
using CafeChain.Application.Results;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Settings;

namespace CafeChain.Application.Interfaces.Admin.Settings
{
    public interface IAdminSettingService
    {
        Task<ServiceResult<NegativeInventorySettingsDTO>> GetNegativeInventorySettingsAsync(
            AdminActorContext actor,
            CancellationToken cancellationToken = default);
        Task<ServiceResult<NegativeInventorySettingsUpdateResultDTO>> UpdateNegativeInventorySettingsAsync(
            UpdateNegativeInventorySettingsDTO request,
            AdminActorContext actor,
            CancellationToken cancellationToken = default);
    }
}
