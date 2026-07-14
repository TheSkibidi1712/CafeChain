using System.Threading.Tasks;
using CafeChain.Application.Constants;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Controllers
{
    [Route("api/[controller]")]
    public class PosController : Controller
    {
        /// <summary>
        /// Soft-removal: voucher validation disabled for active product scope.
        /// Does not silently accept codes.
        /// </summary>
        [HttpPost("validate-voucher")]
        public Task<IActionResult> ValidateVoucher([FromBody] VoucherValidationRequest? request)
        {
            return Task.FromResult<IActionResult>(Json(new
            {
                success = false,
                errorCode = ProductScopeErrorCodes.FeatureNotAvailable,
                message = ProductScopeErrorCodes.VoucherNotAvailableMessage
            }));
        }

        public class VoucherValidationRequest
        {
            public string? Code { get; set; }
            public int CustomerId { get; set; }
            public decimal SubTotal { get; set; }
        }
    }
}
