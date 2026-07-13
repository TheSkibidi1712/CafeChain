using CafeChain.Application.Constants;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Controllers
{
    /// <summary>
    /// Soft-removal: lucky wheel awards vouchers and spends loyalty points — disabled.
    /// </summary>
    public class LuckyWheelController : Controller
    {
        [HttpGet]
        public IActionResult GetUserInfo()
            => Json(new
            {
                isAuthenticated = User.Identity?.IsAuthenticated ?? false,
                success = false,
                errorCode = ProductScopeErrorCodes.FeatureNotAvailable,
                message = ProductScopeErrorCodes.VoucherOrLoyaltyNotAvailableMessage,
                canSpinToday = false,
                points = 0,
                spinCost = 0
            });

        [HttpPost]
        public IActionResult Spin()
            => Json(new
            {
                success = false,
                errorCode = ProductScopeErrorCodes.FeatureNotAvailable,
                message = ProductScopeErrorCodes.VoucherOrLoyaltyNotAvailableMessage
            });
    }
}
