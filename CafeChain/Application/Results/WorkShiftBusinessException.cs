namespace CafeChain.Application.Results;

public sealed class WorkShiftBusinessException : Exception
{
    public WorkShiftBusinessException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
