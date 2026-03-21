using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Accounts;
using CafeChain.Models.Customers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Infrastrusture.Repositories.Accounts
{
    public class AccountRepository : IAccountRepository
    {
        private readonly AppDbContext _context;

        public AccountRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            email = email.Trim().ToLower();
            return await _context.Accounts.AnyAsync(a => a.Email.ToLower() == email);
        }

        public async Task<bool> PhoneExistsAsync(string phone)
        {
            phone = phone.Trim();
            return await _context.CustomerPhones.AnyAsync(p => p.Phone == phone);
        }

        public async Task<Account> CreateCustomerAccountAsync(Account account, string phone)
        {
            using var tran = await _context.Database.BeginTransactionAsync();

            try
            {
                var customer = account.Customer;

                // ===== CUSTOMER =====
                customer.Active = true;
                customer.AvatarUrl ??= "/Images/Upload/avtdf.jpg";

                _context.Customers.Add(customer);

                // ===== SAVE lần 1 để lấy CustomerId =====
                await _context.SaveChangesAsync();

                // ===== PHONE =====
                var customerPhone = new CustomerPhone
                {
                    CustomerId = customer.CustomerId,
                    Phone = phone
                };
                _context.CustomerPhones.Add(customerPhone);

                // ===== POINT =====
                var customerPoint = new CustomerPoint
                {
                    CustomerId = customer.CustomerId,
                    Points = 0
                };
                _context.CustomerPoints.Add(customerPoint);

                // ===== ACCOUNT =====
                account.CustomerId = customer.CustomerId;
                account.AccountTypeId = 1; // Customer
                account.Active = true;

                _context.Accounts.Add(account);

                // ===== SAVE ALL =====
                await _context.SaveChangesAsync();

                await tran.CommitAsync();

                return account;
            }
            catch
            {
                await tran.RollbackAsync();
                throw;
            }
        }

        public async Task<Account> GetAccountByEmailAsync(string email)
        {
            email = email.ToLower().Trim();

            return await _context.Accounts
                .Include(x => x.AccountType)
                .Include(x => x.Customer)
                .Include(x => x.Staff)
                .FirstOrDefaultAsync(x => x.Email.ToLower() == email);
        }
    }
}
