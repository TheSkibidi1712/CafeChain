using System.Net;
using CafeChain.Application.DTOs.AppLauncher;
using CafeChain.Application.Options;
using CafeChain.Application.Services.AppLauncher;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CafeChain.Tests;

public sealed class PosLaunchCoordinatorTests
{
    [Fact]
    public async Task EnsureReady_frontend_ready_does_not_require_print_bridge_or_configured_store()
    {
        using var coordinator = CreateCoordinator(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html><body><div id=\"root\"></div></body></html>")
            });

        var result = await coordinator.EnsureReadyAsync(storeId: 999);

        Assert.True(result.IsReady);
        Assert.Equal(PosLaunchState.Ready, result.State);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task GetStatus_frontend_ready_is_independent_of_print_bridge_presence()
    {
        using var coordinator = CreateCoordinator(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<script type=\"module\" src=\"/@vite/client\"></script>")
            });

        var result = await coordinator.GetStatusAsync(storeId: 2);

        Assert.True(result.IsReady);
        Assert.Equal(PosLaunchState.Ready, result.State);
    }

    [Fact]
    public async Task EnsureReady_frontend_missing_reports_only_frontend_failure()
    {
        var missingDirectory = $"missing-pos-frontend-{Guid.NewGuid():N}";
        using var coordinator = CreateCoordinator(
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new PosLauncherOptions { FrontendDirectory = missingDirectory });

        var result = await coordinator.EnsureReadyAsync(storeId: 1);

        Assert.False(result.IsReady);
        Assert.Equal(PosLaunchState.Failed, result.State);
        Assert.Equal(PosLaunchErrorCodes.FrontendProjectMissing, result.ErrorCode);
        Assert.DoesNotContain("PrintBridge", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void State_values_used_by_existing_clients_remain_stable()
    {
        Assert.Equal(3, (int)PosLaunchState.CheckingFrontend);
        Assert.Equal(4, (int)PosLaunchState.StartingFrontend);
        Assert.Equal(5, (int)PosLaunchState.Ready);
        Assert.Equal(6, (int)PosLaunchState.Failed);
    }

    private static PosLaunchCoordinator CreateCoordinator(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory,
        PosLauncherOptions? options = null)
    {
        var client = new HttpClient(new StubHttpMessageHandler(responseFactory));
        var clientFactory = new Mock<IHttpClientFactory>();
        clientFactory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(client);

        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(x => x.ContentRootPath).Returns(Path.GetTempPath());

        return new PosLaunchCoordinator(
            clientFactory.Object,
            environment.Object,
            Options.Create(options ?? new PosLauncherOptions()),
            NullLogger<PosLaunchCoordinator>.Instance);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(_responseFactory(request));
    }
}
