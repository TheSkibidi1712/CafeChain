using CafeChain.Application.DTOs.Admin.StoreInventories;
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

        public AdminStoreInventoryController(IAdminStoreInventoryService service)
        {
            _service = service;
        }

        // =====================================================
        // INDEX
        // =====================================================

        public async Task<IActionResult> Index(
            string? search,
            int storeId = 0,
            int page = 1)
        {
            var accountId = GetAccountId();

            if (accountId <= 0)
                return Unauthorized();

            var stores = await _service.GetStoresByStaffAsync(accountId);
            var selectedStoreId = ResolveSelectedStoreId(storeId, stores);

            var (data, total) = await _service.GetInventoryByStaffAsync(
                accountId,
                selectedStoreId,
                search,
                page,
                PageSize);

            var vm = BuildIndexViewModel(
                selectedStoreId,
                stores,
                data);

            SetPagingViewBag(
                page,
                total,
                search,
                selectedStoreId);

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

            var stores = await _service.GetStoresByStaffAsync(accountId);

            if (!CanAccessStore(storeId, stores))
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
            List<InventoryDTO> data)
        {
            return new InventoryIndexVM
            {
                StoreId = selectedStoreId,
                Stores = ToStoreTabs(stores),
                Items = ToInventoryItemViewModels(data)
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
                    LastSupplierName = x.LastSupplierName
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

        private static int ResolveSelectedStoreId(
            int requestedStoreId,
            List<InventoryStoreDTO> stores)
        {
            if (!stores.Any())
                return 0;

            if (requestedStoreId > 0 &&
                stores.Any(x => x.StoreId == requestedStoreId))
            {
                return requestedStoreId;
            }

            return stores.First().StoreId;
        }

        private static bool CanAccessStore(
            int requestedStoreId,
            List<InventoryStoreDTO> stores)
        {
            return requestedStoreId <= 0 ||
                   stores.Any(x => x.StoreId == requestedStoreId);
        }

        // =====================================================
        // PRIVATE - VIEWBAG
        // =====================================================

        private void SetPagingViewBag(
            int page,
            int total,
            string? search,
            int selectedStoreId)
        {
            ViewBag.Page = page < 1 ? 1 : page;
            ViewBag.TotalPages = CalculateTotalPages(total);
            ViewBag.Search = search;
            ViewBag.StoreId = selectedStoreId;
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
