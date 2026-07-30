using System.Text.Json;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.StoreMenu;
using CafeChain.Application.Interfaces.Admin.StoreMenu;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.StoreMenu
{
    public sealed class StoreMenuPricingService : IStoreMenuPricingService
    {
        private readonly AppDbContext _context;
        private readonly IStoreCatalogVersionService _catalogVersions;

        public StoreMenuPricingService(AppDbContext context, IStoreCatalogVersionService catalogVersions)
        {
            _context = context;
            _catalogVersions = catalogVersions;
        }

        public async Task<ServiceResult<StoreMenuPriceDto>> GetAsync(
            int storeMenuItemId,
            int actorStaffId,
            CancellationToken cancellationToken = default)
        {
            var item = await _context.StoreMenuItems.AsNoTracking()
                .Include(x => x.DrinkSize)
                .SingleOrDefaultAsync(x => x.StoreMenuItemId == storeMenuItemId, cancellationToken);
            if (item == null)
                return ServiceResult<StoreMenuPriceDto>.Failure("Không tìm thấy SKU trong menu cửa hàng.");
            if (!await CanReadStoreAsync(actorStaffId, item.StoreId, cancellationToken))
                return ServiceResult<StoreMenuPriceDto>.Failure("Bạn không có quyền xem giá menu của cửa hàng này.", errorCode: "STORE_MENU_READ_FORBIDDEN");

            var version = await _catalogVersions.GetAsync(item.StoreId, cancellationToken);
            return ServiceResult<StoreMenuPriceDto>.Success(Map(item, version.Version));
        }

        public async Task<ServiceResult<StoreMenuPriceDto>> UpdateOverrideAsync(
            UpdateStoreMenuPriceOverrideRequest request,
            int actorStaffId,
            CancellationToken cancellationToken = default)
        {
            if (request.UnexpectedFields?.Count > 0)
                return ServiceResult<StoreMenuPriceDto>.Failure(
                    $"Request chứa field không được phép: {string.Join(", ", request.UnexpectedFields.Keys)}.",
                    errorCode: "CLIENT_AUTHORITY_FIELD_REJECTED");
            if (request.StoreMenuItemId <= 0 || request.PriceOverride is <= 0)
                return ServiceResult<StoreMenuPriceDto>.Failure("SKU hoặc giá override không hợp lệ.");
            if (string.IsNullOrWhiteSpace(request.ExpectedRowVersion))
                return ServiceResult<StoreMenuPriceDto>.Failure("Thiếu RowVersion để kiểm soát cập nhật đồng thời.");
            if (string.IsNullOrWhiteSpace(request.Reason))
                return ServiceResult<StoreMenuPriceDto>.Failure("Bắt buộc nhập lý do thay đổi giá cửa hàng.");
            if (!await IsBusinessOwnerAsync(actorStaffId, cancellationToken))
                return ServiceResult<StoreMenuPriceDto>.Failure(
                    "Chỉ Chủ doanh nghiệp được đặt hoặc xóa giá override của cửa hàng.",
                    errorCode: "STORE_PRICE_OVERRIDE_FORBIDDEN");

            var item = await _context.StoreMenuItems.Include(x => x.DrinkSize)
                .SingleOrDefaultAsync(x => x.StoreMenuItemId == request.StoreMenuItemId, cancellationToken);
            if (item == null)
                return ServiceResult<StoreMenuPriceDto>.Failure("Không tìm thấy SKU trong menu cửa hàng.");

            byte[] expected;
            try { expected = Convert.FromBase64String(request.ExpectedRowVersion); }
            catch (FormatException) { return ServiceResult<StoreMenuPriceDto>.Failure("RowVersion không hợp lệ."); }
            if (!expected.SequenceEqual(item.RowVersion))
                return ServiceResult<StoreMenuPriceDto>.Failure(
                    "Menu đã được người khác cập nhật. Vui lòng tải lại.",
                    errorCode: "STORE_MENU_CHANGED_BY_ANOTHER_USER");

            var oldOverride = item.PriceOverride;
            if (oldOverride == request.PriceOverride)
            {
                var currentVersion = await _catalogVersions.GetAsync(item.StoreId, cancellationToken);
                return ServiceResult<StoreMenuPriceDto>.Success(
                    Map(item, currentVersion.Version),
                    "Giá hiệu lực không thay đổi.");
            }

            var now = DateTime.UtcNow;
            var catalogBefore = await _catalogVersions.GetAsync(item.StoreId, cancellationToken);
            _context.Entry(item).Property(x => x.RowVersion).OriginalValue = expected;
            item.PriceOverride = request.PriceOverride;
            item.UpdatedAtUtc = now;
            var versions = await _catalogVersions.InvalidateAsync(new[] { item.StoreId }, now, cancellationToken);

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                _context.StoreMenuItemAudits.Add(new StoreMenuItemAudit
                {
                    StoreMenuItemId = item.StoreMenuItemId,
                    StoreId = item.StoreId,
                    DrinkSizeId = item.DrinkSizeId,
                    Action = request.PriceOverride.HasValue ? "SET_PRICE_OVERRIDE" : "USE_GLOBAL_PRICE",
                    OldIsEnabled = item.IsEnabled,
                    NewIsEnabled = item.IsEnabled,
                    OldPriceOverride = oldOverride,
                    NewPriceOverride = request.PriceOverride,
                    OldEffectiveFromUtc = item.EffectiveFromUtc,
                    NewEffectiveFromUtc = item.EffectiveFromUtc,
                    OldEffectiveToUtc = item.EffectiveToUtc,
                    NewEffectiveToUtc = item.EffectiveToUtc,
                    CatalogVersionBefore = catalogBefore.Version,
                    CatalogVersionAfter = versions[item.StoreId],
                    ItemRowVersionBefore = expected.ToArray(),
                    ItemRowVersionAfter = item.RowVersion.ToArray(),
                    OldDataJson = JsonSerializer.Serialize(new { PriceOverride = oldOverride }),
                    NewDataJson = JsonSerializer.Serialize(new { request.PriceOverride }),
                    ActorStaffId = actorStaffId,
                    Reason = request.Reason.Trim(),
                    CreatedAtUtc = now
                });
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ServiceResult<StoreMenuPriceDto>.Success(
                    Map(item, versions[item.StoreId]),
                    request.PriceOverride.HasValue
                        ? "Đã áp dụng giá riêng cho cửa hàng."
                        : "SKU đã quay lại dùng giá toàn hệ thống.");
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ServiceResult<StoreMenuPriceDto>.Failure(
                    "Menu đã được người khác cập nhật. Vui lòng tải lại.",
                    errorCode: "STORE_MENU_CHANGED_BY_ANOTHER_USER");
            }
        }

        private async Task<bool> IsBusinessOwnerAsync(int staffId, CancellationToken cancellationToken) =>
            await _context.Staffs.AsNoTracking().AnyAsync(x => x.StaffId == staffId
                && x.Active
                && x.Account.Active
                && x.Account.AccountRoles.Any(r => r.Role.Active
                    && (r.Role.Name == RoleConstants.BusinessOwner
                        || r.Role.Name == RoleConstants.SystemAdmin)),
                cancellationToken);

        private async Task<bool> CanReadStoreAsync(int staffId, int storeId, CancellationToken cancellationToken) =>
            await _context.Staffs.AsNoTracking().AnyAsync(x => x.StaffId == staffId
                && x.Active
                && x.Account.Active
                && (x.StoreId == storeId
                    || x.Account.AccountRoles.Any(r => r.Role.Active
                        && (r.Role.Name == RoleConstants.BusinessOwner
                            || r.Role.Name == RoleConstants.SystemAdmin))),
                cancellationToken);

        private static StoreMenuPriceDto Map(StoreMenuItem item, long catalogVersion) => new()
        {
            StoreMenuItemId = item.StoreMenuItemId,
            StoreId = item.StoreId,
            DrinkSizeId = item.DrinkSizeId,
            GlobalPrice = item.DrinkSize.Price,
            StoreOverride = item.PriceOverride,
            EffectivePrice = item.GetEffectivePrice(),
            PriceSource = item.GetPriceSource(),
            RowVersion = item.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(item.RowVersion),
            CatalogVersion = catalogVersion
        };
    }
}
