using System.Text.Json;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Profitability;
using CafeChain.Application.DTOs.Admin.StoreMenu;
using CafeChain.Application.Interfaces.Admin.Profitability;
using CafeChain.Application.Interfaces.Admin.StoreMenu;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.StoreMenu
{
    public sealed class StoreMenuWorkspaceService : IStoreMenuWorkspaceService
    {
        private readonly AppDbContext _context;
        private readonly IStoreMenuAvailabilityEvaluator _availability;
        private readonly IDrinkSizeProfitabilityQueryService _profitability;
        private readonly IStoreCatalogVersionService _catalogVersions;
        private readonly IScopeAuthorizationService _scopeAuthorization;

        public StoreMenuWorkspaceService(
            AppDbContext context,
            IStoreMenuAvailabilityEvaluator availability,
            IDrinkSizeProfitabilityQueryService profitability,
            IStoreCatalogVersionService catalogVersions,
            IScopeAuthorizationService scopeAuthorization)
        {
            _context = context;
            _availability = availability;
            _profitability = profitability;
            _catalogVersions = catalogVersions;
            _scopeAuthorization = scopeAuthorization;
        }

        public async Task<ServiceResult<IReadOnlyList<StoreMenuWorkspaceRowDto>>> GetRowsAsync(
            int storeId,
            int actorStaffId,
            DateTime asOfUtc,
            CancellationToken cancellationToken = default)
        {
            if (!await CanReadStoreAsync(actorStaffId, storeId, cancellationToken))
                return ServiceResult<IReadOnlyList<StoreMenuWorkspaceRowDto>>.Failure(
                    "Bạn không có quyền xem menu của cửa hàng này.",
                    errorCode: "STORE_MENU_READ_FORBIDDEN");

            var items = await _context.StoreMenuItems.AsNoTracking()
                .Include(x => x.DrinkSize).ThenInclude(x => x.Drink).ThenInclude(x => x.Category)
                .Include(x => x.DrinkSize).ThenInclude(x => x.Size)
                .Where(x => x.StoreId == storeId)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.DrinkSize.Drink.Name)
                .ThenBy(x => x.DrinkSize.Size.Name)
                .ToListAsync(cancellationToken);

            var profitabilityBySize = new Dictionary<int, DrinkSizeProfitabilityRowDto>();
            foreach (var drinkId in items.Select(x => x.DrinkSize.DrinkId).Distinct())
            {
                var preview = await _profitability.PreviewAsync(
                    storeId, drinkId, asOfUtc, actorStaffId, cancellationToken);
                if (preview.IsSuccess && preview.Data != null)
                {
                    foreach (var size in preview.Data.Sizes)
                        profitabilityBySize[size.DrinkSizeId] = size;
                }
            }

            var rows = new List<StoreMenuWorkspaceRowDto>(items.Count);
            foreach (var item in items)
            {
                var operational = await _availability.EvaluateAsync(
                    storeId, item.DrinkSizeId, asOfUtc, cancellationToken);
                profitabilityBySize.TryGetValue(item.DrinkSizeId, out var cost);
                rows.Add(Map(item, operational, cost));
            }

            return ServiceResult<IReadOnlyList<StoreMenuWorkspaceRowDto>>.Success(rows);
        }

        public async Task<ServiceResult<StoreMenuWorkspaceRowDto>> UpdateLifecycleAsync(
            UpdateStoreMenuLifecycleRequest request,
            int actorStaffId,
            CancellationToken cancellationToken = default)
        {
            if (request.UnexpectedFields?.Count > 0)
                return ServiceResult<StoreMenuWorkspaceRowDto>.Failure(
                    $"Request chứa field không được phép: {string.Join(", ", request.UnexpectedFields.Keys)}.",
                    errorCode: "CLIENT_AUTHORITY_FIELD_REJECTED");
            if (request.StoreMenuItemId <= 0 || !StoreMenuLifecycleActions.IsSupported(request.Action))
                return ServiceResult<StoreMenuWorkspaceRowDto>.Failure("SKU hoặc thao tác menu không hợp lệ.");
            if (string.IsNullOrWhiteSpace(request.ExpectedRowVersion))
                return ServiceResult<StoreMenuWorkspaceRowDto>.Failure("Thiếu RowVersion để kiểm soát cập nhật đồng thời.");
            if (string.IsNullOrWhiteSpace(request.Reason))
                return ServiceResult<StoreMenuWorkspaceRowDto>.Failure("Bắt buộc nhập lý do thay đổi menu cửa hàng.");

            var item = await _context.StoreMenuItems
                .Include(x => x.DrinkSize).ThenInclude(x => x.Drink).ThenInclude(x => x.Category)
                .Include(x => x.DrinkSize).ThenInclude(x => x.Size)
                .SingleOrDefaultAsync(x => x.StoreMenuItemId == request.StoreMenuItemId, cancellationToken);
            if (item == null)
                return ServiceResult<StoreMenuWorkspaceRowDto>.Failure("Không tìm thấy SKU trong menu cửa hàng.");

            var roles = await GetRolesAsync(actorStaffId, cancellationToken);
            var canPublish = roles.Contains(RoleConstants.BusinessOwner);
            var canOperate = canPublish || (roles.Contains(RoleConstants.StoreManager)
                && await CanAccessManagedStoreAsync(actorStaffId, item.StoreId, cancellationToken));
            if (request.Action == StoreMenuLifecycleActions.Publish && !canPublish)
                return ServiceResult<StoreMenuWorkspaceRowDto>.Failure(
                    "Chỉ Chủ doanh nghiệp được publish SKU lên menu cửa hàng.",
                    errorCode: "STORE_MENU_PUBLISH_FORBIDDEN");
            if (request.Action != StoreMenuLifecycleActions.Publish && !canOperate)
                return ServiceResult<StoreMenuWorkspaceRowDto>.Failure(
                    "Bạn không có quyền vận hành menu của cửa hàng này.",
                    errorCode: "STORE_MENU_OPERATION_FORBIDDEN");

            byte[] expected;
            try { expected = Convert.FromBase64String(request.ExpectedRowVersion); }
            catch (FormatException)
            {
                return ServiceResult<StoreMenuWorkspaceRowDto>.Failure("RowVersion không hợp lệ.");
            }
            if (!expected.SequenceEqual(item.RowVersion))
                return Conflict();

            var now = DateTime.UtcNow;
            var catalogBefore = await _catalogVersions.GetAsync(item.StoreId, cancellationToken);
            var oldData = new
            {
                item.IsEnabled,
                item.PublishedAtUtc,
                item.PublishedByStaffId,
                item.DisplayOrder,
                item.PauseReason
            };
            switch (request.Action)
            {
                case StoreMenuLifecycleActions.Publish:
                    item.PublishedAtUtc ??= now;
                    item.PublishedByStaffId = actorStaffId;
                    item.IsEnabled = true;
                    item.PauseReason = null;
                    break;
                case StoreMenuLifecycleActions.Pause:
                    if (!item.PublishedAtUtc.HasValue)
                        return ServiceResult<StoreMenuWorkspaceRowDto>.Failure("SKU chưa publish nên không thể tạm dừng.");
                    item.IsEnabled = false;
                    item.PauseReason = request.Reason.Trim();
                    break;
                case StoreMenuLifecycleActions.Resume:
                    if (!item.PublishedAtUtc.HasValue)
                        return ServiceResult<StoreMenuWorkspaceRowDto>.Failure("SKU chưa publish nên không thể mở bán lại.");
                    item.IsEnabled = true;
                    item.PauseReason = null;
                    break;
                case StoreMenuLifecycleActions.ChangeDisplayOrder:
                    if (request.DisplayOrder is null or < 0)
                        return ServiceResult<StoreMenuWorkspaceRowDto>.Failure("Thứ tự hiển thị phải từ 0 trở lên.");
                    item.DisplayOrder = request.DisplayOrder.Value;
                    break;
            }

            _context.Entry(item).Property(x => x.RowVersion).OriginalValue = expected;
            item.UpdatedAtUtc = now;
            var catalogVersions = await _catalogVersions.InvalidateAsync(
                new[] { item.StoreId }, now, cancellationToken);

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                _context.StoreMenuItemAudits.Add(new StoreMenuItemAudit
                {
                    StoreMenuItemId = item.StoreMenuItemId,
                    StoreId = item.StoreId,
                    DrinkSizeId = item.DrinkSizeId,
                    Action = request.Action,
                    OldIsEnabled = oldData.IsEnabled,
                    NewIsEnabled = item.IsEnabled,
                    OldPriceOverride = item.PriceOverride,
                    NewPriceOverride = item.PriceOverride,
                    OldEffectiveFromUtc = item.EffectiveFromUtc,
                    NewEffectiveFromUtc = item.EffectiveFromUtc,
                    OldEffectiveToUtc = item.EffectiveToUtc,
                    NewEffectiveToUtc = item.EffectiveToUtc,
                    CatalogVersionBefore = catalogBefore.Version,
                    CatalogVersionAfter = catalogVersions[item.StoreId],
                    ItemRowVersionBefore = expected.ToArray(),
                    ItemRowVersionAfter = item.RowVersion.ToArray(),
                    OldDataJson = JsonSerializer.Serialize(oldData),
                    NewDataJson = JsonSerializer.Serialize(new
                    {
                        item.IsEnabled,
                        item.PublishedAtUtc,
                        item.PublishedByStaffId,
                        item.DisplayOrder,
                        item.PauseReason
                    }),
                    ActorStaffId = actorStaffId,
                    Reason = request.Reason.Trim(),
                    CreatedAtUtc = now
                });
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Conflict();
            }

            var operational = await _availability.EvaluateAsync(
                item.StoreId, item.DrinkSizeId, now, cancellationToken);
            var profitability = await _profitability.PreviewAsync(
                item.StoreId, item.DrinkSize.DrinkId, now, actorStaffId, cancellationToken);
            var cost = profitability.IsSuccess
                ? profitability.Data?.Sizes.FirstOrDefault(x => x.DrinkSizeId == item.DrinkSizeId)
                : null;
            return ServiceResult<StoreMenuWorkspaceRowDto>.Success(
                Map(item, operational, cost),
                LifecycleMessage(request.Action));
        }

        private async Task<bool> CanReadStoreAsync(int staffId, int storeId, CancellationToken cancellationToken)
        {
            var roles = await GetRolesAsync(staffId, cancellationToken);
            if (roles.Contains(RoleConstants.BusinessOwner)
                || roles.Contains(RoleConstants.AccountantWarehouse)
                || roles.Contains(RoleConstants.SystemAdmin))
                return true;
            if (roles.Contains(RoleConstants.AreaManager))
                return await _scopeAuthorization.CanAccessStoreAsync(staffId, storeId);
            return roles.Contains(RoleConstants.StoreManager)
                && await CanAccessManagedStoreAsync(staffId, storeId, cancellationToken);
        }

        private async Task<bool> CanAccessManagedStoreAsync(int staffId, int storeId, CancellationToken cancellationToken) =>
            await _context.Staffs.AsNoTracking().AnyAsync(
                x => x.StaffId == staffId && x.Active && x.StoreId == storeId,
                cancellationToken)
            || await _scopeAuthorization.CanAccessStoreAsync(staffId, storeId);

        private Task<List<string>> GetRolesAsync(int staffId, CancellationToken cancellationToken) =>
            _context.Staffs.AsNoTracking()
                .Where(x => x.StaffId == staffId && x.Active && x.Account.Active)
                .SelectMany(x => x.Account.AccountRoles.Where(r => r.Role.Active).Select(r => r.Role.Name))
                .Distinct()
                .ToListAsync(cancellationToken);

        private static StoreMenuWorkspaceRowDto Map(
            StoreMenuItem item,
            StoreMenuAvailabilityDto operational,
            DrinkSizeProfitabilityRowDto? cost)
        {
            var effectivePrice = item.GetEffectivePrice();
            var fifoCost = cost?.EstimatedCost;
            return new StoreMenuWorkspaceRowDto
            {
                StoreMenuItemId = item.StoreMenuItemId,
                StoreId = item.StoreId,
                DrinkId = item.DrinkSize.DrinkId,
                DrinkSizeId = item.DrinkSizeId,
                DrinkCode = item.DrinkSize.Drink.DrinkCode,
                DrinkName = item.DrinkSize.Drink.Name,
                SizeName = item.DrinkSize.Size.Name,
                CategoryName = item.DrinkSize.Drink.Category?.Name ?? "Chưa phân loại",
                ConfiguredStatus = operational.ConfiguredStatus,
                OperationalStatus = operational.OperationalStatus,
                AvailabilityReason = operational.Reason,
                IsSellable = operational.IsSellable,
                GlobalPrice = item.DrinkSize.Price,
                StoreOverride = item.PriceOverride,
                EffectivePrice = effectivePrice,
                PriceSource = item.GetPriceSource(),
                FifoCost = fifoCost,
                CostStatus = cost?.CostStatus ?? "UNKNOWN",
                EstimatedGrossMarginPercent = fifoCost.HasValue && effectivePrice > 0
                    ? decimal.Round((effectivePrice - fifoCost.Value) / effectivePrice * 100m, 2)
                    : null,
                EffectiveFromUtc = item.EffectiveFromUtc,
                EffectiveToUtc = item.EffectiveToUtc,
                DisplayOrder = item.DisplayOrder,
                PauseReason = item.PauseReason,
                RecipeId = cost?.RecipeId,
                RowVersion = item.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(item.RowVersion)
            };
        }

        private static ServiceResult<StoreMenuWorkspaceRowDto> Conflict() =>
            ServiceResult<StoreMenuWorkspaceRowDto>.Failure(
                "Menu đã được người khác cập nhật. Vui lòng tải lại.",
                errorCode: "STORE_MENU_CHANGED_BY_ANOTHER_USER");

        private static string LifecycleMessage(string action) => action switch
        {
            StoreMenuLifecycleActions.Publish => "Đã publish SKU lên menu cửa hàng.",
            StoreMenuLifecycleActions.Pause => "Đã tạm dừng SKU tại cửa hàng.",
            StoreMenuLifecycleActions.Resume => "Đã mở bán lại SKU tại cửa hàng.",
            _ => "Đã cập nhật thứ tự hiển thị."
        };
    }
}
