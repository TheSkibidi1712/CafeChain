using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Accounts;
using CafeChain.Models.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using CafeChain.Application.Constants;
using CafeChain.Application.Exceptions;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace CafeChain.Infrastrusture.Repositories.Accounts
{
    public class AccountRepository : IAccountRepository
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private const string ROLE_CACHE_KEY = "SystemRolesCache";

        public AccountRepository(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
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
                // ===== 1. ADD ACCOUNT (EF sẽ tự add Customer kèm theo) =====
                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();
                // 🔥 Sau dòng này:
                // account.AccountId có giá trị
                // account.Customer.CustomerId cũng có

                var customer = account.Customer;

                // ===== 2. PHONE =====
                _context.CustomerPhones.Add(new CustomerPhone
                {
                    CustomerId = customer.CustomerId,
                    Phone = phone,
                    IsDefault = true
                });

                // ===== 3. POINT =====
                _context.CustomerPoints.Add(new CustomerPoint
                {
                    CustomerId = customer.CustomerId,
                    Points = 0
                });

                // ===== 4. ROLE =====
                var customerRoleId = await GetRoleIdByNameAsync(RoleConstants.Customer);
                _context.AccountRoles.Add(new AccountRole
                {
                    AccountId = account.AccountId,
                    RoleId = customerRoleId

                });

                await _context.SaveChangesAsync();
                await tran.CommitAsync();

                return account;
            }
            catch (Exception ex)
            {
                await tran.RollbackAsync();

                // 🔥 QUAN TRỌNG: expose lỗi thật
                throw new Exception(ex.InnerException?.Message ?? ex.Message);
            }
        }

        public async Task<Account> GetAccountByEmailAsync(string email)
        {
            email = email.ToLower().Trim();

            return await _context.Accounts
                .Include(x => x.Customer)
                .Include(x => x.Staff)
                .Include(x => x.AccountRoles)
                    .ThenInclude(ar => ar.Role)
                .FirstOrDefaultAsync(x => x.Email.ToLower() == email);
        }

        /// <summary>
        /// 🔥 Thread-safe Lazy Loading Roles into Cache
        /// Tránh query DB liên tục mỗi khi có user Register
        /// </summary>
        private async Task<int> GetRoleIdByNameAsync(string roleName)
        {
            var roleMap = await _cache.GetOrCreateAsync(ROLE_CACHE_KEY, async entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromDays(1); // Cache 1 ngày
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7);
                
                return await _context.Roles
                    .AsNoTracking()
                    .ToDictionaryAsync(r => r.Name, r => r.RoleId);
            });

            if (roleMap != null && roleMap.TryGetValue(roleName, out int roleId))
            {
                return roleId;
            }

            throw new RoleNotFoundException(roleName);
        }

        public async Task UpdateAsync(Account account)
        {
            _context.Accounts.Update(account);
            await _context.SaveChangesAsync();
        }

        public async Task<(bool IsLocked, int RemainingMinutes)> CheckLockAsync(string email)
        {
            email = email.Trim().ToLower();

            var account = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Email.ToLower() == email);

            if (account == null)
                return (false, 0);

            if (account.LockoutEnd.HasValue && account.LockoutEnd > DateTime.UtcNow)
            {
                var remain = (account.LockoutEnd.Value - DateTime.UtcNow).TotalMinutes;
                return (true, (int)Math.Ceiling(remain));
            }

            return (false, 0);
        }
    }
}
