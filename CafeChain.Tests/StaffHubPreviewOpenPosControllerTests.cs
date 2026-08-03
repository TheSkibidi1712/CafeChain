using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Interfaces.StaffHub;
using CafeChain.Application.Results;
using CafeChain.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;

namespace CafeChain.Tests;

public sealed class StaffHubPreviewOpenPosControllerTests
{
    [Fact]
    public async Task Preview_returns_assessment_without_issuing_exchange_ticket()
    {
        var workShifts = new Mock<IWorkShiftService>();
        workShifts.Setup(x => x.AssessOpenContextAsync(4, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<OpenShiftAssessmentDto>.Success(new OpenShiftAssessmentDto
            {
                OpenContext = "OUTSIDE_SCHEDULE",
                ReasonRequired = true,
                ApprovalRequired = true,
                ServerNowUtc = DateTime.UtcNow,
                AutoCloseAtUtc = DateTime.UtcNow.AddHours(6)
            }));
        var exchange = new Mock<IPosSessionExchangeService>();
        var controller = CreateController(workShifts.Object, exchange.Object);

        var response = await controller.PreviewOpenPos(CancellationToken.None);

        Assert.IsType<OkObjectResult>(response);
        exchange.Verify(x => x.IssueAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Preview_requires_antiforgery_and_open_permission()
    {
        var method = typeof(StaffHubController).GetMethod(nameof(StaffHubController.PreviewOpenPos));
        Assert.NotNull(method);
        Assert.NotEmpty(method!.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true));
        var permissions = method.GetCustomAttributes(inherit: true)
            .OfType<AuthorizeAttribute>()
            .Select(x => x.Policy)
            .ToList();
        Assert.Contains("Permission:" + PermissionConstants.PosWorkShiftOpen, permissions);
        Assert.Contains(AuthorizationPolicyConstants.PosApp, permissions);
    }

    private static StaffHubController CreateController(
        IWorkShiftService workShifts,
        IPosSessionExchangeService exchange)
    {
        var configuration = new ConfigurationBuilder().Build();
        var controller = new StaffHubController(
            Mock.Of<IStaffScheduleService>(),
            configuration,
            Mock.Of<IAuthorizationService>(),
            exchange,
            workShifts);
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "10"),
            new Claim("StaffId", "4"),
            new Claim("StoreId", "1")
        }, "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }
}
