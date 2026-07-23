using System.Reflection;
using CafeChain.Application.Constants;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Controllers;
using CafeChain.Controllers.Api.v1;
using CafeChain.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace CafeChain.Tests;

public sealed class LegacyOrderEntryPointIssue210Tests
{
    [Fact]
    public async Task Production_LegacyEntryPointReturnsNotFoundWithoutCallingAction()
    {
        var (context, next, wasCalled) = CreateFilterContext(Environments.Production);

        await new DevelopmentOnlyLegacyEntryPointAttribute()
            .OnActionExecutionAsync(context, next);

        Assert.IsType<NotFoundResult>(context.Result);
        Assert.False(wasCalled());
    }

    [Fact]
    public async Task Development_LegacyEntryPointContinuesToAuthorizedControllerAction()
    {
        var (context, next, wasCalled) = CreateFilterContext(Environments.Development);

        await new DevelopmentOnlyLegacyEntryPointAttribute()
            .OnActionExecutionAsync(context, next);

        Assert.Null(context.Result);
        Assert.True(wasCalled());
    }

    [Fact]
    public void RazorPosAndCustomerCheckout_AreServerBlockedOutsideDevelopment()
    {
        Assert.NotNull(typeof(AdminPOSController)
            .GetCustomAttribute<DevelopmentOnlyLegacyEntryPointAttribute>());
        Assert.NotNull(typeof(CheckoutController)
            .GetCustomAttribute<DevelopmentOnlyLegacyEntryPointAttribute>());

        var posAuthorization = typeof(AdminPOSController)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Single(attribute => attribute.Policy == AuthorizationPolicyConstants.PosApp);
        Assert.Equal(AuthorizationPolicyConstants.PosApp, posAuthorization.Policy);
    }

    [Fact]
    public void DeliveryBoardActions_AreBlockedButSalesHistoryRemainsAvailable()
    {
        var blockedActions = new[]
        {
            nameof(AdminOrderController.Index),
            nameof(AdminOrderController.GetOrders),
            nameof(AdminOrderController.GetOrderDetails),
            nameof(AdminOrderController.AcceptOrder),
            nameof(AdminOrderController.ReadyForPickup),
            nameof(AdminOrderController.GetShippers),
            nameof(AdminOrderController.Dispatched),
            nameof(AdminOrderController.CompleteOrder),
            nameof(AdminOrderController.FailDelivery),
            nameof(AdminOrderController.SimulateWebhook)
        };

        foreach (var actionName in blockedActions)
        {
            var action = typeof(AdminOrderController).GetMethod(actionName);
            Assert.NotNull(action);
            Assert.NotNull(action!.GetCustomAttribute<DevelopmentOnlyLegacyEntryPointAttribute>());
        }

        var historyActions = new[]
        {
            nameof(AdminOrderController.History),
            nameof(AdminOrderController.GetHistoryDetail),
            nameof(AdminOrderController.GetOrderHistoryData),
            nameof(AdminOrderController.ExportCSV)
        };
        foreach (var actionName in historyActions)
        {
            var action = typeof(AdminOrderController).GetMethod(actionName);
            Assert.NotNull(action);
            Assert.Null(action!.GetCustomAttribute<DevelopmentOnlyLegacyEntryPointAttribute>());
        }

        var adminAuthorization = typeof(AdminOrderController)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Single(attribute => attribute.Policy == AuthorizationPolicyConstants.AdminPanelAccess);
        Assert.Equal(AuthorizationPolicyConstants.AdminPanelAccess, adminAuthorization.Policy);
    }

    [Fact]
    public void CanonicalReactPosApi_RemainsEnabledAndJwtProtected()
    {
        Assert.Null(typeof(POSOrderController)
            .GetCustomAttribute<DevelopmentOnlyLegacyEntryPointAttribute>());
        Assert.Null(typeof(PosApiController)
            .GetCustomAttribute<DevelopmentOnlyLegacyEntryPointAttribute>());

        var route = typeof(POSOrderController)
            .GetCustomAttributes<RouteAttribute>(inherit: true)
            .Single(attribute => attribute.Template == "api/v1/pos/orders");
        Assert.Equal("api/v1/pos/orders", route.Template);

        var authorization = typeof(PosApiController)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Single();
        Assert.NotNull(authorization);
    }

    [Fact]
    public void AdminNavigation_HidesDeliveryBoardAndKeepsSalesHistory()
    {
        var layout = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "CafeChain",
            "Areas",
            "Admin",
            "Views",
            "Shared",
            "_AdminLayout.cshtml"));

        Assert.DoesNotContain("Bảng xử lý đơn Web/Giao hàng", layout, StringComparison.Ordinal);
        Assert.Contains("Lịch sử bán hàng", layout, StringComparison.Ordinal);
        Assert.Contains("asp-action=\"History\"", layout, StringComparison.Ordinal);
    }

    private static (
        ActionExecutingContext Context,
        ActionExecutionDelegate Next,
        Func<bool> WasCalled) CreateFilterContext(string environmentName)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(candidate => candidate.EnvironmentName).Returns(environmentName);

        var services = new ServiceCollection()
            .AddSingleton(environment.Object)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services
        };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor(),
            new ModelStateDictionary());
        var executingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());
        var called = false;
        ActionExecutionDelegate next = () =>
        {
            called = true;
            return Task.FromResult(new ActionExecutedContext(
                actionContext,
                new List<IFilterMetadata>(),
                new object()));
        };

        return (executingContext, next, () => called);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "CafeChain"))
                && Directory.Exists(Path.Combine(directory.FullName, "CafeChain.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Không tìm thấy repository root.");
    }
}
