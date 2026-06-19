using System.Collections.Generic;
using System.Threading.Tasks;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Admin.Settings
{
    public interface IAdminSettingService
    {
        Task<Dictionary<string, string>> GetSettingsDictionaryAsync();
        Task<ServiceResult> SaveSettingsAsync(Dictionary<string, string> settings);
    }
}
