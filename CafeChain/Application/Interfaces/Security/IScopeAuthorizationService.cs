using CafeChain.Models.Stores;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CafeChain.Application.Interfaces.Security
{
    public enum ScopeLevel
    {
        HQ = 1,
        Province = 2,
        Ward = 3,
        Store = 4
    }

    public interface IScopeAuthorizationService
    {
        Task<List<Store>> GetAllowedStoresAsync(int currentStaffId);
        Task<bool> CheckIfStoreIsWithinManagerScopeAsync(int currentStaffId, int targetStoreId);
    }
}
