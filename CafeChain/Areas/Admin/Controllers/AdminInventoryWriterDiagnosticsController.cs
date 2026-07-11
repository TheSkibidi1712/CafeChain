using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Admin.StoreInventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>Read-only diagnostics for #118. It cannot change mode or identity metadata.</summary>
    public sealed class AdminInventoryWriterDiagnosticsController : AdminBaseController
    {
        private static readonly string[] ReadRoles =
        {
            RoleConstants.SystemAdmin,
            RoleConstants.BusinessOwner,
            RoleConstants.AccountantWarehouse,
            RoleConstants.AreaManager,
            RoleConstants.StoreManager
        };

        private readonly IInventoryWriterModeService _modeService;
        private readonly IAdminStoreInventoryService _inventoryService;
        private readonly AppDbContext _context;

        public AdminInventoryWriterDiagnosticsController(
            IInventoryWriterModeService modeService,
            IAdminStoreInventoryService inventoryService,
            AppDbContext context)
        {
            _modeService = modeService;
            _inventoryService = inventoryService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Status(int storeId)
        {
            if (!ReadRoles.Any(User.IsInRole))
                return Forbid();

            var accountId = ResolveAccountId();
            var stores = await _inventoryService.GetStoresByStaffAsync(accountId);
            if (!stores.Any(x => x.StoreId == storeId))
                return Forbid();

            var status = await _modeService.GetStatusAsync(storeId);
            if (!status.IsSuccess || status.Data == null)
                return BadRequest(new { success = false, message = status.Message, code = status.ErrorCode });

            var readiness = await _modeService.EvaluateReadinessAsync(storeId);
            var rows = await _context.StoreInventories
                .AsNoTracking()
                .Where(x => x.StoreId == storeId && x.IngredientId == null)
                .OrderBy(x => x.StoreInventoryId)
                .Select(x => new
                {
                    x.StoreInventoryId,
                    x.RecipeId,
                    x.PreparedItemId,
                    BtpIdentityState = x.BtpIdentityState.HasValue ? x.BtpIdentityState.Value.ToString() : null,
                    QuantitySemanticsStatus = x.QuantitySemanticsStatus.HasValue
                        ? x.QuantitySemanticsStatus.Value.ToString()
                        : null,
                    x.SupersededByStoreInventoryId,
                    QuantityWarning = x.QuantitySemanticsStatus == Models.Enums.Inventory.InventoryQuantitySemanticsStatus.Unknown
                        ? "Chưa xác nhận đơn vị tồn"
                        : null
                })
                .ToListAsync();

            return Json(new
            {
                success = true,
                writerMode = status.Data.WriterMode.ToString(),
                status.Data.HasEverActivatedPreparedItem,
                readiness.Ready,
                readiness.ReadinessHash,
                readiness.Blockers,
                rows
            });
        }

        private int ResolveAccountId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("AccountId");
            return int.TryParse(value, out var accountId) ? accountId : 0;
        }
    }
}
