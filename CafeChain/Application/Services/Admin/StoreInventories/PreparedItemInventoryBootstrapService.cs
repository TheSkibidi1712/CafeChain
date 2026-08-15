using CafeChain.Application.Interfaces.Admin.StoreInventories;
using CafeChain.Application.Results;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.StoreInventories;

public sealed class PreparedItemInventoryBootstrapService
    : IPreparedItemInventoryBootstrapService
{
    private readonly AppDbContext _context;
    private readonly TimeProvider _timeProvider;

    public PreparedItemInventoryBootstrapService(
        AppDbContext context,
        TimeProvider? timeProvider = null)
    {
        _context = context;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ServiceResult<StoreInventory>> EnsureAsync(
        int storeId,
        int preparedItemId,
        int actorAccountId,
        string evidenceReference)
    {
        if (storeId <= 0 || preparedItemId <= 0 || actorAccountId <= 0)
        {
            return ServiceResult<StoreInventory>.Failure(
                "Thông tin khởi tạo tồn kho bán thành phẩm chưa hợp lệ.",
                errorCode: "PREPARED_ITEM_INVENTORY_BOOTSTRAP_INVALID");
        }

        if (string.IsNullOrWhiteSpace(evidenceReference))
        {
            return ServiceResult<StoreInventory>.Failure(
                "Thiếu căn cứ khởi tạo tồn kho bán thành phẩm.",
                errorCode: "PREPARED_ITEM_INVENTORY_BOOTSTRAP_EVIDENCE_REQUIRED");
        }

        var storeActive = await _context.Stores
            .AsNoTracking()
            .AnyAsync(x => x.StoreId == storeId && x.Active);
        var preparedItemActive = await _context.PreparedItems
            .AsNoTracking()
            .AnyAsync(x => x.PreparedItemId == preparedItemId && x.Active);
        if (!storeActive || !preparedItemActive)
        {
            return ServiceResult<StoreInventory>.Failure(
                "Cửa hàng hoặc bán thành phẩm không tồn tại hay đã ngừng hoạt động.",
                errorCode: "PREPARED_ITEM_INVENTORY_BOOTSTRAP_NOT_AVAILABLE");
        }

        var resolved = await ResolveExistingAsync(storeId, preparedItemId);
        if (!resolved.IsSuccess || resolved.Data != null)
            return resolved;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var row = new StoreInventory
        {
            StoreId = storeId,
            PreparedItemId = preparedItemId,
            BtpIdentityState = BtpIdentityState.Canonical,
            QuantitySemanticsStatus = InventoryQuantitySemanticsStatus.BaseUnitConfirmed,
            QuantitySemanticsEvidenceType = QuantitySemanticsEvidenceType.SystemCanonicalCreation,
            QuantitySemanticsEvidenceReference = evidenceReference.Trim(),
            QuantitySemanticsReviewedAt = now,
            QuantitySemanticsReviewedByAccountId = actorAccountId,
            AvailableQty = 0m,
            ReservedQty = 0m,
            MinStockLevel = null,
            TargetStockLevel = null,
            MaxNegativeQty = 0m,
            LastUpdated = now
        };

        _context.StoreInventories.Add(row);
        try
        {
            await _context.SaveChangesAsync();
            return ServiceResult<StoreInventory>.Success(
                row,
                "Đã khởi tạo tồn kho bán thành phẩm tại cửa hàng với số lượng bằng 0.");
        }
        catch (DbUpdateException)
        {
            _context.Entry(row).State = EntityState.Detached;
            var winner = await ResolveExistingAsync(storeId, preparedItemId);
            if (winner.IsSuccess && winner.Data != null)
                return winner;

            return ServiceResult<StoreInventory>.Failure(
                "Không thể khởi tạo tồn kho bán thành phẩm do dữ liệu vừa được thay đổi.",
                errorCode: "PREPARED_ITEM_INVENTORY_BOOTSTRAP_CONFLICT");
        }
    }

    private async Task<ServiceResult<StoreInventory>> ResolveExistingAsync(
        int storeId,
        int preparedItemId)
    {
        var rows = await _context.StoreInventories
            .Where(x => x.StoreId == storeId && x.PreparedItemId == preparedItemId)
            .OrderBy(x => x.StoreInventoryId)
            .ToListAsync();
        var canonical = rows
            .Where(x => x.BtpIdentityState == BtpIdentityState.Canonical
                && !x.SupersededByStoreInventoryId.HasValue)
            .ToList();

        if (canonical.Count > 1 || rows.Any(x => x.BtpIdentityState == BtpIdentityState.Legacy))
        {
            return ServiceResult<StoreInventory>.Failure(
                "Bán thành phẩm còn dữ liệu tồn kho cũ cần đối soát trước khi khởi tạo.",
                errorCode: "PREPARED_ITEM_INVENTORY_BOOTSTRAP_COLLISION");
        }

        if (canonical.Count == 0)
            return ServiceResult<StoreInventory>.Success(null!);

        var row = canonical[0];
        if (row.QuantitySemanticsStatus != InventoryQuantitySemanticsStatus.BaseUnitConfirmed)
        {
            return ServiceResult<StoreInventory>.Failure(
                "Tồn kho bán thành phẩm chưa xác nhận đơn vị cơ sở.",
                errorCode: "PREPARED_ITEM_INVENTORY_BOOTSTRAP_UNIT_UNCONFIRMED");
        }

        return ServiceResult<StoreInventory>.Success(
            row,
            "Tồn kho bán thành phẩm tại cửa hàng đã tồn tại.");
    }
}
