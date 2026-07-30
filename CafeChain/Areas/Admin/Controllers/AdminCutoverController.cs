using CafeChain.Application.Constants;
using CafeChain.Application.Authorization;
using CafeChain.Application.DTOs.Inventories.Cutover;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Models.Enums.Inventory;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// Issue #124 — thin admin surface for cutover reconciliation and PreparedItem activation.
    /// Does not mutate inventory quantities directly.
    /// </summary>
    [RequirePermission(PermissionConstants.SystemCutoverView)]
    public sealed class AdminCutoverController : AdminBaseController
    {
        private static readonly string[] ReadRoles =
        {
            RoleConstants.SystemAdmin,
            RoleConstants.BusinessOwner,
            RoleConstants.AccountantWarehouse
        };

        private static readonly string[] ActivateRoles =
        {
            RoleConstants.SystemAdmin,
            RoleConstants.BusinessOwner
        };

        private readonly ICutoverReconciliationService _cutover;
        private readonly IInventorySchemaReadinessProbe _schemaProbe;

        public AdminCutoverController(
            ICutoverReconciliationService cutover,
            IInventorySchemaReadinessProbe schemaProbe)
        {
            _cutover = cutover;
            _schemaProbe = schemaProbe;
        }

        [HttpGet]
        public async Task<IActionResult> Reconcile(int storeId, CancellationToken cancellationToken)
        {
            if (!ReadRoles.Any(User.IsInRole))
                return Forbid();

            var result = await _cutover.ReconcileStoreAsync(storeId, cancellationToken);
            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message, code = result.ErrorCode });

            return Json(new { success = true, data = result.Data });
        }

        [HttpGet]
        public async Task<IActionResult> Schema(CancellationToken cancellationToken)
        {
            if (!ReadRoles.Any(User.IsInRole))
                return Forbid();

            var report = await _schemaProbe.ProbeAsync(cancellationToken);
            return Json(new { success = true, data = report });
        }

        [HttpGet]
        public async Task<IActionResult> Graduation(CancellationToken cancellationToken)
        {
            if (!ReadRoles.Any(User.IsInRole))
                return Forbid();

            var result = await _cutover.BuildGraduationSummaryAsync(cancellationToken);
            return Json(new { success = result.IsSuccess, data = result.Data, message = result.Message });
        }

        [HttpPost]
        [RequirePermission(PermissionConstants.SystemCutoverManage)]
        public async Task<IActionResult> Activate(
            [FromBody] CutoverActivationRequest request,
            CancellationToken cancellationToken)
        {
            if (!ActivateRoles.Any(User.IsInRole))
                return Forbid();

            request = new CutoverActivationRequest
            {
                StoreId = request.StoreId,
                RequestKey = request.RequestKey,
                TargetMode = InventoryWriterMode.PreparedItem,
                ExpectedMode = request.ExpectedMode,
                ExpectedRowVersion = request.ExpectedRowVersion,
                ExpectedReadinessHash = request.ExpectedReadinessHash,
                ExpectedReconciliationHash = request.ExpectedReconciliationHash,
                ExpectedSchemaContractHash = request.ExpectedSchemaContractHash,
                MaintenanceWindowAcknowledged = request.MaintenanceWindowAcknowledged,
                Reason = request.Reason,
                ActorAccountId = ResolveAccountId()
            };

            var result = await _cutover.ActivatePreparedItemAsync(request, cancellationToken);
            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message, code = result.ErrorCode, data = result.Data });

            return Json(new { success = true, data = result.Data });
        }

        [HttpPost]
        [RequirePermission(PermissionConstants.SystemCutoverManage)]
        public async Task<IActionResult> Block(
            int storeId,
            [FromBody] BlockRequest body,
            CancellationToken cancellationToken)
        {
            if (!ActivateRoles.Any(User.IsInRole))
                return Forbid();

            var status = await HttpContext.RequestServices
                .GetRequiredService<IInventoryWriterModeService>()
                .GetStatusAsync(storeId);
            if (!status.IsSuccess || status.Data == null)
                return BadRequest(new { success = false, message = status.Message, code = status.ErrorCode });

            var result = await _cutover.RollbackToBlockedAsync(
                storeId,
                status.Data.RowVersion,
                status.Data.WriterMode,
                body.Reason ?? "Rollback to Blocked",
                ResolveAccountId(),
                cancellationToken);

            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message, code = result.ErrorCode, data = result.Data });

            return Json(new { success = true, data = result.Data });
        }

        private int ResolveAccountId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("AccountId");
            return int.TryParse(value, out var accountId) ? accountId : 0;
        }

        public sealed class BlockRequest
        {
            public string? Reason { get; set; }
        }
    }
}
