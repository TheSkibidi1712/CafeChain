using System.Diagnostics;
using System.ComponentModel;
using System.Net.Sockets;
using CafeChain.Application.DTOs.AppLauncher;
using CafeChain.Application.Interfaces.AppLauncher;
using CafeChain.Application.Options;
using Microsoft.Extensions.Options;

namespace CafeChain.Application.Services.AppLauncher;

public sealed class PosLaunchCoordinator : IPosLaunchCoordinator, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _environment;
    private readonly PosLauncherOptions _options;
    private readonly ILogger<PosLaunchCoordinator> _logger;
    private PosLaunchResultDTO _status = Status(PosLaunchState.Idle, "POS chưa được khởi chạy.");
    private Process? _frontendProcess;

    public PosLaunchCoordinator(
        IHttpClientFactory httpClientFactory,
        IWebHostEnvironment environment,
        IOptions<PosLauncherOptions> options,
        ILogger<PosLaunchCoordinator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _environment = environment;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PosLaunchResultDTO> GetStatusAsync(int storeId, CancellationToken cancellationToken = default)
    {
        if (await IsFrontendReadyAsync(cancellationToken))
            return SetStatus(PosLaunchState.Ready, "POS đã sẵn sàng.");

        return _status;
    }

    public async Task<PosLaunchResultDTO> EnsureReadyAsync(int storeId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var timeout = TimeSpan.FromSeconds(Math.Clamp(_options.StartupTimeoutSeconds, 5, 180));

            SetStatus(PosLaunchState.CheckingFrontend, "Đang kiểm tra CafeChain.Frontend...");
            if (!await IsFrontendReadyAsync(cancellationToken))
            {
                var frontendDirectory = ResolvePath(_options.FrontendDirectory);
                if (!Directory.Exists(frontendDirectory) || !File.Exists(Path.Combine(frontendDirectory, "package.json")))
                    return SetFailure(PosLaunchErrorCodes.FrontendProjectMissing, "Không tìm thấy CafeChain.Frontend hoặc package.json trong cấu hình.");

                if (await IsPortOpenAsync(_options.FrontendPort, cancellationToken))
                    return SetFailure(PosLaunchErrorCodes.FrontendPortInUse, $"Port {_options.FrontendPort} đang bị ứng dụng khác sử dụng.");

                SetStatus(PosLaunchState.StartingFrontend, "Đang khởi chạy CafeChain.Frontend...");
                try
                {
                    if (_frontendProcess is null || _frontendProcess.HasExited)
                        _frontendProcess = StartFrontend(frontendDirectory);
                }
                catch (Win32Exception ex)
                {
                    _logger.LogError(ex, "Không tìm thấy npm khi khởi chạy Frontend");
                    return SetFailure(PosLaunchErrorCodes.NpmMissing, "Không tìm thấy Node.js/npm để khởi chạy CafeChain.Frontend.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Không thể khởi chạy frontend từ {Directory}", frontendDirectory);
                    return SetFailure(PosLaunchErrorCodes.FrontendStartFailed, "Không thể khởi chạy CafeChain.Frontend. Kiểm tra Node.js/npm và log máy chủ.");
                }

                if (!await WaitUntilAsync(IsFrontendReadyAsync, timeout, cancellationToken))
                    return SetFailure(PosLaunchErrorCodes.FrontendTimeout, "CafeChain.Frontend chưa sẵn sàng trong thời gian cho phép.");
            }

            return SetStatus(PosLaunchState.Ready, "POS đã sẵn sàng.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không mong đợi khi khởi chạy POS");
            return SetFailure(PosLaunchErrorCodes.InvalidConfiguration, "Không thể khởi chạy POS do cấu hình hoặc môi trường không hợp lệ.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private Process StartFrontend(string workingDirectory)
    {
        var executable = OperatingSystem.IsWindows() ? "npm.cmd" : "npm";
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add("dev");
        start.ArgumentList.Add("--");
        start.ArgumentList.Add("--host");
        start.ArgumentList.Add("127.0.0.1");
        start.ArgumentList.Add("--port");
        start.ArgumentList.Add(_options.FrontendPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add("--strictPort");
        return Process.Start(start) ?? throw new InvalidOperationException("npm không tạo process Frontend.");
    }

    private async Task<bool> IsFrontendReadyAsync(CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(_options.HealthCheckUrl, UriKind.Absolute, out var uri))
            return false;

        try
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(2);
            using var response = await client.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return false;
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return body.Contains("id=\"root\"", StringComparison.OrdinalIgnoreCase)
                || body.Contains("/@vite/client", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return false;
        }
    }

    private static async Task<bool> IsPortOpenAsync(int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", port, cancellationToken);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private async Task<bool> WaitUntilAsync(
        Func<CancellationToken, Task<bool>> predicate,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await predicate(cancellationToken))
                return true;
            await Task.Delay(Math.Clamp(_options.PollIntervalMilliseconds, 100, 5000), cancellationToken);
        }
        return false;
    }

    private string ResolvePath(string configuredPath) => Path.GetFullPath(configuredPath, _environment.ContentRootPath);
    private PosLaunchResultDTO SetStatus(PosLaunchState state, string message) => _status = Status(state, message);
    private PosLaunchResultDTO SetFailure(string code, string message) => _status = Failure(code, message);
    private static PosLaunchResultDTO Status(PosLaunchState state, string message) => new() { State = state, Message = message };
    private static PosLaunchResultDTO Failure(string code, string message) => new() { State = PosLaunchState.Failed, ErrorCode = code, Message = message };

    public void Dispose()
    {
        _gate.Dispose();
        _frontendProcess?.Dispose();
    }
}
