using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CafeChain.Application.Authorization;

/// <summary>Requires a server-validated POS session bound to an OPEN WorkShift.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class RequireActivePosShiftAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var accessMode = context.HttpContext.Items["PosAccessMode"] as string;
        if (string.Equals(accessMode, PosAccessModes.Active, StringComparison.Ordinal)) return;

        context.Result = new ObjectResult(new
        {
            success = false,
            errorCode = WorkShiftErrorCodes.ShiftNotOpened,
            recommendedAction = accessMode == PosAccessModes.PendingClose
                ? WorkShiftRecommendedActions.CompleteClosing
                : WorkShiftRecommendedActions.EnterOpeningCash,
            message = accessMode == PosAccessModes.PendingClose
                ? "Ca đang chờ hoàn tất đóng; không thể thực hiện nghiệp vụ POS này."
                : "Bạn chưa mở ca làm việc."
        }) { StatusCode = StatusCodes.Status403Forbidden };
    }
}
