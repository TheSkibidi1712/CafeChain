using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Infrastrusture.Interfaces.Accounts;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastrusture.Repositories.Accounts
{
    public class PasswordResetRepository : IPasswordResetRepository
    {
        private readonly AppDbContext _context;

        public PasswordResetRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task SaveOtpAsync(PasswordResetOtp otp)
        {
            _context.PasswordResetOtps.Add(otp);
            await _context.SaveChangesAsync();
        }

        public async Task<PasswordResetOtp> GetValidOtpAsync(string email, string code)
        {
            return await _context.PasswordResetOtps
                .FirstOrDefaultAsync(x =>
                    x.Email == email &&
                    x.Code == code &&
                    !x.IsUsed &&
                    x.ExpiredAt > DateTime.UtcNow);
        }

        public async Task MarkOtpUsedAsync(PasswordResetOtp otp)
        {
            otp.IsUsed = true;
            await _context.SaveChangesAsync();
        }

        public async Task UpdatePasswordAsync(string email, string hash)
        {
            var acc = await _context.Accounts.FirstOrDefaultAsync(x => x.Email == email);
            if (acc == null)
                throw new Exception("Account not found");
            acc.PasswordHash = hash;

            await _context.SaveChangesAsync();
        }
        public async Task<PasswordResetOtp?> GetLatestOtpAsync(string email)
        {
            return await _context.PasswordResetOtps
                .Where(x => x.Email == email)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task InvalidateOldOtpsAsync(string email)
        {
            var otps = await _context.PasswordResetOtps
                .Where(x => x.Email == email && !x.IsUsed)
                .ToListAsync();

            foreach (var otp in otps)
                otp.IsUsed = true;

            await _context.SaveChangesAsync();
        }
        public async Task IncreaseFailCountAsync(PasswordResetOtp otp)
        {
            otp.FailedAttempts++;
            await _context.SaveChangesAsync();
        }
    }
}
