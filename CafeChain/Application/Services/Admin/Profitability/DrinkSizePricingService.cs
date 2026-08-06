using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.Interfaces.Admin.Profitability;
using CafeChain.Application.Interfaces.Admin.StoreMenu;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Profitability
{
    public sealed class DrinkSizePricingService : IDrinkSizePricingService
    {
        private readonly AppDbContext _context;
        private readonly IDrinkSizeProfitabilityQueryService _profitability;
        private readonly IStoreCatalogVersionService _catalogVersions;

        public DrinkSizePricingService(
            AppDbContext context,
            IDrinkSizeProfitabilityQueryService profitability,
            IStoreCatalogVersionService? catalogVersions = null)
        {
            _context = context;
            _profitability = profitability;
            _catalogVersions = catalogVersions
                ?? new CafeChain.Application.Services.Admin.StoreMenu.StoreCatalogVersionService(context);
        }

        public async Task<ServiceResult<DrinkSizePriceUpdateResult>> UpdatePriceAsync(UpdateDrinkSizePriceRequest request, int storeIdForCostCheck, int actorStaffId, CancellationToken cancellationToken = default)
        {
            if (request.UnexpectedFields?.Count > 0)
                return ServiceResult<DrinkSizePriceUpdateResult>.Failure(
                    $"Request chứa field không được phép: {string.Join(", ", request.UnexpectedFields.Keys)}.", errorCode: "CLIENT_AUTHORITY_FIELD_REJECTED");
            if (request.DrinkSizeId <= 0 || request.NewSellingPrice <= 0)
                return ServiceResult<DrinkSizePriceUpdateResult>.Failure("DrinkSize hoặc giá bán không hợp lệ.");
            if (string.IsNullOrWhiteSpace(request.ExpectedRowVersion))
                return ServiceResult<DrinkSizePriceUpdateResult>.Failure("Thiếu RowVersion để kiểm soát cập nhật đồng thời.");
            if (!await IsBusinessOwnerAsync(actorStaffId, cancellationToken))
                return ServiceResult<DrinkSizePriceUpdateResult>.Failure("Chỉ Chủ doanh nghiệp được cập nhật giá bán toàn hệ thống.", errorCode: "GLOBAL_PRICE_FORBIDDEN");

            var drinkSize = await _context.DrinkSizes.Include(x => x.Drink)
                .FirstOrDefaultAsync(x => x.DrinkSizeId == request.DrinkSizeId && x.Active, cancellationToken);
            if (drinkSize == null)
                return ServiceResult<DrinkSizePriceUpdateResult>.Failure("Không tìm thấy DrinkSize đang hoạt động.");

            byte[] expected;
            try { expected = Convert.FromBase64String(request.ExpectedRowVersion); }
            catch (FormatException) { return ServiceResult<DrinkSizePriceUpdateResult>.Failure("RowVersion không hợp lệ."); }
            if (!expected.SequenceEqual(drinkSize.RowVersion))
                return ServiceResult<DrinkSizePriceUpdateResult>.Failure(
                    "Giá đã được người khác cập nhật. Vui lòng tải lại trước khi lưu.", errorCode: "PRICE_CHANGED_BY_ANOTHER_USER");

            if (drinkSize.Price == request.NewSellingPrice)
            {
                var currentCatalog = await _catalogVersions.GetAsync(storeIdForCostCheck, cancellationToken);
                return ServiceResult<DrinkSizePriceUpdateResult>.Success(new DrinkSizePriceUpdateResult
                {
                    DrinkSizeId = drinkSize.DrinkSizeId,
                    OldPrice = drinkSize.Price,
                    NewPrice = drinkSize.Price,
                    RowVersion = drinkSize.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(drinkSize.RowVersion),
                    CatalogVersion = currentCatalog.Version
                }, "Giá toàn hệ thống không thay đổi.");
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
                return ServiceResult<DrinkSizePriceUpdateResult>.Failure(
                    "Vui lòng nhập lý do thay đổi giá bán.",
                    errorCode: "PRICE_CHANGE_REASON_REQUIRED");

            var preview = await _profitability.PreviewAsync(storeIdForCostCheck, drinkSize.DrinkId, DateTime.UtcNow, actorStaffId, cancellationToken);
            var costStatus = preview.IsSuccess
                ? preview.Data.Sizes.FirstOrDefault(x => x.DrinkSizeId == drinkSize.DrinkSizeId)?.CostStatus ?? ProfitabilityCostStatuses.Incomplete
                : ProfitabilityCostStatuses.Incomplete;
            if (costStatus != ProfitabilityCostStatuses.Complete && !request.ConfirmIncompleteCost)
                return ServiceResult<DrinkSizePriceUpdateResult>.Failure(
                    "Giá vốn chưa đầy đủ. Hãy xác nhận cảnh báo trước khi lưu giá thủ công.",
                    errorCode: "INCOMPLETE_COST_CONFIRMATION_REQUIRED");

            var oldPrice = drinkSize.Price;
            _context.Entry(drinkSize).Property(x => x.RowVersion).OriginalValue = expected;
            drinkSize.Price = request.NewSellingPrice;
            drinkSize.UpdatedAtUtc = DateTime.UtcNow;

            var now = DateTime.UtcNow;
            var affectedStoreIds = await _context.StoreMenuItems.AsNoTracking()
                .Where(x => x.DrinkSizeId == drinkSize.DrinkSizeId
                    && x.PriceOverride == null
                    && x.IsEnabled
                    && x.PublishedAtUtc.HasValue
                    && (!x.EffectiveFromUtc.HasValue || x.EffectiveFromUtc.Value <= now)
                    && (!x.EffectiveToUtc.HasValue || x.EffectiveToUtc.Value > now))
                .Select(x => x.StoreId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var catalogVersions = await _catalogVersions.InvalidateAsync(affectedStoreIds, now, cancellationToken);

            _context.DrinkSizePriceAudits.Add(new DrinkSizePriceAudit
            {
                DrinkSizeId = drinkSize.DrinkSizeId,
                OldPrice = oldPrice,
                NewPrice = request.NewSellingPrice,
                ActorStaffId = actorStaffId,
                Reason = request.Reason.Trim(),
                CostStatus = costStatus,
                CreatedAtUtc = now
            });

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ServiceResult<DrinkSizePriceUpdateResult>.Success(new DrinkSizePriceUpdateResult
                {
                    DrinkSizeId = drinkSize.DrinkSizeId,
                    OldPrice = oldPrice,
                    NewPrice = drinkSize.Price,
                    RowVersion = drinkSize.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(drinkSize.RowVersion),
                    CatalogVersion = catalogVersions.Count == 0 ? 0 : catalogVersions.Values.Max()
                }, affectedStoreIds.Count == 0
                    ? "Đã cập nhật giá bán toàn hệ thống; chưa có menu cửa hàng fallback cần làm mới."
                    : $"Đã cập nhật giá bán toàn hệ thống và làm mới {affectedStoreIds.Count} catalog cửa hàng fallback.");
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ServiceResult<DrinkSizePriceUpdateResult>.Failure(
                    "Giá đã được người khác cập nhật. Vui lòng tải lại trước khi lưu.", errorCode: "PRICE_CHANGED_BY_ANOTHER_USER");
            }
        }

        public Task<PosCatalogVersionDto> GetCatalogVersionAsync(int storeId, CancellationToken cancellationToken = default) =>
            _catalogVersions.GetAsync(storeId, cancellationToken);

        private async Task<bool> IsBusinessOwnerAsync(int staffId, CancellationToken ct) => await _context.Staffs.AsNoTracking()
            .AnyAsync(s => s.StaffId == staffId && s.Active && s.Account.Active
                && s.Account.AccountRoles.Any(ar => ar.Role.Active
                    && (ar.Role.Name == RoleConstants.BusinessOwner
                        || ar.Role.Name == RoleConstants.SystemAdmin)), ct);
    }
}
