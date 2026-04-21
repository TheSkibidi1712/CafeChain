using CafeChain.Application.Interfaces.Admin.StoreInventories;
using CafeChain.ViewModels.Admin.StoreInventories;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CafeChain.Areas.Admin.Controllers
{
    public class AdminStoreInventoryController : AdminBaseController
    {
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

            int pageSize = 10;

            var stores = await _service.GetStoresByStaffAsync(accountId);

            if (storeId == 0 && stores.Any())
            {
                storeId = stores.First().StoreId;
            }

            var (data, total) = await _service.GetInventoryByStaffAsync(
                accountId,
                storeId,
                search,
                page,
                pageSize);

            var vm = new InventoryIndexVM
            {
                StoreId = storeId,

                Stores = stores
                    .Select(x => new InventoryStoreTabVM
                    {
                        StoreId = x.StoreId,
                        StoreName = x.StoreName
                    })
                    .ToList(),

                Items = data
                    .Select(x => new InventoryItemVM
                    {
                        StoreInventoryId = x.StoreInventoryId,

                        StoreId = x.StoreId,
                        StoreName = x.StoreName,

                        IngredientName = x.IngredientName,

                        AvailableQty = x.AvailableQty,
                        ReservedQty = x.ReservedQty,
                        LastUpdated = x.LastUpdated,

                        UnitCode = x.UnitCode,

                        LastUnitPrice = x.LastUnitPrice,
                        LastSupplierName = x.LastSupplierName
                    })
                    .ToList()
            };

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.Search = search;

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

            int pageSize = 10;

            var (data, total) = await _service.GetAllTransactionsByStaffAsync(
                accountId,
                storeId,
                page,
                pageSize);

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.StoreId = storeId;

            var vm = data
                .Select(x => new InventoryTransactionVM
                {
                    StoreId = x.StoreId,
                    StoreName = x.StoreName,

                    IngredientName = x.IngredientName,
                    TypeName = x.TypeName,

                    Quantity = x.Quantity,
                    BeforeQty = x.BeforeQty,
                    AfterQty = x.AfterQty,

                    CreatedAt = x.CreatedAt,
                    UnitCode = x.UnitCode,

                    UnitPrice = x.UnitPrice,
                    TotalAmount = x.TotalAmount
                })
                .ToList();

            return PartialView("_TransactionPartial", vm);
        }

        // =====================================================
        // PRIVATE
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

