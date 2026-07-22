using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.Stores;
using CafeChain.Models.Locations;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastructure.Repositories.Admin.Stores;

public sealed class AdminStoreRepository : IAdminStoreRepository
{
    private readonly AppDbContext _context;
    public AdminStoreRepository(AppDbContext context) => _context = context;

    public Task<List<Store>> GetAllAsync() => _context.Stores
        .AsNoTracking()
        .Include(x => x.Province).Include(x => x.District).Include(x => x.Ward)
        .Include(x => x.Staffs).ThenInclude(x => x.Account).ThenInclude(x => x.AccountRoles)
        .ThenInclude(x => x.Role)
        .OrderByDescending(x => x.CreatedAt)
        .ToListAsync();

    public Task<Store?> GetTrackedAsync(int storeId) =>
        _context.Stores.FirstOrDefaultAsync(x => x.StoreId == storeId);

    public Task<List<Province>> GetProvincesAsync() =>
        _context.Provinces.AsNoTracking().OrderBy(x => x.Name).ToListAsync();

    public Task<bool> IsLocationHierarchyValidAsync(int provinceId, int districtId, int wardId) =>
        _context.Wards.AnyAsync(w => w.WardId == wardId
            && w.DistrictId == districtId
            && w.District.ProvinceId == provinceId);

    public Task AddAsync(Store store) => _context.Stores.AddAsync(store).AsTask();

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
