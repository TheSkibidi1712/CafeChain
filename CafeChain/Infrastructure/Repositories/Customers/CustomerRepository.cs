using CafeChain.Application.DTOs.Customer;
using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Customers;
using CafeChain.Models.Customers;
using CafeChain.Models.Locations;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastructure.Repositories.Customers
{
    public class CustomerRepository : ICustomerRepository
    {
        private readonly AppDbContext _context;

        public CustomerRepository(AppDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // PROFILE
        // =====================================================

        public async Task<Account?> GetCustomerProfileAccountAsync(int accountId)
        {
            return await _context.Accounts
                .Include(a => a.Customer)
                    .ThenInclude(c => c.MemberLevel)
                .Include(a => a.Customer)
                    .ThenInclude(c => c.CustomerAddresses)
                        .ThenInclude(a => a.Ward)
                            .ThenInclude(w => w.District)
                                .ThenInclude(d => d.Province)
                .Include(a => a.Customer)
                    .ThenInclude(c => c.CustomerPhones)
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    a => a.AccountId == accountId);
        }

        // =====================================================
        // CUSTOMER
        // =====================================================

        public async Task<Customer?> GetByIdAsync(int customerId)
        {
            return await _context.Customers
                .FirstOrDefaultAsync(
                    x => x.CustomerId == customerId);
        }

        public async Task<Customer?> GetByPhoneAsync(string phone)
        {
            var customerPhone =
                await _context.CustomerPhones
                    .Include(x => x.Customer)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.Phone == phone);

            return customerPhone?.Customer;
        }

        public async Task<Customer?> GetCustomerForUpdateAsync(int customerId)
        {
            return await _context.Customers
                .Include(x => x.CustomerPhones)
                .Include(x => x.CustomerAddresses)
                .FirstOrDefaultAsync(
                    x => x.CustomerId == customerId);
        }

        public Task UpdateCustomerAsync(Customer customer)
        {
            _context.Customers.Update(customer);

            return Task.CompletedTask;
        }

        // =====================================================
        // ACCOUNT
        // =====================================================

        public async Task<Account?> GetAccountByIdAsync(int accountId)
        {
            return await _context.Accounts
                .FirstOrDefaultAsync(
                    x => x.AccountId == accountId);
        }

        public Task UpdateAccountAsync(Account account)
        {
            _context.Accounts.Update(account);

            return Task.CompletedTask;
        }

        // =====================================================
        // LOCATION
        // =====================================================

        public async Task<LocationNameDto?> GetLocationNamesAsync(int provinceId, int districtId, int wardId)
        {
            return await _context.Wards
                .Where(w => w.WardId == wardId && w.DistrictId == districtId)
                .Select(w => new LocationNameDto
                {
                    WardName = w.Name,

                    DistrictName = w.District.Name,

                    ProvinceName = w.District.Province.Name
                })
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        public async Task<List<Province>> GetProvincesAsync()
        {
            return await _context.Provinces
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();
        }

        public async Task<List<District>> GetDistrictsByProvinceAsync(int provinceId)
        {
            return await _context.Districts
                .Where(x => x.ProvinceId == provinceId)
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();
        }

        public async Task<List<Ward>> GetWardsByDistrictAsync(int districtId)
        {
            return await _context.Wards
                .Where(x => x.DistrictId == districtId)
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .ToListAsync();
        }

        // =====================================================
        // AVATAR
        // =====================================================

        public Task UpdateAvatarAsync(Customer customer, string avatarUrl, string avatarPublicId)
        {
            customer.AvatarUrl = avatarUrl;
            customer.AvatarPublicId = avatarPublicId;

            return Task.CompletedTask;
        }

        // =====================================================
        // QUICK REGISTER
        // =====================================================

        public async Task<bool> PhoneExistsAsync(string phone)
        {
            return await _context.CustomerPhones.AnyAsync(x => x.Phone == phone);
        }

        public async Task AddAccountAsync(Account account)
        {
            await _context.Accounts.AddAsync(account);
        }

        public async Task AddCustomerAsync(Customer customer)
        {
            await _context.Customers.AddAsync(customer);
        }

        public async Task AddCustomerPhoneAsync(CustomerPhone customerPhone)
        {
            await _context.CustomerPhones.AddAsync(customerPhone);
        }

        // =====================================================
        // UNIT OF WORK
        // =====================================================

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            await using var transaction =
                await _context.Database
                    .BeginTransactionAsync();

            try
            {
                await action();

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
