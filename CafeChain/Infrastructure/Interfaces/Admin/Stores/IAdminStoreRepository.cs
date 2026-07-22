using CafeChain.Models.Locations;
using CafeChain.Models.Stores;

namespace CafeChain.Infrastructure.Interfaces.Admin.Stores;

public interface IAdminStoreRepository
{
    Task<List<Store>> GetAllAsync();
    Task<Store?> GetTrackedAsync(int storeId);
    Task<List<Province>> GetProvincesAsync();
    Task<bool> IsLocationHierarchyValidAsync(int provinceId, int districtId, int wardId);
    Task AddAsync(Store store);
    Task SaveChangesAsync();
}
