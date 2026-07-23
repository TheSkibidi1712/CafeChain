using CafeChain.Application.Constants;
using CafeChain.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafeChain.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicyConstants.PosApp)]
[LegacyEntryPointGone]
public sealed class AdminPOSController : Controller
{
    public IActionResult Index() => StatusCode(StatusCodes.Status410Gone);
}
