using System.Reflection;
using System.Security.Claims;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Dashboard;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Dashboard;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Services.Admin.Dashboard;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Infrastrusture.Interfaces.Admin.Dashboard;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CafeChain.Tests;

public sealed class DashboardAnalyticsServiceTests
{
    private static readonly AdminActorContext Actor = new() { StaffId = 7, RoleNames = ["Admin"] };

    [Fact]
    public async Task GetSectionAsync_RejectsStoreOutsideActorScope()
    {
        var (repository, scope) = CreateDependencies();
        var service = new DashboardService(repository.Object, scope.Object);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetSectionAsync(
            Actor, DashboardSection.Executive, CreateFilter(storeId: 999)));

        repository.Verify(x => x.GetExecutiveAsync(
            It.IsAny<DashboardFilterDto>(), It.IsAny<IReadOnlyCollection<int>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetSectionAsync_NormalizesFilterAndPropagatesScopeAndCancellation()
    {
        var (repository, scope) = CreateDependencies();
        using var cancellation = new CancellationTokenSource();
        repository.Setup(x => x.GetExecutiveAsync(
                It.Is<DashboardFilterDto>(filter =>
                    filter.FromDate == new DateTime(2026, 7, 1) &&
                    filter.ToDate == new DateTime(2026, 7, 17) &&
                    filter.Granularity == "Day" && filter.Top == 100),
                It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 10, 20 })),
                cancellation.Token))
            .ReturnsAsync(new ExecutiveDashboardData
            {
                NetSalesTrend = DashboardWidgetResult<NetSalesTrendRow>.Success([
                    new NetSalesTrendRow { BucketDate = new DateTime(2026, 7, 1), NetSales = 125000m }
                ])
            });

        var filter = CreateFilter();
        filter.FromDate = new DateTime(2026, 7, 1, 8, 30, 0);
        filter.ToDate = new DateTime(2026, 7, 17, 22, 0, 0);
        filter.Granularity = "day";
        filter.Top = 500;
        var service = new DashboardService(repository.Object, scope.Object);

        var raw = await service.GetSectionAsync(Actor, DashboardSection.Executive, filter, cancellation.Token);
        var result = Assert.IsType<DashboardSectionResponse<ExecutiveDashboardData>>(raw);

        Assert.Equal(new DateTime(2026, 7, 18), result.ToExclusive);
        Assert.Equal(new[] { 10, 20 }, result.StoreIds);
        Assert.Equal(125000m, Assert.Single(result.Data.NetSalesTrend.Data).NetSales);
    }

    [Fact]
    public async Task GetSectionAsync_RejectsInvertedDateRange()
    {
        var (repository, scope) = CreateDependencies();
        var service = new DashboardService(repository.Object, scope.Object);
        var filter = CreateFilter();
        filter.FromDate = new DateTime(2026, 7, 18);
        filter.ToDate = new DateTime(2026, 7, 17);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetSectionAsync(Actor, DashboardSection.Executive, filter));
    }

    [Fact]
    public async Task GetAnalyticsEndpoint_UsesActorAccessorAndRequestCancellation()
    {
        var service = new Mock<IDashboardService>();
        var actorAccessor = new Mock<IAdminActorContextAccessor>();
        DashboardAnalyticsFilter? capturedFilter = null;
        using var cancellation = new CancellationTokenSource();
        actorAccessor.Setup(x => x.Get(It.IsAny<ClaimsPrincipal>())).Returns(Actor);
        service.Setup(x => x.GetAnalyticsAsync(
                DashboardAnalyticsWidget.PaymentMethodMix,
                It.IsAny<DashboardAnalyticsFilter>(), cancellation.Token))
            .Callback<DashboardAnalyticsWidget, DashboardAnalyticsFilter, CancellationToken>((_, filter, _) => capturedFilter = filter)
            .ReturnsAsync(new DashboardAnalyticsResponse { Widget = DashboardAnalyticsWidget.PaymentMethodMix });

        var controller = new DashboardController(service.Object, actorAccessor.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestAborted = cancellation.Token }
            }
        };

        var result = await controller.GetAnalytics(
            DashboardAnalyticsWidget.PaymentMethodMix, new DashboardAnalyticsFilter());

        Assert.IsType<JsonResult>(result);
        Assert.NotNull(capturedFilter);
        Assert.Equal(7, ReadStaffId(capturedFilter!));
        service.VerifyAll();
    }

    private static (Mock<IDashboardRepository> Repository, Mock<IScopeAuthorizationService> Scope) CreateDependencies()
    {
        var repository = new Mock<IDashboardRepository>();
        repository.Setup(x => x.GetStoreOptionsAsync(
                It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new DashboardStoreOptionDto { StoreId = 20, StoreName = "Store 20", ProvinceId = 100, DistrictId = 200 },
                new DashboardStoreOptionDto { StoreId = 10, StoreName = "Store 10", ProvinceId = 100, DistrictId = 200 }
            ]);
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.GetAllowedStoresAsync(7)).ReturnsAsync([
            new Store { StoreId = 20 }, new Store { StoreId = 10 }
        ]);
        return (repository, scope);
    }

    private static DashboardFilterDto CreateFilter(int? storeId = null) => new()
    {
        FromDate = new DateTime(2026, 7, 1),
        ToDate = new DateTime(2026, 7, 17),
        StoreId = storeId
    };

    private static int? ReadStaffId(DashboardAnalyticsFilter filter) =>
        (int?)typeof(DashboardAnalyticsFilter)
            .GetProperty("StaffId", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(filter);
}
