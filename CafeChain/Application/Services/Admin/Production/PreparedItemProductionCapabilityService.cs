using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Production;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Production;
using CafeChain.Application.Interfaces.Admin.StoreInventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Production;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Production;

public sealed class PreparedItemProductionCapabilityService
    : IPreparedItemProductionCapabilityService
{
    private const int MaxPageSize = 100;
    private readonly AppDbContext _context;
    private readonly IAdminPermissionService _permissions;
    private readonly IPreparedItemInventoryBootstrapService _inventoryBootstrap;
    private readonly TimeProvider _timeProvider;

    public PreparedItemProductionCapabilityService(
        AppDbContext context,
        IAdminPermissionService permissions,
        IPreparedItemInventoryBootstrapService inventoryBootstrap,
        TimeProvider? timeProvider = null)
    {
        _context = context;
        _permissions = permissions;
        _inventoryBootstrap = inventoryBootstrap;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ServiceResult<PreparedItemProductionCapabilityPageDto>> GetPageAsync(
        int actorAccountId,
        int storeId,
        string? search,
        int page,
        int pageSize)
    {
        if (actorAccountId <= 0 || storeId <= 0)
            return ServiceResult<PreparedItemProductionCapabilityPageDto>.Failure("Thông tin cửa hàng chưa hợp lệ.");

        var view = await _permissions.HasPermissionAsync(
            actorAccountId,
            PermissionConstants.ProductionOrderView,
            storeId);
        if (!view.IsSuccess || view.Data?.Allowed != true)
            return ServiceResult<PreparedItemProductionCapabilityPageDto>.Failure("Bạn không có quyền xem năng lực sản xuất tại cửa hàng này.");

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var query = _context.PreparedItems
            .AsNoTracking()
            .Where(x => x.Active);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x => x.Name.Contains(keyword) || x.Code.Contains(keyword));
        }

        var total = await query.CountAsync();
        var preparedItems = await query
            .OrderBy(x => x.Name)
            .ThenBy(x => x.PreparedItemId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.PreparedItemId,
                x.Name,
                x.Code,
                BaseUnitCode = x.BaseUnit.UnitCode
            })
            .ToListAsync();
        var ids = preparedItems.Select(x => x.PreparedItemId).ToList();
        var global = await _context.InventoryItemSourceCapabilities
            .AsNoTracking()
            .Where(x => x.PreparedItemId.HasValue && ids.Contains(x.PreparedItemId.Value))
            .ToDictionaryAsync(x => x.PreparedItemId!.Value);
        var byStore = await _context.StoreProductionCapabilities
            .AsNoTracking()
            .Where(x => x.StoreId == storeId
                && x.PreparedItemId.HasValue
                && ids.Contains(x.PreparedItemId.Value))
            .ToDictionaryAsync(x => x.PreparedItemId!.Value);
        var inventoryIdRows = await _context.StoreInventories
            .AsNoTracking()
            .Where(x => x.StoreId == storeId
                && x.PreparedItemId.HasValue
                && ids.Contains(x.PreparedItemId.Value)
                && x.BtpIdentityState == BtpIdentityState.Canonical
                && x.QuantitySemanticsStatus == InventoryQuantitySemanticsStatus.BaseUnitConfirmed
                && !x.SupersededByStoreInventoryId.HasValue)
            .Select(x => x.PreparedItemId!.Value)
            .ToListAsync();
        var inventoryIds = inventoryIdRows.ToHashSet();
        var storeName = await _context.Stores
            .AsNoTracking()
            .Where(x => x.StoreId == storeId && x.Active)
            .Select(x => x.Name)
            .SingleOrDefaultAsync();
        if (storeName == null)
            return ServiceResult<PreparedItemProductionCapabilityPageDto>.Failure("Không tìm thấy cửa hàng đang hoạt động.");

        var globalPermission = await _permissions.HasPermissionAsync(
            actorAccountId,
            PermissionConstants.PreparedItemUpdate);
        var storePermission = await _permissions.HasPermissionAsync(
            actorAccountId,
            PermissionConstants.ProductionOrderPlan,
            storeId);
        return ServiceResult<PreparedItemProductionCapabilityPageDto>.Success(new()
        {
            StoreId = storeId,
            StoreName = storeName,
            Search = search,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            CanManageGlobalCapability = globalPermission.IsSuccess && globalPermission.Data?.Allowed == true,
            CanManageStoreCapability = storePermission.IsSuccess && storePermission.Data?.Allowed == true,
            Items = preparedItems.Select(x =>
            {
                global.TryGetValue(x.PreparedItemId, out var globalCapability);
                byStore.TryGetValue(x.PreparedItemId, out var storeCapability);
                return new PreparedItemProductionCapabilityItemDto
                {
                    PreparedItemId = x.PreparedItemId,
                    Name = x.Name,
                    Code = x.Code,
                    BaseUnitCode = x.BaseUnitCode,
                    CanProduceGlobally = globalCapability is { Active: true, CanProduce: true },
                    GlobalRowVersion = Encode(globalCapability?.RowVersion),
                    CanProduceAtStore = storeCapability is { Active: true },
                    StoreRowVersion = Encode(storeCapability?.RowVersion),
                    HasCanonicalInventory = inventoryIds.Contains(x.PreparedItemId)
                };
            }).ToList()
        });
    }

    public async Task<ServiceResult> SetGlobalProductionAsync(
        int actorAccountId,
        int actorStaffId,
        int preparedItemId,
        bool enabled,
        string? rowVersion)
    {
        var permission = await _permissions.HasPermissionAsync(
            actorAccountId,
            PermissionConstants.PreparedItemUpdate);
        if (!permission.IsSuccess || permission.Data?.Allowed != true)
            return ServiceResult.Failure("Bạn không có quyền cấu hình khả năng sản xuất của bán thành phẩm.");
        if (!await IsActivePreparedItemAsync(preparedItemId))
            return ServiceResult.Failure("Bán thành phẩm không tồn tại hoặc đã ngừng hoạt động.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var capability = await _context.InventoryItemSourceCapabilities
            .SingleOrDefaultAsync(x => x.PreparedItemId == preparedItemId);
        if (capability == null)
        {
            if (!enabled)
                return ServiceResult.Success("Bán thành phẩm chưa được cho phép sản xuất nội bộ.");
            capability = new InventoryItemSourceCapability
            {
                PreparedItemId = preparedItemId,
                CanProduce = true,
                CanPurchase = false,
                CanTransfer = false,
                Active = true,
                EffectiveFromUtc = now,
                CreatedByStaffId = actorStaffId,
                CreatedAtUtc = now
            };
            _context.InventoryItemSourceCapabilities.Add(capability);
        }
        else
        {
            if (capability.Active && capability.CanProduce == enabled)
                return ServiceResult.Success(enabled
                    ? "Bán thành phẩm đã được cho phép sản xuất nội bộ."
                    : "Bán thành phẩm đã ngừng cho phép sản xuất nội bộ.");
            var concurrency = ApplyRowVersion(capability, rowVersion);
            if (concurrency != null)
                return concurrency;
            capability.Active = true;
            capability.CanProduce = enabled;
            capability.EffectiveFromUtc = enabled ? now : capability.EffectiveFromUtc;
            capability.EffectiveToUtc = null;
            capability.UpdatedByStaffId = actorStaffId;
            capability.UpdatedAtUtc = now;
        }

        return await SaveAsync(enabled
            ? "Đã cho phép bán thành phẩm được sản xuất nội bộ."
            : "Đã ngừng cho phép bán thành phẩm được sản xuất nội bộ.");
    }

    public async Task<ServiceResult> SetStoreProductionAsync(
        int actorAccountId,
        int actorStaffId,
        int storeId,
        int preparedItemId,
        bool enabled,
        string? rowVersion)
    {
        var permission = await _permissions.HasPermissionAsync(
            actorAccountId,
            PermissionConstants.ProductionOrderPlan,
            storeId);
        if (!permission.IsSuccess || permission.Data?.Allowed != true)
            return ServiceResult.Failure("Bạn không có quyền cấu hình năng lực sản xuất tại cửa hàng này.");
        if (!await IsActivePreparedItemAsync(preparedItemId)
            || !await _context.Stores.AsNoTracking().AnyAsync(x => x.StoreId == storeId && x.Active))
            return ServiceResult.Failure("Cửa hàng hoặc bán thành phẩm không tồn tại hay đã ngừng hoạt động.");
        if (enabled && !await _context.InventoryItemSourceCapabilities.AsNoTracking()
            .AnyAsync(x => x.PreparedItemId == preparedItemId && x.Active && x.CanProduce))
            return ServiceResult.Failure("Bán thành phẩm chưa được cho phép sản xuất nội bộ ở cấp toàn chuỗi.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var capability = await _context.StoreProductionCapabilities
                .SingleOrDefaultAsync(x => x.StoreId == storeId && x.PreparedItemId == preparedItemId);
            if (capability != null && capability.Active == enabled)
            {
                if (enabled)
                {
                    var existingBootstrap = await _inventoryBootstrap.EnsureAsync(
                        storeId, preparedItemId, actorAccountId, "StoreProductionCapability");
                    if (!existingBootstrap.IsSuccess)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult.Failure(existingBootstrap.Message, errorCode: existingBootstrap.ErrorCode);
                    }
                }
                await transaction.CommitAsync();
                return ServiceResult.Success(enabled
                    ? "Cửa hàng đã có năng lực sản xuất bán thành phẩm này."
                    : "Cửa hàng đã ngừng năng lực sản xuất bán thành phẩm này.");
            }

            if (capability == null)
            {
                if (!enabled)
                {
                    await transaction.CommitAsync();
                    return ServiceResult.Success("Cửa hàng chưa bật năng lực sản xuất bán thành phẩm này.");
                }
                capability = new StoreProductionCapability
                {
                    StoreId = storeId,
                    PreparedItemId = preparedItemId,
                    Active = true,
                    EffectiveFromUtc = now,
                    CreatedByStaffId = actorStaffId,
                    CreatedAtUtc = now
                };
                _context.StoreProductionCapabilities.Add(capability);
            }
            else
            {
                var concurrency = ApplyRowVersion(capability, rowVersion);
                if (concurrency != null)
                {
                    await transaction.RollbackAsync();
                    return concurrency;
                }
                capability.Active = enabled;
                capability.EffectiveFromUtc = enabled ? now : capability.EffectiveFromUtc;
                capability.EffectiveToUtc = enabled ? null : now;
                capability.UpdatedByStaffId = actorStaffId;
                capability.UpdatedAtUtc = now;
            }

            if (enabled)
            {
                var bootstrap = await _inventoryBootstrap.EnsureAsync(
                    storeId, preparedItemId, actorAccountId, "StoreProductionCapability");
                if (!bootstrap.IsSuccess)
                {
                    await transaction.RollbackAsync();
                    return ServiceResult.Failure(bootstrap.Message, errorCode: bootstrap.ErrorCode);
                }
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return ServiceResult.Success(enabled
                ? "Đã bật năng lực sản xuất bán thành phẩm tại cửa hàng."
                : "Đã tắt năng lực sản xuất bán thành phẩm tại cửa hàng.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return ConcurrencyFailure();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            return ServiceResult.Failure(
                "Dữ liệu năng lực sản xuất vừa được thay đổi. Vui lòng tải lại trang.",
                errorCode: "PRODUCTION_CAPABILITY_CONFLICT");
        }
    }

    private Task<bool> IsActivePreparedItemAsync(int preparedItemId) =>
        _context.PreparedItems.AsNoTracking().AnyAsync(x => x.PreparedItemId == preparedItemId && x.Active);

    private ServiceResult? ApplyRowVersion(object entity, string? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion))
            return ConcurrencyFailure();
        try
        {
            var value = Convert.FromBase64String(rowVersion);
            _context.Entry(entity).Property("RowVersion").OriginalValue = value;
            return null;
        }
        catch (FormatException)
        {
            return ConcurrencyFailure();
        }
    }

    private async Task<ServiceResult> SaveAsync(string message)
    {
        try
        {
            await _context.SaveChangesAsync();
            return ServiceResult.Success(message);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConcurrencyFailure();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Failure(
                "Dữ liệu năng lực sản xuất vừa được thay đổi. Vui lòng tải lại trang.",
                errorCode: "PRODUCTION_CAPABILITY_CONFLICT");
        }
    }

    private static ServiceResult ConcurrencyFailure() => ServiceResult.Failure(
        "Dữ liệu đã được người khác cập nhật. Vui lòng tải lại trang.",
        errorCode: "RESOURCE_CHANGED_BY_ANOTHER_USER");

    private static string? Encode(byte[]? rowVersion) =>
        rowVersion is { Length: > 0 } ? Convert.ToBase64String(rowVersion) : null;
}
