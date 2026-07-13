using CafeChain.Application.Constants;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers
{
    /// <summary>
    /// Soft-removal: voucher and member-level loyalty management disabled.
    /// Historical data remains in schema; no active mutation routes.
    /// </summary>
    [Area("Admin")]
    public class AdminVoucherController : Controller
    {
        public IActionResult Index()
            => NotFound(new
            {
                errorCode = ProductScopeErrorCodes.FeatureNotAvailable,
                message = ProductScopeErrorCodes.VoucherNotAvailableMessage
            });

        [HttpPost]
        public IActionResult UpdateMemberLevel()
            => FeatureDisabled();

        [HttpGet]
        public IActionResult Create()
            => FeatureDisabled();

        [HttpPost]
        public IActionResult Create(object? _)
            => FeatureDisabled();

        [HttpGet]
        public IActionResult Edit(int id)
            => FeatureDisabled();

        [HttpPost]
        public IActionResult Edit(object? _)
            => FeatureDisabled();

        [HttpPost]
        public IActionResult ToggleStatus(int id)
            => FeatureDisabledJson();

        [HttpGet]
        public IActionResult GetVoucherJson(int id)
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
