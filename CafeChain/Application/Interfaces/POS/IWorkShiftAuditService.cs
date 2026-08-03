namespace CafeChain.Application.Interfaces.POS;

public interface IWorkShiftAuditService
{
    Task WriteAsync(
        string action,
        int workShiftId,
        int actorStaffId,
        object? oldData,
        object? newData,
        CancellationToken cancellationToken = default);

    Task WriteOtpAsync(
        string action,
        int otpChallengeId,
        int actorStaffId,
        object? data,
        CancellationToken cancellationToken = default);
}
