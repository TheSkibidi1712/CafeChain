using CafeChain.Models.Stores;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CafeChain.Application.Interfaces.Security
{
    public enum ScopeLevel
    {
        Country = 1,
        HQ = Country,
        Province = 2,
        Ward = 4,
        Store = 5
    }

    public interface IScopeAuthorizationService
    {
        Task<List<Store>> GetAllowedStoresAsync(int currentStaffId);
        Task<bool> CheckIfStoreIsWithinManagerScopeAsync(int currentStaffId, int targetStoreId);
        Task<bool> CanAccessStoreAsync(int currentStaffId, int targetStoreId);
    }
}
