using CafeChain.Models.Staffs;

namespace CafeChain.Infrastructure.Interfaces.Admin.Profiles;

public interface IAdminProfileRepository
{
    Task<Staff?> GetByAccountIdAsync(int accountId);
    Task<bool> PhoneExistsAsync(string phone, int excludeStaffId);
    Task SaveChangesAsync();
    Task ExecuteInTransactionAsync(Func<Task> operation);
}
