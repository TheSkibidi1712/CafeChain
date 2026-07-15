using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.Interfaces.Admin.Profitability;
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

        public DrinkSizePricingService(AppDbContext context, IDrinkSizeProfitabilityQueryService profitability)
        {
            _context = context;
            _profitability = profitability;
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

            var preview = await _profitability.PreviewAsync(storeIdForCostCheck, drinkSize.DrinkId, DateTime.UtcNow, actorStaffId, cancellationToken);
            var costStatus = preview.IsSuccess
                ? preview.Data.Sizes.FirstOrDefault(x => x.DrinkSizeId == drinkSize.DrinkSizeId)?.CostStatus ?? ProfitabilityCostStatuses.Incomplete
                : ProfitabilityCostStatuses.Incomplete;
            if (costStatus != ProfitabilityCostStatuses.Complete && string.IsNullOrWhiteSpace(request.Reason))
                return ServiceResult<DrinkSizePriceUpdateResult>.Failure(
                    "Giá vốn chưa đầy đủ. Hãy xác nhận cảnh báo và nhập lý do trước khi lưu giá thủ công.",
                    errorCode: "INCOMPLETE_COST_CONFIRMATION_REQUIRED");

            var oldPrice = drinkSize.Price;
            _context.Entry(drinkSize).Property(x => x.RowVersion).OriginalValue = expected;
            drinkSize.Price = request.NewSellingPrice;
            drinkSize.UpdatedAtUtc = DateTime.UtcNow;

            var catalog = await _context.PosCatalogStates.FirstOrDefaultAsync(x => x.PosCatalogStateId == 1, cancellationToken);
            if (catalog == null)
            {
                catalog = new PosCatalogState { PosCatalogStateId = 1, Version = 1, UpdatedAtUtc = DateTime.UtcNow };
                _context.PosCatalogStates.Add(catalog);
            }
            else
            {
                catalog.Version++;
                catalog.UpdatedAtUtc = DateTime.UtcNow;
            }

            _context.DrinkSizePriceAudits.Add(new DrinkSizePriceAudit
            {
                DrinkSizeId = drinkSize.DrinkSizeId,
                OldPrice = oldPrice,
                NewPrice = request.NewSellingPrice,
                ActorStaffId = actorStaffId,
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Cập nhật giá bán toàn hệ thống" : request.Reason.Trim(),
                CostStatus = costStatus,
                CreatedAtUtc = DateTime.UtcNow
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
                    CatalogVersion = catalog.Version
                }, "Đã cập nhật giá bán toàn hệ thống và làm mới phiên bản catalog.");
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ServiceResult<DrinkSizePriceUpdateResult>.Failure(
                    "Giá đã được người khác cập nhật. Vui lòng tải lại trước khi lưu.", errorCode: "PRICE_CHANGED_BY_ANOTHER_USER");
            }
        }

        public async Task<PosCatalogVersionDto> GetCatalogVersionAsync(CancellationToken cancellationToken = default)
        {
            var state = await _context.PosCatalogStates.AsNoTracking().FirstOrDefaultAsync(x => x.PosCatalogStateId == 1, cancellationToken);
            return state == null
                ? new PosCatalogVersionDto { Version = 0, UpdatedAtUtc = DateTime.UnixEpoch }
                : new PosCatalogVersionDto { Version = state.Version, UpdatedAtUtc = state.UpdatedAtUtc };
        }

        private async Task<bool> IsBusinessOwnerAsync(int staffId, CancellationToken ct) => await _context.Staffs.AsNoTracking()
            .AnyAsync(s => s.StaffId == staffId && s.Active && s.Account.AccountRoles.Any(ar => ar.Role.Active && ar.Role.Name == RoleConstants.BusinessOwner), ct);
    }
}
