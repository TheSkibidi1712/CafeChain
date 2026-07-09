using CafeChain.Application.DTOs.Admin.StoreInventories;
using CafeChain.Application.Interfaces.Admin.StoreInventories;
using CafeChain.Infrastrusture.Interfaces.Admin.StoreInventories;

namespace CafeChain.Application.Services.Admin.StoreInventories
{
    public class AdminStoreInventoryService : IAdminStoreInventoryService
    {
        private const int MaxPageSize = 100;

        private readonly IAdminStoreInventoryRepository _repo;

        public AdminStoreInventoryService(IAdminStoreInventoryRepository repo)
        {
            _repo = repo;
        }

        // =====================================================
        // INVENTORY
        // =====================================================

        public async Task<(List<InventoryDTO> data, int total)> GetInventoryByStaffAsync(
            int accountId,
            int storeId,
            string? search,
            int page,
            int pageSize)
        {
            var stores = await GetAllowedStoresAsync(accountId);
            var storeIds = GetStoreIds(stores);

            if (!storeIds.Any())
                return (new List<InventoryDTO>(), 0);

            if (!CanAccessStore(storeIds, storeId))
                return (new List<InventoryDTO>(), 0);

            return await _repo.GetPagedAsync(
                storeIds,
                storeId,
                NormalizeSearch(search),
                NormalizePage(page),
                NormalizePageSize(pageSize));
        }

        // =====================================================
        // STORE TABS
        // =====================================================

        public async Task<List<InventoryStoreDTO>> GetStoresByStaffAsync(
            int accountId)
        {
            return await GetAllowedStoresAsync(accountId);
        }

        // =====================================================
        // ALL TRANSACTIONS
        // =====================================================

        public async Task<(List<InventoryTransactionDTO> data, int total)> GetAllTransactionsByStaffAsync(
            int accountId,
            int storeId,
            int page,
            int pageSize)
        {
            var stores = await GetAllowedStoresAsync(accountId);
            var storeIds = GetStoreIds(stores);

            if (!storeIds.Any())
                return (new List<InventoryTransactionDTO>(), 0);

            if (!CanAccessStore(storeIds, storeId))
                return (new List<InventoryTransactionDTO>(), 0);

            var (data, total) = await _repo.GetTransactionsByStoreIdsAsync(
                storeIds,
                storeId,
                NormalizePage(page),
                NormalizePageSize(pageSize));

            return (NormalizeTransactions(data), total);
        }

        // =====================================================
        // TRANSACTION BY INVENTORY
        // =====================================================

        public async Task<(List<InventoryTransactionDTO> data, int total)> GetTransactionsByInventoryAsync(
            int accountId,
            int storeInventoryId,
            int page,
            int pageSize)
        {
            var stores = await GetAllowedStoresAsync(accountId);
            var storeIds = GetStoreIds(stores);

            if (!storeIds.Any() || storeInventoryId <= 0)
                return (new List<InventoryTransactionDTO>(), 0);

            var (data, total) = await _repo.GetTransactionsByStoreInventoryIdAsync(
                storeIds,
                storeInventoryId,
                NormalizePage(page),
                NormalizePageSize(pageSize));

            return (NormalizeTransactions(data), total);
        }

        // =====================================================
        // PRIVATE - STORE SCOPE
        // =====================================================

        private async Task<List<InventoryStoreDTO>> GetAllowedStoresAsync(
            int accountId)
        {
            if (accountId <= 0)
                return new List<InventoryStoreDTO>();

            return await _repo.GetAccessibleStoresByAccountIdAsync(accountId);
        }

        private static List<int> GetStoreIds(
            IEnumerable<InventoryStoreDTO> stores)
        {
            return stores
                .Select(x => x.StoreId)
                .Distinct()
                .ToList();
        }

        private static bool CanAccessStore(
            List<int> allowedStoreIds,
            int selectedStoreId)
        {
            return selectedStoreId <= 0 || allowedStoreIds.Contains(selectedStoreId);
        }

        // =====================================================
        // PRIVATE - TRANSACTION FORMAT
        // =====================================================

        private static List<InventoryTransactionDTO> NormalizeTransactions(
            List<InventoryTransactionDTO> transactions)
        {
            foreach (var item in transactions)
            {
                var rawTypeName = item.TypeName;

                item.Quantity = NormalizeQuantityForDisplay(
                    item.Quantity,
                    item.BeforeQty,
                    item.AfterQty,
                    rawTypeName);

                item.TypeName = CafeChain.Helpers.InventoryVietnameseMapper.ToVietnameseTransactionType(rawTypeName);
                item.StockStatusName = CafeChain.Helpers.InventoryVietnameseMapper.ToVietnameseStockStatus(item.StockStatusName);
            }

            return transactions;
        }

        private static decimal NormalizeQuantityForDisplay(
            decimal quantity,
            decimal beforeQty,
            decimal afterQty,
            string typeName)
        {
            var delta = afterQty - beforeQty;

            if (delta != 0 || quantity == 0)
                return delta;

            return IsOutputTransaction(typeName)
                ? -quantity
                : quantity;
        }

        private static bool IsOutputTransaction(
            string typeName)
        {
            var value = NormalizeEnumText(typeName);
            var tokens = GetEnumTokens(value);

            if (value.Contains("TRANSFER_IN") ||
                value.Contains("IMPORT") ||
                value.Contains("INCREASE") ||
                tokens.Contains("IN") ||
                value.Contains("NHAP"))
            {
                return false;
            }

            return value.Contains("TRANSFER_OUT") ||
                   value.Contains("EXPORT") ||
                   value.Contains("WASTE") ||
                   tokens.Contains("OUT") ||
                   value.Contains("ISSUE") ||
                   value.Contains("SALE") ||
                   value.Contains("SELL") ||
                   value.Contains("CONSUME") ||
                   value.Contains("DECREASE") ||
                   value.Contains("XUAT");
        }

        private static string ToVietnameseTransactionType(
            string typeName)
        {
            var value = NormalizeEnumText(typeName);

            if (value.Contains("TRANSFER_IN"))
                return "Nhập chuyển kho";

            if (value.Contains("TRANSFER_OUT"))
                return "Xuất chuyển kho";

            var tokens = GetEnumTokens(value);

            if (value.Contains("IMPORT") ||
                value.Contains("INCREASE") ||
                tokens.Contains("IN") ||
                value.Contains("NHAP"))
            {
                return "Nhập kho";
            }

            if (value.Contains("EXPORT") ||
                tokens.Contains("OUT") ||
                value.Contains("SALE") ||
                value.Contains("SELL") ||
                value.Contains("CONSUME") ||
                value.Contains("XUAT"))
            {
                return "Xuất kho";
            }

            if (value.Contains("ADJUST") || value.Contains("KIEMKE"))
                return "Điều chỉnh";

            return string.IsNullOrWhiteSpace(typeName)
                ? "Không xác định"
                : typeName;
        }

        private static string ToVietnameseStockStatus(
            string stockStatus)
        {
            var value = NormalizeEnumText(stockStatus);

            if (value.Contains("NEGATIVE") || value.Contains("AM_KHO"))
                return "Âm kho";

            if (value.Contains("OUT"))
                return "Hết hàng";

            if (value.Contains("LOW"))
                return "Sắp hết";

            if (value.Contains("NORMAL") || value.Contains("AVAILABLE") || value.Contains("IN_STOCK"))
                return "Còn hàng";

            return string.IsNullOrWhiteSpace(stockStatus)
                ? "Không xác định"
                : stockStatus;
        }

        private static string NormalizeEnumText(
            string? value)
        {
            return (value ?? string.Empty)
                .Trim()
                .Replace("-", "_")
                .Replace(" ", "_")
                .ToUpperInvariant();
        }

        private static HashSet<string> GetEnumTokens(
            string normalizedValue)
        {
            return normalizedValue
                .Split('_', StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        // =====================================================
        // PRIVATE - INPUT NORMALIZATION
        // =====================================================

        private static string? NormalizeSearch(
            string? search)
        {
            return string.IsNullOrWhiteSpace(search)
                ? null
                : search.Trim();
        }

        private static int NormalizePage(
            int page)
        {
            return page < 1 ? 1 : page;
        }

        private static int NormalizePageSize(
            int pageSize)
        {
            if (pageSize < 1)
                return 10;

            return pageSize > MaxPageSize
                ? MaxPageSize
                : pageSize;
        }
    }
}
