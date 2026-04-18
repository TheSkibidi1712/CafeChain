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

        // ================= TỒN KHO =================
        public async Task<IActionResult> Index(string? search, int page = 1)
        {
            var accountId = GetAccountId();

            if (accountId <= 0)
                return Unauthorized();

            int pageSize = 10;

            var (data, total) = await _service
                .GetInventoryByStaffAsync(accountId, search, page, pageSize);

            var vm = new InventoryIndexVM
            {
                Items = data.Select(x => new InventoryItemVM
                {
                    StoreInventoryId = x.StoreInventoryId,
                    IngredientName = x.IngredientName,
                    AvailableQty = x.AvailableQty,
                    ReservedQty = x.ReservedQty,
                    LastUpdated = x.LastUpdated,
                    UnitCode = x.UnitCode,

                    // 🔥 NEW
                    LastUnitPrice = x.LastUnitPrice,
                    LastSupplierName = x.LastSupplierName
                }).ToList()
            };

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);
            ViewBag.Search = search;

            return View(vm);
        }

        // ================= LỊCH SỬ GIAO DỊCH =================
        public async Task<IActionResult> Transactions(int page = 1)
        {
            var accountId = GetAccountId();

            int pageSize = 10;

            var (data, total) = await _service
                .GetAllTransactionsByStaffAsync(accountId, page, pageSize);

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)total / pageSize);

            var vm = data.Select(x => new InventoryTransactionVM
            {
                IngredientName = x.IngredientName,
                TypeName = x.TypeName,
                Quantity = x.Quantity,
                BeforeQty = x.BeforeQty,
                AfterQty = x.AfterQty,
                CreatedAt = x.CreatedAt,
                UnitCode = x.UnitCode,
                UnitPrice = x.UnitPrice,
                TotalAmount = x.TotalAmount
            }).ToList();

            return PartialView("_TransactionPartial", vm);
        }
        // ================= PRIVATE =================

        private int GetAccountId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                     ?? User.FindFirst("AccountId")
                     ?? User.FindFirst("sub");

            if (claim == null)
                return 0;

            return int.TryParse(claim.Value, out var id) ? id : 0;
        }
    }
}

