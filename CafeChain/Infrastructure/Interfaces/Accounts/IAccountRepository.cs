using CafeChain.Models.Customers;
using System.Threading.Tasks;

namespace CafeChain.Infrastrusture.Interfaces.Accounts
{
    public interface IAccountRepository
    {
        Task<bool> EmailExistsAsync(string email);
        Task<CustomerPhone?> GetCustomerPhoneAsync(string phone);
        Task<Account> GetAccountByEmailAsync(string email);
        Task<Account?> GetAccountByIdAsync(int accountId);
        Task<Account> CreateAccountForExistingCustomerAsync(Customer customer, string email, string passwordHash);
        Task<Account> CreateNewCustomerAccountAsync(Account account, string phone); 
        Task UpdateAsync(Account account); 
        Task RecordFailedLoginAsync(int accountId, DateTime nowUtc, int maxAttempts, TimeSpan lockDuration);
        Task ResetLoginFailuresAsync(int accountId);
        Task<(bool IsLocked, int RemainingMinutes)> CheckLockAsync(string email);
        Task ExecuteInTransactionAsync(Func<Task> action);

    }
}
