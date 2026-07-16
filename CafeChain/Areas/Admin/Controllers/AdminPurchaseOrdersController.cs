using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Areas.Admin.Controllers
{
    public sealed class AdminPurchaseOrdersController : AdminBaseController
    {
        private readonly IPurchaseOrderService _service;
        private readonly IRestockAllocationService _allocations;
        private readonly IPhysicalUnitConversionService _conversion;
        private readonly IAdminActorContextAccessor _actor;
        private readonly AppDbContext _context;

        public AdminPurchaseOrdersController(
            IPurchaseOrderService service,
            IRestockAllocationService allocations,
            IPhysicalUnitConversionService conversion,
            IAdminActorContextAccessor actor,
            AppDbContext context)
        {
            _service = service;
            _allocations = allocations;
            _conversion = conversion;
            _actor = actor;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? storeId = null, string? status = null)
        {
            if (!CanManage()) return Forbid();
            return View(await _service.ListAsync(storeId, status));
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            if (!CanManage()) return Forbid();
            var result = await _service.GetDetailAsync(id);
            return result.IsSuccess && result.Data != null ? View(result.Data) : NotFound();
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? restockRequestId = null)
        {
            if (!CanManage()) return Forbid();
            var model = new CreatePurchaseOrderRequest { Lines = new() { new() } };
            if (restockRequestId.HasValue)
            {
                var request = await _context.RestockRequests.AsNoTracking()
                    .Include(x => x.Ingredient)
                    .SingleOrDefaultAsync(x => x.RestockRequestId == restockRequestId.Value);
                if (request?.IngredientId == null) return BadRequest("Chỉ tạo PO cho RestockRequest nguyên liệu.");
                var allocation = await _allocations.GetSummaryAsync(request.RestockRequestId);
                var remaining = allocation?.RemainingUnallocatedQuantity ?? request.RequestedQuantity;
                if (remaining <= 0)
                {
                    TempData["ErrorMessage"] = "Yêu cầu nhập không còn số lượng chưa phân bổ để tạo PO.";
                    return RedirectToAction("Details", "AdminRestockRequests", new { id = request.RestockRequestId });
                }
                model.StoreId = request.StoreId;
                model.Lines[0].RestockRequestId = request.RestockRequestId;
                model.Lines[0].IngredientId = request.IngredientId.Value;
                var offer = await _context.IngredientSuppliers.AsNoTracking()
                    .Where(x => x.IngredientId == request.IngredientId && x.Active)
                    .OrderByDescending(x => x.IsPrimary).ThenBy(x => x.IngredientSupplierId)
                    .FirstOrDefaultAsync();
                if (offer != null)
                {
                    var packageBaseQuantity = await _conversion.ConvertAsync(
                        offer.PackageQuantity.GetValueOrDefault(),
                        offer.UnitId,
                        request.Ingredient!.BaseUnitId);
                    model.SupplierId = offer.SupplierId;
                    model.Lines[0].IngredientSupplierId = offer.IngredientSupplierId;
                    model.Lines[0].PackageCount = Math.Max(
                        offer.MinimumOrderPackageCount.GetValueOrDefault(1),
                        packageBaseQuantity.IsSuccess && packageBaseQuantity.Data > 0
                            ? Math.Ceiling(remaining / packageBaseQuantity.Data)
                            : 1);
                }
            }
            await PopulateAsync(model.StoreId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreatePurchaseOrderRequest model)
        {
            if (!CanManage()) return Forbid();
            var actor = _actor.Get(User);
            var result = await _service.CreateDraftAsync(model, actor.StaffId, actor.RoleNames);
            if (!result.IsSuccess || result.Data == null)
            {
                TempData["ErrorMessage"] = result.Message;
                await PopulateAsync(model.StoreId);
                return View(model);
            }
            return RedirectToAction(nameof(Details), new { id = result.Data.PurchaseOrderId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> Approve(int id) => Transition(id, true);

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> MarkSent(int id) => Transition(id, false);

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string reason)
        {
            if (!CanManage()) return Forbid();
            var actor = _actor.Get(User);
            var result = await _service.CancelAsync(id, actor.StaffId, actor.RoleNames, reason);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task<IActionResult> Transition(int id, bool approve)
        {
            if (!CanManage()) return Forbid();
            var actor = _actor.Get(User);
            var result = approve
                ? await _service.ApproveAsync(id, actor.StaffId, actor.RoleNames)
                : await _service.MarkSentAsync(id, actor.StaffId, actor.RoleNames);
            TempData[result.IsSuccess ? "SuccessMessage" : "ErrorMessage"] = result.Message;
            return RedirectToAction(nameof(Details), new { id });
        }

        private async Task PopulateAsync(int storeId)
        {
            ViewBag.Stores = await _context.Stores.AsNoTracking().Where(x => x.Active).OrderBy(x => x.Name).ToListAsync();
            ViewBag.Suppliers = await _context.Suppliers.AsNoTracking().Where(x => x.Active).OrderBy(x => x.Name).ToListAsync();
            ViewBag.Offers = await _context.IngredientSuppliers.AsNoTracking()
                .Include(x => x.Ingredient).Include(x => x.Supplier).Include(x => x.Unit)
                .Where(x => x.Active && (storeId <= 0 || x.Supplier.SupplierStores.Any(s => s.StoreId == storeId && s.Active)))
                .OrderBy(x => x.Ingredient.Name).ToListAsync();
        }

        private bool CanManage() =>
            User.IsInRole(RoleConstants.AccountantWarehouse)
            || User.IsInRole(RoleConstants.BusinessOwner)
            || User.IsInRole(RoleConstants.AreaManager);
    }
}
