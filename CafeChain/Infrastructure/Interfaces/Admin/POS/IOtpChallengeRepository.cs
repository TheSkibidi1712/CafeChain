using CafeChain.Models.Operations;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;

namespace CafeChain.Infrastructure.Interfaces.Admin.POS
{
    public interface IOtpChallengeRepository
    {
        Task<Staff?> GetRequestingStaffAsync(int staffId, int storeId);

        /// <summary>
        /// Eligible OTP approver for store, excluding the actor (anti self-approval).
        /// Priority: ShiftSupervisor then StoreManager; requires email.
        /// </summary>
        Task<Staff?> GetOtpApproverAsync(int storeId, int excludeStaffId, DateTime utcNow);

        /// <summary>Revalidate approver still eligible at store and not the actor.</summary>
        Task<bool> IsApproverStillEligibleAsync(int approverStaffId, int storeId, int actorStaffId);

        Task<Store?> GetStoreAsync(int storeId);

        Task<OtpChallenge?> GetByPublicIdAsync(Guid publicId);

        /// <summary>Tracked load for update (verify/resend/consume).</summary>
        Task<OtpChallenge?> GetByPublicIdForUpdateAsync(Guid publicId);

        Task<OtpChallenge?> FindActiveChallengeAsync(
            int storeId,
            int requestedByStaffId,
            string actionType,
            string targetType,
            int? targetId,
            DateTime utcNow);

        /// <summary>
        /// Mark Pending/Approved challenges past ExpiresAt as Expired so the unique
        /// one-active index can accept a new request for the same actor/action/target.
        /// </summary>
        Task<int> ExpireStaleActiveChallengesAsync(
            int storeId,
            int requestedByStaffId,
            string actionType,
            string targetType,
            int? targetId,
            DateTime utcNow);

        Task AddAsync(OtpChallenge challenge);

        Task SaveChangesAsync();

        /// <summary>Begin ambient transaction when none exists (SQL Server concurrency tests / consume).</summary>
        Task BeginTransactionAsync();

        Task CommitTransactionAsync();

        Task RollbackTransactionAsync();

        bool HasActiveTransaction { get; }
    }
}
