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
        District = 3,
        Ward = 4,
        Store = 5
    }

    /// <summary>
    /// Identifies the business boundary requesting store access.
    /// Default never grants a role-based global-store bypass.
    /// </summary>
    public enum StoreScopePurpose
    {
        Default = 0,
        ReorderSuggestion = 1
    }

    public interface IScopeAuthorizationService
    {
        Task<List<Store>> GetAllowedStoresAsync(int currentStaffId);
        Task<List<Store>> GetAllowedStoresAsync(
            int currentStaffId,
            StoreScopePurpose purpose) =>
            purpose == StoreScopePurpose.Default
                ? GetAllowedStoresAsync(currentStaffId)
                : Task.FromResult(new List<Store>());

        Task<bool> CheckIfStoreIsWithinManagerScopeAsync(int currentStaffId, int targetStoreId);
        Task<bool> CanAccessStoreAsync(int currentStaffId, int targetStoreId);
        Task<bool> CanAccessStoreAsync(
            int currentStaffId,
            int targetStoreId,
            StoreScopePurpose purpose) =>
            purpose == StoreScopePurpose.Default
                ? CanAccessStoreAsync(currentStaffId, targetStoreId)
                : Task.FromResult(false);
    }
}
