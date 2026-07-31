using System.Text.Json;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.StoreScope;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.StoreScope
{
    public sealed class AdminStoreScopeResolver : IAdminStoreScopeResolver
    {
        private readonly AppDbContext _context;
        private readonly IScopeAuthorizationService _scopeAuthorization;
        private readonly IAdminSelectedStoreContext _selectedStoreContext;

        public AdminStoreScopeResolver(
            AppDbContext context,
            IScopeAuthorizationService scopeAuthorization,
            IAdminSelectedStoreContext selectedStoreContext)
        {
            _context = context;
            _scopeAuthorization = scopeAuthorization;
            _selectedStoreContext = selectedStoreContext;
        }

        public Task<AdminStoreScopeResolution> ResolveAsync(
            AdminActorContext actor,
            int? requestedStoreId = null,
            CancellationToken cancellationToken = default) =>
            ResolveAsync(
                actor,
                StoreScopePurpose.Default,
                requestedStoreId,
                cancellationToken);

        public async Task<AdminStoreScopeResolution> ResolveAsync(
            AdminActorContext actor,
            StoreScopePurpose purpose,
            int? requestedStoreId = null,
            CancellationToken cancellationToken = default)
        {
            var allowedStores = actor.StaffId > 0
                ? await _scopeAuthorization.GetAllowedStoresAsync(
                    actor.StaffId,
                    purpose)
                : new List<Models.Stores.Store>();
            var options = allowedStores
                .Where(x => x.Active)
                .OrderBy(x => x.Name)
                .ThenBy(x => x.StoreId)
                .Select(x => new AdminStoreOptionDto
                {
                    StoreId = x.StoreId,
                    StoreName = x.Name
                })
                .ToList();
            var allowedIds = options.Select(x => x.StoreId).ToHashSet();

            if (requestedStoreId.HasValue)
            {
                var requestedId = requestedStoreId.Value;
                if (!allowedIds.Contains(requestedId))
                {
                    await WriteTamperingAuditAsync(
                        actor,
                        requestedId,
                        purpose,
                        allowedIds,
                        cancellationToken);
                    return Failure(
                        AdminStoreScopeResolutionStatus.RequestedStoreForbidden,
                        AdminStoreScopeErrorCodes.StoreScopeForbidden,
                        "Bạn không có quyền truy cập cửa hàng đã chọn.",
                        options);
                }

                return Resolved(
                    requestedId,
                    AdminStoreScopeResolutionSource.ExplicitRequest,
                    options);
            }

            string? warningCode = null;
            var selectedStoreId = _selectedStoreContext.GetSelectedStoreId();
            if (selectedStoreId.GetValueOrDefault() > 0)
            {
                if (allowedIds.Contains(selectedStoreId!.Value))
                {
                    return Resolved(
                        selectedStoreId.Value,
                        AdminStoreScopeResolutionSource.SelectedSessionStore,
                        options);
                }

                _selectedStoreContext.ClearSelectedStoreId();
                warningCode = AdminStoreScopeErrorCodes.SelectedStoreNoLongerAccessible;
            }

            var staffStoreId = actor.StaffId > 0
                ? await _context.Staffs
                    .AsNoTracking()
                    .Where(x => x.StaffId == actor.StaffId)
                    .Select(x => (int?)x.StoreId)
                    .SingleOrDefaultAsync(cancellationToken)
                : null;
            if (staffStoreId is int resolvedStaffStoreId
                && resolvedStaffStoreId > 0
                && allowedIds.Contains(resolvedStaffStoreId))
            {
                return Resolved(
                    resolvedStaffStoreId,
                    AdminStoreScopeResolutionSource.StaffStore,
                    options,
                    warningCode);
            }

            var firstStore = options.FirstOrDefault();
            if (firstStore != null)
            {
                return Resolved(
                    firstStore.StoreId,
                    AdminStoreScopeResolutionSource.FirstAccessibleStore,
                    options,
                    warningCode);
            }

            return Failure(
                AdminStoreScopeResolutionStatus.NoAccessibleStore,
                AdminStoreScopeErrorCodes.StoreScopeNotConfigured,
                "Tài khoản chưa được cấu hình phạm vi cửa hàng. Vui lòng liên hệ quản trị viên.",
                options,
                warningCode);
        }

        private async Task WriteTamperingAuditAsync(
            AdminActorContext actor,
            int requestedStoreId,
            StoreScopePurpose purpose,
            IReadOnlyCollection<int> effectiveStoreIds,
            CancellationToken cancellationToken)
        {
            var auditData = JsonSerializer.Serialize(new
            {
                requestedStoreId,
                purpose = purpose.ToString(),
                effectiveStoreIds = effectiveStoreIds.OrderBy(x => x).ToArray()
            });
            var createdAt = DateTime.UtcNow;
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO AuditLogs
                     (TableName, RecordId, Action, OldData, NewData, UserId, CreatedAt)
                 VALUES
                     ({"StoreScope"}, {requestedStoreId}, {"CROSS_STORE_TAMPERING"},
                      {(string?)null}, {auditData}, {actor.StaffId}, {createdAt})
                 """,
                cancellationToken);
        }

        private AdminStoreScopeResolution Resolved(
            int storeId,
            AdminStoreScopeResolutionSource source,
            IReadOnlyList<AdminStoreOptionDto> options,
            string? warningCode = null)
        {
            _selectedStoreContext.SetSelectedStoreId(storeId);
            return new AdminStoreScopeResolution
            {
                Status = AdminStoreScopeResolutionStatus.Resolved,
                Source = source,
                StoreId = storeId,
                WarningCode = warningCode,
                AccessibleStores = options
            };
        }

        private static AdminStoreScopeResolution Failure(
            AdminStoreScopeResolutionStatus status,
            string errorCode,
            string message,
            IReadOnlyList<AdminStoreOptionDto> options,
            string? warningCode = null)
        {
            return new AdminStoreScopeResolution
            {
                Status = status,
                ErrorCode = errorCode,
                WarningCode = warningCode,
                Message = message,
                AccessibleStores = options
            };
        }
    }
}
