using CafeChain.Application.DTOs.Admin.StoreInventories;
using CafeChain.Application.Interfaces.Admin.StoreInventories;
using CafeChain.Infrastrusture.Interfaces.Admin.StoreInventories;
using CafeChain.Models.Staffs;

namespace CafeChain.Application.Services.Admin.StoreInventories
{
    public class AdminStoreInventoryService : IAdminStoreInventoryService
    {
        private readonly IAdminStoreInventoryRepository _repo;

        public AdminStoreInventoryService(IAdminStoreInventoryRepository repo)
        {
            _repo = repo;
        }

        // =====================================================
        // INVENTORY
        // =====================================================

        public async Task<(List<InventoryDTO>, int total)> GetInventoryByStaffAsync(
            int accountId,
            int storeId,
            string? search,
            int page,
            int pageSize)
        {
            var storeIds = await GetStoreIdsFromStaff(accountId);

            return await _repo.GetPagedAsync(
                storeIds,
                storeId,
                search,
                page,
                pageSize);
        }

        // =====================================================
        // STORE TABS
        // =====================================================

        public async Task<List<InventoryStoreDTO>> GetStoresByStaffAsync(
            int accountId)
        {
            var storeIds = await GetStoreIdsFromStaff(accountId);

            var (data, _) = await _repo.GetPagedAsync(
                storeIds,
                0,
                null,
                1,
                9999);

            return data
                .GroupBy(x => new
                {
                    x.StoreId,
                    x.StoreName
                })
                .Select(x => new InventoryStoreDTO
                {
                    StoreId = x.Key.StoreId,
                    StoreName = x.Key.StoreName
                })
                .OrderBy(x => x.StoreName)
                .ToList();
        }

        // =====================================================
        // ALL TRANSACTIONS
        // =====================================================

        public async Task<(List<InventoryTransactionDTO>, int total)> GetAllTransactionsByStaffAsync(
            int accountId,
            int storeId,
            int page,
            int pageSize)
        {
            var storeIds = await GetStoreIdsFromStaff(accountId);

            var (data, total) = await _repo.GetTransactionsByStoreIdsAsync(
                storeIds,
                storeId,
                page,
                pageSize);

            var result = data
                .Select(x =>
                {
                    var detail = x.InventoryDocument?.Details?
                        .Where(d =>
                            d.IngredientId == x.StoreInventory.IngredientId)
                        .FirstOrDefault();

                    return new InventoryTransactionDTO
                    {
                        StoreId = x.StoreInventory.StoreId,
                        StoreName = x.StoreInventory.Store.Name,

                        IngredientName = x.StoreInventory.Ingredient.Name,
                        TypeName = x.Type.ToString(),

                        Quantity = x.Quantity,
                        BeforeQty = x.BeforeQty,
                        AfterQty = x.AfterQty,

                        CreatedAt = x.CreatedAt,
                        UnitCode = x.StoreInventory.Ingredient.BaseUnit.UnitCode,

                        UnitPrice = detail?.UnitPrice,
                        TotalAmount = detail?.TotalAmount
                    };
                })
                .ToList();

            return (result, total);
        }

        // =====================================================
        // TRANSACTION BY INVENTORY
        // =====================================================

        public async Task<(List<InventoryTransactionDTO>, int total)> GetTransactionsByInventoryAsync(
            int accountId,
            int storeInventoryId,
            int page,
            int pageSize)
        {
            var storeIds = await GetStoreIdsFromStaff(accountId);

            var (data, total) = await _repo.GetTransactionsByStoreIdsAsync(
                storeIds,
                0,
                page,
                pageSize);

            var result = data
                .Where(x => x.StoreInventoryId == storeInventoryId)
                .Select(x =>
                {
                    var detail = x.InventoryDocument?.Details?
                        .Where(d =>
                            d.IngredientId == x.StoreInventory.IngredientId)
                        .OrderByDescending(d =>
                            x.InventoryDocument.DocumentDate)
                        .FirstOrDefault();

                    return new InventoryTransactionDTO
                    {
                        IngredientName = x.StoreInventory.Ingredient.Name,
                        TypeName = x.Type.ToString(),

                        Quantity = x.Quantity,
                        BeforeQty = x.BeforeQty,
                        AfterQty = x.AfterQty,

                        CreatedAt = x.CreatedAt,
                        UnitCode = x.StoreInventory.Ingredient.BaseUnit.UnitCode,

                        UnitPrice = detail?.UnitPrice,
                        TotalAmount = detail?.TotalAmount
                    };
                })
                .ToList();

            return (result, result.Count);
        }

        // =====================================================
        // PRIVATE
        // =====================================================

        private async Task<List<int>> GetStoreIdsFromStaff(
            int accountId)
        {
            var staff = await _repo.GetStaffByAccountIdAsync(accountId);

            if (staff == null)
                throw new Exception("Staff not found");

            var storeIds = staff.StaffScopes
                .Where(x =>
                    x.ScopeType != null &&
                    x.ScopeType.Code == "STORE" &&
                    x.ScopeRefId > 0)
                .Select(x => x.ScopeRefId)
                .Distinct()
                .ToList();

            if (!storeIds.Any() && staff.StoreId > 0)
            {
                storeIds.Add(staff.StoreId);
            }

            if (!storeIds.Any())
                throw new Exception("No store assigned");

            return storeIds;
        }
    }
}