namespace CafeChain.Application.DTOs.AppLauncher;

public enum PosLaunchState
{
    Idle = 0,
    CheckingFrontend = 3,
    StartingFrontend = 4,
    Ready = 5,
    Failed = 6
}

public sealed class PosLaunchResultDTO
{
    public PosLaunchState State { get; init; }
    public bool IsReady => State == PosLaunchState.Ready;
    public string Message { get; init; } = string.Empty;
    public string? ErrorCode { get; init; }
}

public static class PosLaunchErrorCodes
{
    public const string NpmMissing = "POS_NPM_MISSING";
    public const string FrontendProjectMissing = "POS_FRONTEND_PROJECT_MISSING";
    public const string FrontendPortInUse = "POS_FRONTEND_PORT_IN_USE";
    public const string FrontendStartFailed = "POS_FRONTEND_START_FAILED";
    public const string FrontendTimeout = "POS_FRONTEND_TIMEOUT";
    public const string InvalidConfiguration = "POS_INVALID_CONFIGURATION";
}
