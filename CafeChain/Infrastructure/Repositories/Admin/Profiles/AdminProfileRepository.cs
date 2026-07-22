using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.Profiles;
using CafeChain.Models.Staffs;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastructure.Repositories.Admin.Profiles;

public sealed class AdminProfileRepository : IAdminProfileRepository
{
    private readonly AppDbContext _context;

    public AdminProfileRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Staff?> GetByAccountIdAsync(int accountId) =>
        _context.Staffs
            .Include(x => x.Account)
                .ThenInclude(x => x.AccountRoles)
                    .ThenInclude(x => x.Role)
            .Include(x => x.Store)
            .Include(x => x.StaffPhones)
            .FirstOrDefaultAsync(x => x.AccountId == accountId);

    public Task<bool> PhoneExistsAsync(string phone, int excludeStaffId) =>
        _context.StaffPhones.AnyAsync(x => x.Phone == phone && x.StaffId != excludeStaffId);

    public Task SaveChangesAsync() => _context.SaveChangesAsync();

    public async Task ExecuteInTransactionAsync(Func<Task> operation)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await operation();
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
