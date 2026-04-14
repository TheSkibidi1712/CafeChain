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

        // ================= INVENTORY =================
        public async Task<(List<InventoryDTO>, int total)> GetInventoryByStaffAsync(int accountId, string? search, int page, int pageSize)
        {
            var storeId = await GetStoreIdFromStaff(accountId);

            var (data, total) = await _repo.GetPagedAsync(storeId, search, page, pageSize);

            var result = data.Select(x => new InventoryDTO
            {
                StoreInventoryId = x.StoreInventoryId,
                IngredientName = x.Ingredient.Name,
                AvailableQty = x.AvailableQty,
                ReservedQty = x.ReservedQty,
                LastUpdated = x.LastUpdated,
                UnitCode = x.Ingredient.BaseUnit.UnitCode
            }).ToList();

            return (result, total);
        }

        // ================= TRANSACTION =================
        public async Task<(List<InventoryTransactionDTO>, int total)> GetTransactionsByInventoryAsync(int accountId, int storeInventoryId, int page, int pageSize)
        {
            await GetStoreIdFromStaff(accountId); // validate quyền

            var (data, total) = await _repo
                .GetTransactionsByInventoryIdAsync(storeInventoryId, page, pageSize);

            var result = data.Select(x => new InventoryTransactionDTO
            {
                IngredientName = x.StoreInventory.Ingredient.Name,
                TypeName = x.TransactionType.Name,
                Quantity = x.Quantity,
                BeforeQty = x.BeforeQty,
                AfterQty = x.AfterQty,
                CreatedAt = x.CreatedAt,
                UnitCode = x.StoreInventory.Ingredient.BaseUnit.UnitCode
            }).ToList();

            return (result, total);
        }

        // ================= ALL TRANSACTIONS BY STAFF =================
        public async Task<(List<InventoryTransactionDTO>, int total)> GetAllTransactionsByStaffAsync(int accountId, int page, int pageSize)
        {
            var storeId = await GetStoreIdFromStaff(accountId);

            var (data, total) = await _repo
                .GetTransactionsByStoreIdAsync(storeId, page, pageSize);

            var result = data.Select(x => new InventoryTransactionDTO
            {
                IngredientName = x.StoreInventory.Ingredient.Name,
                TypeName = x.TransactionType.Name,
                Quantity = x.Quantity,
                BeforeQty = x.BeforeQty,
                AfterQty = x.AfterQty,
                CreatedAt = x.CreatedAt,
                UnitCode = x.StoreInventory.Ingredient.BaseUnit.UnitCode
            }).ToList();

            return (result, total);
        }

        // ================= PRIVATE (CORE LOGIC) =================

        private async Task<int> GetStoreIdFromStaff(int accountId)
        {
            var staff = await _repo.GetStaffByAccountIdAsync(accountId);

            if (staff == null)
                throw new Exception("Staff not found");

            ValidateStaffScope(staff);

            return staff.StoreId;
        }

        private void ValidateStaffScope(Staff staff)
        {
            var hasStoreScope = staff.StaffScopes
                .Any(x => x.ScopeType.Code == "STORE");

            if (!hasStoreScope)
                throw new Exception("No permission for this store");
        }
    }
}
