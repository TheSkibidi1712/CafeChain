using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.POS;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CafeChain.Controllers.Api.v1
{
    /// <summary>
    /// Issue #96 — POS “Kho chi nhánh” read-only inventory.
    /// GET /api/v1/pos/branch-inventory
    /// </summary>
    [Route("api/v1/pos")]
    public class POSBranchInventoryController : PosApiController
    {
        private static readonly HashSet<string> AllowedRoles = new(StringComparer.Ordinal)
        {
            RoleConstants.SalesStaff,
            RoleConstants.ShiftSupervisor,
            RoleConstants.StoreManager,
            RoleConstants.AccountantWarehouse
        };

        private readonly IPosBranchInventoryService _service;

        public POSBranchInventoryController(IPosBranchInventoryService service)
        {
            _service = service;
        }

        /// <summary>
        /// GET /api/v1/pos/branch-inventory?search=&amp;itemType=&amp;page=1&amp;pageSize=50
        /// Scoped strictly to JWT CurrentStoreId. Read-only.
        /// </summary>
        [HttpGet("branch-inventory")]
        public async Task<IActionResult> GetBranchInventory(
            [FromQuery] string? search = null,
            [FromQuery] string? itemType = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50)
        {
            if (!IsAllowedRole())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    message = "Tài khoản không có quyền xem kho chi nhánh."
                });
            }

            // Never trust client storeId — always JWT claim.
            var storeId = CurrentStoreId;

            var result = await _service.GetBranchInventoryAsync(
                storeId,
                search,
                itemType,
                page,
                pageSize);

            if (!result.IsSuccess)
            {
                return BadRequest(new
                {
                    success = false,
                    message = result.Message
                });
            }

            return Ok(new
            {
                success = true,
                data = result.Data
            });
        }

        private bool IsAllowedRole()
        {
            var roles = User.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .Concat(User.FindAll("role").Select(c => c.Value));

            return roles.Any(r => AllowedRoles.Contains(r));
        }
    }
}
