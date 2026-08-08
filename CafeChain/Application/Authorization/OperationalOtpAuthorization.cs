using CafeChain.Application.Constants;

namespace CafeChain.Application.Authorization;

/// <summary>
/// Authoritative mapping between an operational OTP purpose and the permission
/// required from its approver. Unknown purposes are denied by default.
/// </summary>
public static class OperationalOtpAuthorization
{
    public static bool TryGetApproverPermission(string? actionType, out string permissionCode)
    {
        permissionCode = actionType switch
        {
            OtpConstants.ActionTypes.OpenShiftLate => PermissionConstants.PosWorkShiftApproveOutsideSchedule,
            OtpConstants.ActionTypes.OpenShiftOutsideSchedule => PermissionConstants.PosWorkShiftApproveOutsideSchedule,
            OtpConstants.ActionTypes.CashDifference => PermissionConstants.PosWorkShiftClose,
            OtpConstants.ActionTypes.CloseShiftException => PermissionConstants.PosWorkShiftCloseException,
            OtpConstants.ActionTypes.ReconcileWorkShift => PermissionConstants.PosWorkShiftReconcile,
            OtpConstants.ActionTypes.RegisterTerminal => PermissionConstants.PosWorkShiftOverrideTerminal,
            _ => string.Empty
        };

        return permissionCode.Length > 0;
    }
}
