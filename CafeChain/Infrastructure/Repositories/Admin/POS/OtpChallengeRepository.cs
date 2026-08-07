using CafeChain.Application.Constants;
using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Operations;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CafeChain.Infrastructure.Repositories.Admin.POS
{
    public class OtpChallengeRepository : IOtpChallengeRepository
    {
        private readonly AppDbContext _context;
        private IDbContextTransaction? _transaction;

        public OtpChallengeRepository(AppDbContext context)
        {
            _context = context;
        }

        public bool HasActiveTransaction => _transaction != null || _context.Database.CurrentTransaction != null;

        public async Task BeginTransactionAsync()
        {
            if (_context.Database.CurrentTransaction != null || _transaction != null)
                return;
            _transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        }

        public async Task CommitTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task RollbackTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task<Staff?> GetRequestingStaffAsync(int staffId, int storeId)
        {
            return await _context.Staffs
                .Include(staff => staff.Account)
                .AsNoTracking()
                .FirstOrDefaultAsync(staff =>
                    staff.StaffId == staffId &&
                    staff.StoreId == storeId &&
                    staff.Active &&
                    staff.Account != null &&
                    staff.Account.Active);
        }

        public async Task<Staff?> GetOtpApproverAsync(int storeId, int excludeStaffId, DateTime utcNow)
        {
            // Phase 1: ShiftSupervisor then StoreManager only; never actor; never AW default.
            var approverRoles = new[]
            {
                RoleConstants.ShiftSupervisor,
                RoleConstants.StoreManager,
                RoleConstants.AreaManager,
                RoleConstants.BusinessOwner
            };

            var candidates = await _context.Staffs
                .Include(staff => staff.Account)
                    .ThenInclude(account => account!.AccountRoles)
                        .ThenInclude(accountRole => accountRole.Role)
                .Where(staff =>
                    staff.StoreId == storeId &&
                    staff.StaffId != excludeStaffId &&
                    staff.Active &&
                    staff.Account != null &&
                    staff.Account.Active &&
                    !string.IsNullOrWhiteSpace(staff.Account.Email) &&
                    staff.Account.AccountRoles.Any(accountRole =>
                        accountRole.Role != null &&
                        accountRole.Role.Active &&
                        approverRoles.Contains(accountRole.Role.Name)))
                .AsNoTracking()
                .ToListAsync();

            return candidates
                .OrderBy(staff => GetOtpApproverRolePriority(staff))
                .ThenBy(staff => staff.StaffId)
                .FirstOrDefault();
        }

        public async Task<IReadOnlyList<Staff>> GetOtpApproverCandidatesAsync(int excludeStaffId)
        {
            var candidates = await _context.Staffs
                .Include(x => x.Account)
                    .ThenInclude(x => x!.AccountRoles)
                        .ThenInclude(x => x.Role)
                .Where(x => x.StaffId != excludeStaffId
                    && x.Active
                    && x.Account != null
                    && x.Account.Active
                    && !string.IsNullOrWhiteSpace(x.Account.Email))
                .AsNoTracking()
                .ToListAsync();

            return candidates
                .OrderBy(GetOtpApproverRolePriority)
                .ThenBy(x => x.StaffId)
                .ToList();
        }

        public async Task<int> GetRecentFailedAttemptsAsync(int requestedByStaffId, DateTime sinceUtc)
        {
            return await _context.OtpChallenges
                .Where(x => x.RequestedByStaffId == requestedByStaffId && x.CreatedAt >= sinceUtc)
                .SumAsync(x => (int?)x.FailedAttempts) ?? 0;
        }

        public Task<int> GetRecentChallengeCountForStaffAsync(int requestedByStaffId, DateTime sinceUtc)
        {
            return _context.OtpChallenges.CountAsync(x =>
                x.RequestedByStaffId == requestedByStaffId && x.CreatedAt >= sinceUtc);
        }

        public Task<int> GetRecentChallengeCountForTerminalAsync(string terminalId, DateTime sinceUtc)
        {
            var normalizedTerminalId = terminalId.Trim();
            return _context.OtpChallenges.CountAsync(x =>
                x.TerminalId == normalizedTerminalId && x.CreatedAt >= sinceUtc);
        }

        public Task<int> GetRecentChallengeCountForIpAsync(string clientIpHash, DateTime sinceUtc)
        {
            var normalized = clientIpHash.Trim();
            return _context.OtpChallenges.CountAsync(x =>
                x.ClientIpHash == normalized && x.CreatedAt >= sinceUtc);
        }

        public Task<int> GetRecentChallengeCountForDeviceAsync(string deviceFingerprintHash, DateTime sinceUtc)
        {
            var normalized = deviceFingerprintHash.Trim();
            return _context.OtpChallenges.CountAsync(x =>
                x.DeviceFingerprintHash == normalized && x.CreatedAt >= sinceUtc);
        }

        public async Task<int> GetRecentFailedAttemptsForIpAsync(string clientIpHash, DateTime sinceUtc)
        {
            var normalized = clientIpHash.Trim();
            return await _context.OtpChallenges
                .Where(x => x.ClientIpHash == normalized && x.CreatedAt >= sinceUtc)
                .SumAsync(x => (int?)x.FailedAttempts) ?? 0;
        }

        public async Task<int> GetRecentFailedAttemptsForDeviceAsync(string deviceFingerprintHash, DateTime sinceUtc)
        {
            var normalized = deviceFingerprintHash.Trim();
            return await _context.OtpChallenges
                .Where(x => x.DeviceFingerprintHash == normalized && x.CreatedAt >= sinceUtc)
                .SumAsync(x => (int?)x.FailedAttempts) ?? 0;
        }

        public async Task<bool> IsApproverStillEligibleAsync(int approverStaffId, int storeId, int actorStaffId)
        {
            if (approverStaffId == actorStaffId)
                return false;

            var approverRoles = new[]
            {
                RoleConstants.ShiftSupervisor,
                RoleConstants.StoreManager,
                RoleConstants.AreaManager,
                RoleConstants.BusinessOwner
            };

            return await _context.Staffs
                .AsNoTracking()
                .AnyAsync(staff =>
                    staff.StaffId == approverStaffId &&
                    staff.StoreId == storeId &&
                    staff.Active &&
                    staff.Account != null &&
                    staff.Account.Active &&
                    !string.IsNullOrWhiteSpace(staff.Account.Email) &&
                    staff.Account.AccountRoles.Any(ar =>
                        ar.Role != null &&
                        ar.Role.Active &&
                        approverRoles.Contains(ar.Role.Name)));
        }

        private static int GetOtpApproverRolePriority(Staff staff)
        {
            var roleNames = staff.Account?.AccountRoles
                .Where(ar => ar.Role != null && ar.Role.Active)
                .Select(ar => ar.Role!.Name)
                .ToList() ?? new List<string>();

            if (roleNames.Contains(RoleConstants.ShiftSupervisor))
                return 0;
            if (roleNames.Contains(RoleConstants.StoreManager))
                return 1;
            if (roleNames.Contains(RoleConstants.AreaManager))
                return 2;
            if (roleNames.Contains(RoleConstants.BusinessOwner))
                return 3;
            return 4;
        }

        public async Task<Store?> GetStoreAsync(int storeId)
        {
            return await _context.Stores
                .AsNoTracking()
                .FirstOrDefaultAsync(store => store.StoreId == storeId);
        }

        public async Task<OtpChallenge?> GetByPublicIdAsync(Guid publicId)
        {
            return await _context.OtpChallenges
                .AsNoTracking()
                .Include(challenge => challenge.Store)
                .Include(challenge => challenge.RequestedByStaff)
                .Include(challenge => challenge.ApproverStaff)
                    .ThenInclude(staff => staff.Account)
                .FirstOrDefaultAsync(challenge => challenge.PublicId == publicId);
        }

        public async Task<OtpChallenge?> GetByPublicIdForUpdateAsync(Guid publicId)
        {
            // SQL Server: take an exclusive row lock so verify/resend/consume serialize.
            if (_context.Database.IsSqlServer())
            {
                await _context.OtpChallenges
                    .FromSqlRaw(
                        "SELECT * FROM [OtpChallenges] WITH (UPDLOCK, ROWLOCK) WHERE [PublicId] = {0}",
                        publicId)
                    .Select(c => c.OtpChallengeId)
                    .FirstOrDefaultAsync();
            }

            // Tracked entity for concurrent updates (RowVersion).
            return await _context.OtpChallenges
                .Include(challenge => challenge.Store)
                .Include(challenge => challenge.RequestedByStaff)
                .Include(challenge => challenge.ApproverStaff)
                    .ThenInclude(staff => staff!.Account)
                        .ThenInclude(a => a!.AccountRoles)
                            .ThenInclude(ar => ar.Role)
                .FirstOrDefaultAsync(challenge => challenge.PublicId == publicId);
        }

        public async Task<OtpChallenge?> FindActiveChallengeAsync(
            int storeId,
            int requestedByStaffId,
            string actionType,
            string targetType,
            int? targetId,
            DateTime utcNow)
        {
            var activeStatuses = new[]
            {
                OtpConstants.Statuses.Pending,
                OtpConstants.Statuses.Approved
            };

            // SQL Server: serialize one-open-challenge checks under the ambient transaction.
            if (_context.Database.IsSqlServer())
            {
                var locked = await _context.OtpChallenges
                    .FromSqlRaw(
                        @"SELECT * FROM [OtpChallenges] WITH (UPDLOCK, HOLDLOCK)
                          WHERE [StoreId] = {0}
                            AND [RequestedByStaffId] = {1}
                            AND [ActionType] = {2}
                            AND [TargetType] = {3}
                            AND [Status] IN (N'Pending', N'Approved')
                            AND [ExpiresAt] > {4}
                            AND (({5} IS NOT NULL AND [TargetId] = {5}) OR ({5} IS NULL AND [TargetId] IS NULL))",
                        storeId, requestedByStaffId, actionType, targetType, utcNow, targetId)
                    .OrderByDescending(c => c.CreatedAt)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();
                return locked;
            }

            var q = _context.OtpChallenges
                .Where(c =>
                    c.StoreId == storeId &&
                    c.RequestedByStaffId == requestedByStaffId &&
                    c.ActionType == actionType &&
                    c.TargetType == targetType &&
                    activeStatuses.Contains(c.Status) &&
                    c.ExpiresAt > utcNow);

            if (targetId.HasValue)
                q = q.Where(c => c.TargetId == targetId.Value);
            else
                q = q.Where(c => c.TargetId == null);

            return await q
                .OrderByDescending(c => c.CreatedAt)
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        public Task<OtpChallenge?> FindLatestOpenShiftChallengeAsync(
            int storeId,
            int requestedByStaffId,
            DateTime sinceUtc)
        {
            var openActions = new[]
            {
                OtpConstants.ActionTypes.OpenShiftLate,
                OtpConstants.ActionTypes.OpenShiftOutsideSchedule
            };
            var visibleStatuses = new[]
            {
                OtpConstants.Statuses.Pending,
                OtpConstants.Statuses.Approved,
                OtpConstants.Statuses.Expired,
                OtpConstants.Statuses.Locked
            };

            return _context.OtpChallenges
                .AsNoTracking()
                .Where(x => x.StoreId == storeId
                    && x.RequestedByStaffId == requestedByStaffId
                    && x.CreatedAt >= sinceUtc
                    && openActions.Contains(x.ActionType)
                    && visibleStatuses.Contains(x.Status))
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.OtpChallengeId)
                .FirstOrDefaultAsync();
        }

        public Task<OtpChallenge?> FindLatestTerminalRegistrationChallengeAsync(
            int storeId,
            int requestedByStaffId,
            DateTime sinceUtc)
        {
            var visibleStatuses = new[]
            {
                OtpConstants.Statuses.Pending,
                OtpConstants.Statuses.Approved,
                OtpConstants.Statuses.Used,
                OtpConstants.Statuses.Expired,
                OtpConstants.Statuses.Locked,
                OtpConstants.Statuses.Cancelled
            };
            return _context.OtpChallenges
                .AsNoTracking()
                .Where(x => x.StoreId == storeId
                    && x.RequestedByStaffId == requestedByStaffId
                    && x.ActionType == OtpConstants.ActionTypes.RegisterTerminal
                    && x.CreatedAt >= sinceUtc
                    && visibleStatuses.Contains(x.Status))
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.OtpChallengeId)
                .FirstOrDefaultAsync();
        }

        public async Task<int> ExpireStaleActiveChallengesAsync(
            int storeId,
            int requestedByStaffId,
            string actionType,
            string targetType,
            int? targetId,
            DateTime utcNow)
        {
            var activeStatuses = new[]
            {
                OtpConstants.Statuses.Pending,
                OtpConstants.Statuses.Approved
            };

            var q = _context.OtpChallenges
                .Where(c =>
                    c.StoreId == storeId &&
                    c.RequestedByStaffId == requestedByStaffId &&
                    c.ActionType == actionType &&
                    c.TargetType == targetType &&
                    activeStatuses.Contains(c.Status) &&
                    c.ExpiresAt <= utcNow);

            if (targetId.HasValue)
                q = q.Where(c => c.TargetId == targetId.Value);
            else
                q = q.Where(c => c.TargetId == null);

            var stale = await q.ToListAsync();
            if (stale.Count == 0)
                return 0;

            foreach (var challenge in stale)
            {
                challenge.Status = OtpConstants.Statuses.Expired;
                challenge.ProtectedOtpPayload = null;
            }

            var staleIds = stale.Select(x => x.OtpChallengeId).ToList();
            var notifications = await _context.StaffNotifications
                .Where(x =>
                    x.Type == StaffNotificationTypes.OperationalOtpRequest
                    && x.EntityType == StaffNotificationEntityTypes.OtpChallenge
                    && staleIds.Contains(x.EntityId)
                    && x.ResolvedAt == null)
                .ToListAsync();
            foreach (var notification in notifications)
            {
                notification.ResolvedAt = utcNow;
                notification.UpdatedAt = utcNow;
            }

            await _context.SaveChangesAsync();
            return stale.Count;
        }

        public async Task<IReadOnlyList<(int ChallengeId, Guid PublicId, int ApproverStaffId, int? NotificationId)>>
            ExpireDueChallengesAsync(DateTime utcNow, CancellationToken cancellationToken = default)
        {
            var activeStatuses = new[] { OtpConstants.Statuses.Pending, OtpConstants.Statuses.Approved };
            var due = await _context.OtpChallenges
                .Where(x => activeStatuses.Contains(x.Status) && x.ExpiresAt <= utcNow)
                .OrderBy(x => x.OtpChallengeId)
                .Take(200)
                .ToListAsync(cancellationToken);
            if (due.Count == 0)
                return Array.Empty<(int, Guid, int, int?)>();

            var ids = due.Select(x => x.OtpChallengeId).ToArray();
            var notifications = await _context.StaffNotifications
                .Where(x => x.Type == StaffNotificationTypes.OperationalOtpRequest
                    && ((x.OtpChallengeId.HasValue && ids.Contains(x.OtpChallengeId.Value))
                        || ids.Contains(x.EntityId)))
                .ToListAsync(cancellationToken);

            foreach (var challenge in due)
            {
                challenge.Status = OtpConstants.Statuses.Expired;
                challenge.ProtectedOtpPayload = null;
            }
            foreach (var notification in notifications)
            {
                notification.ResolvedAt ??= utcNow;
                notification.UpdatedAt = utcNow;
            }
            await _context.SaveChangesAsync(cancellationToken);

            var notificationByChallenge = notifications
                .GroupBy(x => x.OtpChallengeId ?? x.EntityId)
                .ToDictionary(x => x.Key, x => (int?)x.OrderByDescending(n => n.StaffNotificationId).First().StaffNotificationId);
            return due.Select(x => (
                    x.OtpChallengeId,
                    x.PublicId,
                    x.ApproverStaffId,
                    notificationByChallenge.GetValueOrDefault(x.OtpChallengeId)))
                .ToArray();
        }

        public async Task AddAsync(OtpChallenge challenge)
        {
            _context.OtpChallenges.Add(challenge);
            await _context.SaveChangesAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
