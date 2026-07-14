using CafeChain.Application.Constants;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// Soft-removal: lucky-wheel voucher config disabled (voucher out of product scope).
    /// </summary>
    [Area("Admin")]
    public class AdminWheelController : Controller
    {
        public IActionResult Index()
            => NotFound(new
            {
                errorCode = ProductScopeErrorCodes.FeatureNotAvailable,
                message = ProductScopeErrorCodes.VoucherNotAvailableMessage
            });

        [HttpPost]
        public IActionResult Create()
            => FeatureDisabled();

        [HttpPost]
        public IActionResult ToggleStatus(int id)
            => FeatureDisabledJson();

        [HttpPost]
        public IActionResult SavePrizes()
            => FeatureDisabledJson();

        private IActionResult FeatureDisabled()
            => StatusCode(410, new
            {
                errorCode = ProductScopeErrorCodes.FeatureNotAvailable,
                message = ProductScopeErrorCodes.VoucherNotAvailableMessage
            });

        private IActionResult FeatureDisabledJson()
            => Json(new
            {
                success = false,
                errorCode = ProductScopeErrorCodes.FeatureNotAvailable,
                message = ProductScopeErrorCodes.VoucherNotAvailableMessage
            });
    }
}
