using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Data;
using CafeChain.Models.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Inventories
{
    public sealed class StoreInventoryWriteResolver : IStoreInventoryWriteResolver
    {
        private readonly AppDbContext _context;
        private readonly IInventoryWriterModeService _modeService;

        public StoreInventoryWriteResolver(AppDbContext context, IInventoryWriterModeService modeService)
        {
            _context = context;
            _modeService = modeService;
        }

        public async Task<StoreInventoryWriteResolution> ResolveAsync(StoreInventoryWriteRequest request)
        {
            if (!_modeService.IsSnapshotValidForCurrentTransaction(request.ModeSnapshot, request.StoreId))
                return Result(InventoryWriteResolutionStatuses.ConcurrencyConflict, "Mode snapshot không thuộc transaction hiện tại.");

            if (request.IdentityType == InventoryWriteIdentityTypes.Ingredient)
                return await ResolveIngredientAsync(request);

            if (request.ModeSnapshot.WriterMode == InventoryWriterMode.Blocked)
                return Result(InventoryWriteResolutionStatuses.BlockedMode, "Kho BTP đang bị khóa.");

            if (request.IdentityType == InventoryWriteIdentityTypes.LegacyRecipe)
                return await ResolveLegacyAsync(request);

            if (request.IdentityType == InventoryWriteIdentityTypes.PreparedItem)
                return await ResolvePreparedItemAsync(request);

            return Result(InventoryWriteResolutionStatuses.InvalidMapping, "Loại identity ghi kho không hợp lệ.");
        }

        private async Task<StoreInventoryWriteResolution> ResolveIngredientAsync(StoreInventoryWriteRequest request)
        {
            if (!request.IngredientId.HasValue)
                return Result(InventoryWriteResolutionStatuses.InvalidMapping, "Thiếu IngredientId.");

            var row = await _context.StoreInventories.FirstOrDefaultAsync(x =>
                x.StoreId == request.StoreId && x.IngredientId == request.IngredientId.Value);
            return row != null
                ? Found(row)
                : request.AllowCreateIntent
                    ? Result(InventoryWriteResolutionStatuses.CreateAllowed, "Có thể tạo dòng tồn Ingredient.")
                    : Result(InventoryWriteResolutionStatuses.NotFound, "Không tìm thấy tồn Ingredient.");
        }

        private async Task<StoreInventoryWriteResolution> ResolveLegacyAsync(StoreInventoryWriteRequest request)
        {
            if (request.ModeSnapshot.WriterMode != InventoryWriterMode.LegacyRecipe)
                return Result(InventoryWriteResolutionStatuses.BlockedMode, "Recipe writer không được phép trong mode hiện tại.");
            if (!request.RecipeId.HasValue)
                return Result(InventoryWriteResolutionStatuses.InvalidMapping, "Thiếu RecipeId.");

            var row = await _context.StoreInventories.FirstOrDefaultAsync(x =>
                x.StoreId == request.StoreId && x.RecipeId == request.RecipeId.Value);
            return row != null
                ? Found(row)
                : request.AllowCreateIntent
                    ? Result(InventoryWriteResolutionStatuses.CreateAllowed, "Có thể tạo dòng tồn Recipe legacy.")
                    : Result(InventoryWriteResolutionStatuses.NotFound, "Không tìm thấy tồn Recipe legacy.");
        }

        private async Task<StoreInventoryWriteResolution> ResolvePreparedItemAsync(StoreInventoryWriteRequest request)
        {
            if (request.ModeSnapshot.WriterMode != InventoryWriterMode.PreparedItem)
                return Result(InventoryWriteResolutionStatuses.BlockedMode, "PreparedItem writer chưa được bật.");
            if (!request.PreparedItemId.HasValue)
                return Result(InventoryWriteResolutionStatuses.InvalidMapping, "Thiếu PreparedItemId.");

            var rows = await _context.StoreInventories
                .Include(x => x.PreparedItem)
                .Where(x => x.StoreId == request.StoreId && x.PreparedItemId == request.PreparedItemId.Value)
                .OrderBy(x => x.StoreInventoryId)
                .ToListAsync();

            var canonical = rows.Where(x => x.BtpIdentityState == BtpIdentityState.Canonical).ToList();
            if (canonical.Count > 1 || rows.Any(x => x.BtpIdentityState == BtpIdentityState.Legacy))
                return Result(InventoryWriteResolutionStatuses.Collision, "PreparedItem còn collision identity chưa xử lý.");
            if (canonical.Count == 0)
            {
                if (rows.Any(x => x.BtpIdentityState == BtpIdentityState.Superseded))
                    return Result(InventoryWriteResolutionStatuses.Superseded, "Chỉ tìm thấy dòng tồn superseded.");

                return request.AllowCreateIntent
                    ? Result(InventoryWriteResolutionStatuses.CreateAllowed, "Có thể tạo canonical PreparedItem sau khi writer được triển khai.")
                    : Result(InventoryWriteResolutionStatuses.NotFound, "Không tìm thấy canonical PreparedItem.");
            }

            var row = canonical[0];
            if (row.QuantitySemanticsStatus != InventoryQuantitySemanticsStatus.BaseUnitConfirmed)
                return Result(InventoryWriteResolutionStatuses.UnknownQuantitySemantics, "Chưa xác nhận quantity theo base unit.");
            if (row.PreparedItem == null || !row.PreparedItem.Active)
                return Result(InventoryWriteResolutionStatuses.InvalidMapping, "PreparedItem không hợp lệ.");
            if (request.NormalizedBaseUnitId.HasValue
                && request.NormalizedBaseUnitId.Value != row.PreparedItem.BaseUnitId)
                return Result(InventoryWriteResolutionStatuses.UnitMismatch, "Đơn vị mutation không phải PreparedItem base unit.");
            if (row.RecipeId.HasValue && request.SourceRecipeId.HasValue && row.RecipeId != request.SourceRecipeId)
                return Result(InventoryWriteResolutionStatuses.InvalidMapping, "Compatibility Recipe không khớp source Recipe.");

            return Found(row);
        }

        private static StoreInventoryWriteResolution Found(Models.Stores.StoreInventory row) => new()
        {
            Status = InventoryWriteResolutionStatuses.FoundCanonical,
            StoreInventory = row,
            Message = "Đã resolve dòng tồn có thể ghi."
        };

        private static StoreInventoryWriteResolution Result(string status, string message) => new()
        {
            Status = status,
            Message = message
        };
    }
}
