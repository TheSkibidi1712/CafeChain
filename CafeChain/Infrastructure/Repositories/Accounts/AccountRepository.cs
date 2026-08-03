using CafeChain.Application.Constants;
using CafeChain.Application.Exceptions;
using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Accounts;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Customer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CafeChain.Infrastrusture.Repositories.Accounts
{
    public class AccountRepository : IAccountRepository
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private const string ROLE_CACHE = "ROLE_CACHE";

        public AccountRepository(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            email = email.Trim().ToLower();

            return await _context.Accounts
                .AnyAsync(x => x.Email.ToLower() == email);
        }

        public async Task<CustomerPhone?> GetCustomerPhoneAsync(string phone)
        {
            return await _context.CustomerPhones
                .Include(x => x.Customer)
                .ThenInclude(c => c.Account)
                .FirstOrDefaultAsync(x => x.Phone == phone);
        }

        public async Task<Account?> GetAccountByEmailAsync(string email)
        {
            email = email.Trim().ToLower();

            return await _context.Accounts
                .Include(x => x.Customer)
                .Include(x => x.Staff)
                .Include(x => x.AccountRoles)
                .ThenInclude(x => x.Role)
                .FirstOrDefaultAsync(x =>
                    x.Email.ToLower() == email);
        }

        public Task<Account?> GetAccountByIdAsync(int accountId) =>
            _context.Accounts.SingleOrDefaultAsync(x => x.AccountId == accountId);

        public async Task<Account> CreateAccountForExistingCustomerAsync(Customer customer, string email, string passwordHash)
        {
            using var tran = await _context.Database.BeginTransactionAsync();

            try
            {
                var account = new Account
                {
                    Email = email,
                    PasswordHash = passwordHash,
                    Active = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Accounts.Add(account);

                await _context.SaveChangesAsync();

                customer.AccountId = account.AccountId;

                if (customer.Category == CustomerCategory.Guest)
                {
                    customer.Category = CustomerCategory.Registered;
                }

                var roleId =
                    await GetRoleId(RoleConstants.Customer);

                _context.AccountRoles.Add(
                    new AccountRole
                    {
                        AccountId = account.AccountId,
                        RoleId = roleId
                    });

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

        public async Task<Account> CreateNewCustomerAccountAsync(Account account, string phone)
        {
            using var tran = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Accounts.Add(account);

                await _context.SaveChangesAsync();

                _context.CustomerPhones.Add(
                    new CustomerPhone
                    {
                        CustomerId = account.Customer.CustomerId,

                        Phone = phone,

                        IsDefault = true
                    });

                var roleId = await GetRoleId(RoleConstants.Customer);

                _context.AccountRoles.Add(
                    new AccountRole
                    {
                        AccountId = account.AccountId,

                        RoleId = roleId
                    });

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

        public Task UpdateAsync(Account account)
        {
            _context.Accounts.Update(account);

            return Task.CompletedTask;
        }

        public async Task RecordFailedLoginAsync(
            int accountId,
            DateTime nowUtc,
            int maxAttempts,
            TimeSpan lockDuration)
        {
            if (_context.Database.IsSqlServer())
            {
                var lockoutEndUtc = nowUtc.Add(lockDuration);
                await _context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.Accounts WITH (UPDLOCK, ROWLOCK)
SET FailedLoginAttempts = CASE
        WHEN FailedLoginAttempts + 1 >= {maxAttempts} THEN 0
        ELSE FailedLoginAttempts + 1
    END,
    LockoutEnd = CASE
        WHEN FailedLoginAttempts + 1 >= {maxAttempts} THEN {lockoutEndUtc}
        ELSE NULL
    END
WHERE AccountId = {accountId};");
                return;
            }

            await ExecuteInTransactionAsync(async () =>
            {
                var tracked = await _context.Accounts.SingleAsync(x => x.AccountId == accountId);
                tracked.FailedLoginAttempts++;
                if (tracked.FailedLoginAttempts >= maxAttempts)
                {
                    tracked.FailedLoginAttempts = 0;
                    tracked.LockoutEnd = nowUtc.Add(lockDuration);
                }
            });
        }

        public async Task ResetLoginFailuresAsync(int accountId)
        {
            if (_context.Database.IsSqlServer())
            {
                await _context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.Accounts WITH (UPDLOCK, ROWLOCK)
SET FailedLoginAttempts = 0,
    LockoutEnd = NULL
WHERE AccountId = {accountId};");
                return;
            }

            await ExecuteInTransactionAsync(async () =>
            {
                var tracked = await _context.Accounts.SingleAsync(x => x.AccountId == accountId);
                tracked.FailedLoginAttempts = 0;
                tracked.LockoutEnd = null;
            });
        }

        public async Task<(bool, int)>CheckLockAsync(string email)
        {
            var account = await GetAccountByEmailAsync(email);

            if (account?.LockoutEnd > DateTime.UtcNow)
            {
                return (
                    true,
                    (int)Math.Ceiling(
                        (
                            account.LockoutEnd.Value - DateTime.UtcNow
                        ).TotalMinutes
                    )
                );
            }

            return (false, 0);
        }

        private async Task<int> GetRoleId(string role)
        {
            var map =
                await _cache.GetOrCreateAsync(
                    ROLE_CACHE,
                    async x =>
                    {
                        x.AbsoluteExpirationRelativeToNow =
                            TimeSpan.FromDays(1);

                        return await _context.Roles
                            .ToDictionaryAsync(
                                x => x.Name,
                                x => x.RoleId
                            );
                    });

            if (map != null && map.TryGetValue(role, out int id))
            {
                return id;
            }

            throw new RoleNotFoundException(role);
        }

        public async Task ExecuteInTransactionAsync(Func<Task> action)
        {
            await using var tran = await _context.Database.BeginTransactionAsync();

            try
            {
                await action();

                await _context.SaveChangesAsync();

                await tran.CommitAsync();
            }
            catch
            {
                await tran.RollbackAsync();

                throw;
            }
        }
    }
}
