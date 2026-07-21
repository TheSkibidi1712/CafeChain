namespace CafeChain.Application.Options;

public sealed class PosLauncherOptions
{
    public const string SectionName = "AppLauncher:Pos";

    public string FrontendDirectory { get; set; } = "../CafeChain.Frontend";
    public string PosUrl { get; set; } = "http://127.0.0.1:5173/order";
    public string HealthCheckUrl { get; set; } = "http://127.0.0.1:5173/";
    public int FrontendPort { get; set; } = 5173;
    public int StartupTimeoutSeconds { get; set; } = 45;
    public int PollIntervalMilliseconds { get; set; } = 500;
}
