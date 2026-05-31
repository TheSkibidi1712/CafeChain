using CafeChain.Application.DTOs.Customer;
using CafeChain.Models.Customers;
using CafeChain.Models.Locations;

namespace CafeChain.Infrastructure.Interfaces.Customers
{
    public interface ICustomerRepository
    {
        // =========================
        // PROFILE
        // =========================

        Task<Account?> GetCustomerProfileAccountAsync(int accountId);

        // =========================
        // CUSTOMER
        // =========================

        Task<Customer?> GetByIdAsync(int customerId);

        Task<Customer?> GetByPhoneAsync(string phone);

        Task<Customer?> GetCustomerForUpdateAsync(int customerId);
        Task UpdateCustomerAsync(Customer customer);

        // =========================
        // ACCOUNT
        // =========================

        Task<Account?> GetAccountByIdAsync(int accountId);
        Task UpdateAccountAsync(Account account);

        // =========================
        // LOCATION
        // =========================

        Task<LocationNameDto?> GetLocationNamesAsync(int provinceId, int districtId, int wardId);

        Task<List<Province>> GetProvincesAsync();

        Task<List<District>> GetDistrictsByProvinceAsync(int provinceId);

        Task<List<Ward>> GetWardsByDistrictAsync(int districtId);

        // =========================
        // AVATAR
        // =========================

        Task UpdateAvatarAsync(Customer customer, string avatarUrl, string avatarPublicId);

        // =========================
        // QUICK REGISTER
        // =========================

        Task<bool> PhoneExistsAsync(string phone);

        Task AddAccountAsync(Account account);

        Task AddCustomerAsync(Customer customer);

        Task AddCustomerPhoneAsync(CustomerPhone customerPhone);

        // =========================
        // UNIT OF WORK
        // =========================

        Task SaveChangesAsync();

        Task ExecuteInTransactionAsync(
            Func<Task> action);
    }
}
