using System.Reflection;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Controllers.Api.v1;
using CafeChain.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace CafeChain.Tests;

public sealed class LegacyOrderRemovalIssue213Tests
{
    [Fact]
    public async Task RetiredEntryPoint_Returns410WithoutCallingAction()
    {
        var httpContext = new DefaultHttpContext();
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

        await new LegacyEntryPointGoneAttribute().OnActionExecutionAsync(
            executingContext,
            () =>
            {
                called = true;
                return Task.FromResult(new ActionExecutedContext(
                    actionContext,
                    new List<IFilterMetadata>(),
                    new object()));
            });

        var result = Assert.IsType<ObjectResult>(executingContext.Result);
        Assert.Equal(StatusCodes.Status410Gone, result.StatusCode);
        Assert.False(called);
    }

    [Fact]
    public void RazorPos_IsRetiredAndReactPosApiRemainsCanonical()
    {
        Assert.NotNull(typeof(AdminPOSController)
            .GetCustomAttribute<LegacyEntryPointGoneAttribute>());
        Assert.Null(typeof(POSOrderController)
            .GetCustomAttribute<LegacyEntryPointGoneAttribute>());
        Assert.False(File.Exists(RepoPath(
            "CafeChain", "Areas", "Admin", "Views", "AdminPOS", "Index.cshtml")));
        Assert.False(File.Exists(RepoPath(
            "CafeChain", "Areas", "Admin", "Views", "Shared", "_POSLayout.cshtml")));
        Assert.False(File.Exists(RepoPath(
            "CafeChain", "wwwroot", "js", "pos-app.js")));
    }

    [Fact]
    public void DeliveryBoardShipperCodAndSimulation_AreRetired()
    {
        var retiredActions = new[]
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

        Assert.All(retiredActions, actionName =>
        {
            var action = typeof(AdminOrderController).GetMethod(actionName);
            Assert.NotNull(action);
            Assert.NotNull(action!
                .GetCustomAttribute<LegacyEntryPointGoneAttribute>());
        });
        Assert.False(File.Exists(RepoPath(
            "CafeChain", "Areas", "Admin", "Views", "AdminOrder", "Index.cshtml")));
    }

    [Fact]
    public void MockInventoryPosEndpoint_IsRemoved()
    {
        Assert.Null(Type.GetType(
            "CafeChain.Controllers.MockPOSController, CafeChain",
            throwOnError: false));
    }

    [Fact]
    public void SalesHistoryAndNavigation_RemainAvailableWithoutDeliveryMenu()
    {
        Assert.Null(typeof(AdminOrderController)
            .GetMethod(nameof(AdminOrderController.History))!
            .GetCustomAttribute<LegacyEntryPointGoneAttribute>());

        var layout = File.ReadAllText(RepoPath(
            "CafeChain", "Areas", "Admin", "Views", "Shared", "_AdminLayout.cshtml"));
        Assert.Contains("Lịch sử bán hàng", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("Bảng xử lý đơn Web/Giao hàng", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("Shipper", layout, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepoPath(params string[] parts) =>
        Path.Combine([FindRepoRoot(), .. parts]);

    private static string FindRepoRoot()
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
