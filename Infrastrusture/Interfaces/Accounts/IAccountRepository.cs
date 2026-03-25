using CafeChain.Models.Customers;
using System.Threading.Tasks;

namespace CafeChain.Infrastrusture.Interfaces.Accounts
{
    public interface IAccountRepository
    {
        Task<bool> EmailExistsAsync(string email);
        Task<bool> PhoneExistsAsync(string phone);
        Task<Account> CreateCustomerAccountAsync(Account account, string phone);
        Task<Account> GetAccountByEmailAsync(string email);
    }
}
