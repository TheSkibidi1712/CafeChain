using CafeChain.Models.Operations;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Infrastructure.Interfaces.Admin.POS
{
    public interface IOtpChallengeRepository
    {
        Task<Staff?> GetRequestingStaffAsync(int staffId, int storeId);
        Task<Staff?> GetOtpApproverAsync(int storeId, DateTime utcNow);
        Task<Store?> GetStoreAsync(int storeId);
        Task<OtpChallenge?> GetByPublicIdAsync(Guid publicId);
        Task AddAsync(OtpChallenge challenge);
        Task SaveChangesAsync();
    }
}
