using CafeChain.Application.Constants;
using CafeChain.Application.Authorization;
using CafeChain.Application.DTOs.Inventories.Consolidation;
using CafeChain.Application.Interfaces.Inventories;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// Issue #123 — thin admin surface for legacy BTP consolidation tooling.
    /// No direct inventory mutation; all writes go through ILegacyBtpConsolidationService.
    /// </summary>
    [RequirePermission(PermissionConstants.SystemLegacyConsolidationView)]
    public sealed class AdminLegacyBtpConsolidationController : AdminBaseController
    {
        private static readonly string[] AuditRoles =
        {
            RoleConstants.SystemAdmin,
            RoleConstants.BusinessOwner,
            RoleConstants.AccountantWarehouse,
            RoleConstants.AreaManager
        };

        private static readonly string[] ExecuteRoles =
        {
            RoleConstants.SystemAdmin,
            RoleConstants.BusinessOwner
        };

        private readonly ILegacyBtpConsolidationService _service;

        public AdminLegacyBtpConsolidationController(ILegacyBtpConsolidationService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Audit(int storeId, CancellationToken cancellationToken)
        {
            if (!AuditRoles.Any(User.IsInRole))
                return Forbid();

            var result = await _service.AuditStoreAsync(storeId, cancellationToken);
            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message, code = result.ErrorCode });

            return Json(new { success = true, data = result.Data });
        }

        [HttpPost]
        [RequirePermission(PermissionConstants.SystemLegacyConsolidationManage)]
        public async Task<IActionResult> CreateNoOpEvidence(
            [FromBody] ConsolidationNoOpRequest request,
            CancellationToken cancellationToken)
        {
            if (!AuditRoles.Any(User.IsInRole))
                return Forbid();

            var result = await _service.CreateNoOpEvidenceAsync(request, cancellationToken);
            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message, code = result.ErrorCode, data = result.Data });

            return Json(new { success = true, data = result.Data });
        }

        [HttpPost]
        [RequirePermission(PermissionConstants.SystemLegacyConsolidationManage)]
        public async Task<IActionResult> DryRun(
            [FromBody] ConsolidationDryRunRequest request,
            CancellationToken cancellationToken)
        {
            if (!AuditRoles.Any(User.IsInRole))
                return Forbid();

            var result = await _service.DryRunAsync(request, cancellationToken);
            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message, code = result.ErrorCode, data = result.Data });

            return Json(new { success = true, data = result.Data });
        }

        [HttpPost]
        [RequirePermission(PermissionConstants.SystemLegacyConsolidationManage)]
        public async Task<IActionResult> Execute(
            [FromBody] ConsolidationExecuteRequest request,
            CancellationToken cancellationToken)
        {
            if (!ExecuteRoles.Any(User.IsInRole))
                return Forbid();

            // Enforce role on server; ignore client-supplied role spoofing by overwriting.
            request = new ConsolidationExecuteRequest
            {
                StoreId = request.StoreId,
                RequestKey = request.RequestKey,
                ExpectedDryRunHash = request.ExpectedDryRunHash,
                ExecutedByStaffId = request.ExecutedByStaffId,
                ActorRole = ResolvePrimaryExecuteRole()
            };

            if (string.IsNullOrEmpty(request.ActorRole))
                return Forbid();

            var result = await _service.ExecuteAsync(request, cancellationToken);
            if (!result.IsSuccess)
                return BadRequest(new { success = false, message = result.Message, code = result.ErrorCode, data = result.Data });

            return Json(new { success = true, data = result.Data });
        }

        [HttpGet]
        public async Task<IActionResult> GetRun(int storeId, Guid requestKey, CancellationToken cancellationToken)
        {
            if (!AuditRoles.Any(User.IsInRole))
                return Forbid();

            var result = await _service.GetRunAsync(storeId, requestKey, cancellationToken);
            if (!result.IsSuccess)
                return NotFound(new { success = false, message = result.Message, code = result.ErrorCode });

            return Json(new { success = true, data = result.Data });
        }

        private string? ResolvePrimaryExecuteRole()
        {
            if (User.IsInRole(RoleConstants.SystemAdmin))
                return RoleConstants.SystemAdmin;
            if (User.IsInRole(RoleConstants.BusinessOwner))
                return RoleConstants.BusinessOwner;
            return null;
        }
    }
}
