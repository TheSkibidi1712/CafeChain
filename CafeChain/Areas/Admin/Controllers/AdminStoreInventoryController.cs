using CafeChain.Application.DTOs.Admin.StoreInventories;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces.Admin.StoreInventories;
using CafeChain.ViewModels.Admin.StoreInventories;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CafeChain.Areas.Admin.Controllers
{
    public class AdminStoreInventoryController : AdminBaseController
    {
        private const int PageSize = 10;

        private readonly IAdminStoreInventoryService _service;
        private readonly IAdminActorContextAccessor _actor;
        private readonly IAdminStoreScopeResolver _storeScopeResolver;

        public AdminStoreInventoryController(
            IAdminStoreInventoryService service,
            IAdminActorContextAccessor actor,
            IAdminStoreScopeResolver storeScopeResolver)
        {
            _service = service;
            _actor = actor;
            _storeScopeResolver = storeScopeResolver;
        }

        // =====================================================
        // INDEX
        // =====================================================

        public async Task<IActionResult> Index(
            string? search,
            int storeId = 0,
            string inventoryType = InventoryCatalogTypes.Ingredients,
            int page = 1)
        {
            var accountId = GetAccountId();

            if (accountId <= 0)
                return Unauthorized();

            var actor = _actor.Get(User);
            var storeScope = await _storeScopeResolver.ResolveAsync(
                actor,
                storeId > 0 ? storeId : null);
            if (!storeScope.IsResolved)
                return StoreScopeFailure(storeScope);
            var stores = await _service.GetStoresByStaffAsync(accountId);
            var selectedStoreId = storeScope.StoreId!.Value;
            var selectedType = NormalizeInventoryType(inventoryType);

            var (data, total) = await _service.GetInventoryByStaffAsync(
                accountId,
                selectedStoreId,
                selectedType,
                search,
                page,
                PageSize);

            var vm = BuildIndexViewModel(
                selectedStoreId,
                stores,
                data,
                selectedType,
                search,
                page,
                total);

            SetPagingViewBag(
                page,
                total,
                search,
                selectedStoreId,
                selectedType);

            return View(vm);
        }

        // =====================================================
        // TRANSACTIONS
        // =====================================================

        public async Task<IActionResult> Transactions(
            int storeId = 0,
            int page = 1)
        {
            var accountId = GetAccountId();

            if (accountId <= 0)
                return Unauthorized();

            var actor = _actor.Get(User);
            var storeScope = await _storeScopeResolver.ResolveAsync(actor, storeId);
            if (!storeScope.IsResolved)
                return StoreScopeFailure(storeScope);
            storeId = storeScope.StoreId!.Value;
            var stores = await _service.GetStoresByStaffAsync(accountId);

            if (!stores.Any(x => x.StoreId == storeId))
                return Forbid();

            var (data, total) = await _service.GetAllTransactionsByStaffAsync(
                accountId,
                storeId,
                page,
                PageSize);

            SetTransactionViewBag(
                page,
                total,
                storeId,
                stores);

            return PartialView(
                "Partials/_TransactionPartial",
                ToTransactionViewModels(data));
        }

        // =====================================================
        // PRIVATE - VIEW MODEL
        // =====================================================

        private static InventoryIndexVM BuildIndexViewModel(
            int selectedStoreId,
            List<InventoryStoreDTO> stores,
            List<InventoryDTO> data,
            string selectedType,
            string? search,
            int page,
            int total)
        {
            return new InventoryIndexVM
            {
                StoreId = selectedStoreId,
                Stores = ToStoreTabs(stores),
                Items = ToInventoryItemViewModels(data),
                ActiveTab = selectedType,
                Search = search,
                Page = page < 1 ? 1 : page,
                TotalPages = CalculateTotalPages(total),
                TotalCount = total
            };
        }

        private static List<InventoryStoreTabVM> ToStoreTabs(
            IEnumerable<InventoryStoreDTO> stores)
        {
            return stores
                .Select(x => new InventoryStoreTabVM
                {
                    StoreId = x.StoreId,
                    StoreName = x.StoreName
                })
                .ToList();
        }

        private static List<InventoryItemVM> ToInventoryItemViewModels(
            IEnumerable<InventoryDTO> data)
        {
            return data
                .Select(x => new InventoryItemVM
                {
                    StoreInventoryId = x.StoreInventoryId,

                    StoreId = x.StoreId,
                    StoreName = x.StoreName,

                    IngredientName = x.IngredientName,
                    ItemCode = x.ItemCode,
                    ItemType = x.ItemType,
                    IdentityBadge = x.IdentityBadge,
                    LegacyRecipeId = x.LegacyRecipeId,
                    PreparedItemId = x.PreparedItemId,
                    QuantitySemanticsStatus = x.QuantitySemanticsStatus,

                    AvailableQty = x.AvailableQty,
                    ReservedQty = x.ReservedQty,
                    MaxNegativeQty = x.MaxNegativeQty,
                    LastUpdated = x.LastUpdated,

                    UnitCode = x.UnitCode,

                    LastUnitPrice = x.LastUnitPrice,
                    LastSupplierName = x.LastSupplierName,
                    LatestCostLayerId = x.LatestCostLayerId,
                    LatestCostLayerAt = x.LatestCostLayerAt,
                    SourceProductionRunId = x.SourceProductionRunId,
                    CostEvidenceStatus = x.CostEvidenceStatus
                })
                .ToList();
        }

        private static List<InventoryTransactionVM> ToTransactionViewModels(
            IEnumerable<InventoryTransactionDTO> data)
        {
            return data
                .Select(x => new InventoryTransactionVM
                {
                    InventoryTransactionId = x.InventoryTransactionId,
                    StoreInventoryId = x.StoreInventoryId,

                    StoreId = x.StoreId,
                    StoreName = x.StoreName,

                    IngredientName = x.IngredientName,
                    IdentityBadge = x.IdentityBadge,
                    QuantitySemanticsStatus = x.QuantitySemanticsStatus,
                    TypeName = x.TypeName,
                    StockStatusName = x.StockStatusName,

                    Quantity = x.Quantity,
                    BeforeQty = x.BeforeQty,
                    AfterQty = x.AfterQty,

                    UnitPrice = x.UnitPrice,
                    TotalAmount = x.TotalAmount,

                    InventoryDocumentId = x.InventoryDocumentId,
                    InventoryTransferId = x.InventoryTransferId,
                    ReferenceOrderId = x.ReferenceOrderId,
                    ReferenceType = x.ReferenceType,

                    CreatedAt = x.CreatedAt,
                    UnitCode = x.UnitCode
                })
                .ToList();
        }

        // =====================================================
        // PRIVATE - STORE ACCESS
        // =====================================================

        // =====================================================
        // PRIVATE - VIEWBAG
        // =====================================================

        private void SetPagingViewBag(
            int page,
            int total,
            string? search,
            int selectedStoreId,
            string inventoryType)
        {
            ViewBag.Page = page < 1 ? 1 : page;
            ViewBag.TotalPages = CalculateTotalPages(total);
            ViewBag.Search = search;
            ViewBag.StoreId = selectedStoreId;
            ViewBag.InventoryType = inventoryType;
        }

        private void SetTransactionViewBag(
            int page,
            int total,
            int selectedStoreId,
            List<InventoryStoreDTO> stores)
        {
            ViewBag.Page = page < 1 ? 1 : page;
            ViewBag.TotalPages = CalculateTotalPages(total);
            ViewBag.StoreId = selectedStoreId;
            ViewBag.StoreTabs = ToStoreTabs(stores);
        }

        private static int CalculateTotalPages(
            int total)
        {
            return (int)Math.Ceiling((double)total / PageSize);
        }

        private static string NormalizeInventoryType(string? inventoryType)
        {
            return string.Equals(
                inventoryType,
                InventoryCatalogTypes.PreparedItems,
                StringComparison.OrdinalIgnoreCase)
                ? InventoryCatalogTypes.PreparedItems
                : InventoryCatalogTypes.Ingredients;
        }

        // =====================================================
        // PRIVATE - AUTH
        // =====================================================

        private int GetAccountId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                     ?? User.FindFirst("AccountId")
                     ?? User.FindFirst("sub");

            if (claim == null)
                return 0;

            return int.TryParse(claim.Value, out var id)
                ? id
                : 0;
        }
    }
}
