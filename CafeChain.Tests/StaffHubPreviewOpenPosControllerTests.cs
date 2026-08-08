using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.DTOs.StaffHub;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Interfaces.StaffHub;
using CafeChain.Application.Results;
using CafeChain.Controllers;
using CafeChain.Models.Stores;
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
        workShifts.Setup(x => x.AssessOpenContextAsync(4, 1, "terminal-1", It.IsAny<CancellationToken>()))
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

        var response = await controller.PreviewOpenPos(new StaffHubPosPreviewRequestDto
        {
            TerminalId = "terminal-1",
            RequestKey = Guid.NewGuid().ToString("N")
        }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(response);
        exchange.Verify(x => x.IssueAsync(
            It.IsAny<PosSessionExchangeContextDto>(), It.IsAny<CancellationToken>()), Times.Never);
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

    [Fact]
    public async Task Request_open_pos_otp_sets_supported_target_type()
    {
        var workShifts = new Mock<IWorkShiftService>();
        workShifts.Setup(x => x.AssessOpenContextAsync(4, 1, "terminal-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<OpenShiftAssessmentDto>.Success(new OpenShiftAssessmentDto
            {
                OpenContext = WorkShiftOpenContexts.OutsideSchedule,
                ReasonRequired = true,
                ApprovalRequired = true
            }));
        var otp = new Mock<IOtpApprovalService>();
        otp.Setup(x => x.RequestOtpAsync(
                It.Is<OtpRequestDto>(request =>
                    request.TargetType == OtpConstants.TargetTypes.Shifts
                    && request.ActionType == OtpConstants.ActionTypes.OpenShiftOutsideSchedule),
                4,
                1))
            .ReturnsAsync(ServiceResult<OtpChallengeResponseDto>.Success(new OtpChallengeResponseDto
            {
                OtpChallengePublicId = Guid.NewGuid(),
                Status = OtpConstants.Statuses.Pending
            }));
        var controller = CreateController(workShifts.Object, Mock.Of<IPosSessionExchangeService>(), otp.Object);

        var response = await controller.RequestOpenPosOtp(new StaffHubOpenOtpRequestDto
        {
            TerminalId = "terminal-1",
            RequestKey = Guid.NewGuid().ToString("N"),
            Reason = "Mở POS ngoài lịch để hỗ trợ cửa hàng."
        }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(response);
        otp.VerifyAll();
    }

    [Fact]
    public async Task Request_terminal_registration_otp_sets_supported_target_type()
    {
        var otp = new Mock<IOtpApprovalService>();
        otp.Setup(x => x.RequestOtpAsync(
                It.Is<OtpRequestDto>(request =>
                    request.TargetType == OtpConstants.TargetTypes.Shifts
                    && request.ActionType == OtpConstants.ActionTypes.RegisterTerminal),
                4,
                1))
            .ReturnsAsync(ServiceResult<OtpChallengeResponseDto>.Success(new OtpChallengeResponseDto
            {
                OtpChallengePublicId = Guid.NewGuid(),
                Status = OtpConstants.Statuses.Pending
            }));
        var controller = CreateController(
            Mock.Of<IWorkShiftService>(),
            Mock.Of<IPosSessionExchangeService>(),
            otp.Object);

        var response = await controller.RequestTerminalRegistrationOtp(new StaffHubTerminalOtpRequestDto
        {
            TerminalId = "terminal-new",
            TerminalName = "Quầy mang đi",
            RequestKey = Guid.NewGuid().ToString("N")
        });

        Assert.IsType<OkObjectResult>(response);
        otp.VerifyAll();
    }

    [Fact]
    public async Task Cancel_terminal_registration_is_scoped_to_current_staff_and_store()
    {
        var challengeId = Guid.NewGuid();
        var otp = new Mock<IOtpApprovalService>();
        otp.Setup(x => x.CancelTerminalRegistrationOtpAsync(
                It.Is<OtpCancelDto>(request => request.OtpChallengePublicId == challengeId),
                4,
                1))
            .ReturnsAsync(ServiceResult<OtpChallengeResponseDto>.Success(new OtpChallengeResponseDto
            {
                OtpChallengePublicId = challengeId,
                Status = OtpConstants.Statuses.Cancelled
            }));
        var controller = CreateController(
            Mock.Of<IWorkShiftService>(),
            Mock.Of<IPosSessionExchangeService>(),
            otp.Object);

        var response = await controller.CancelTerminalRegistrationOtp(new OtpCancelDto
        {
            OtpChallengePublicId = challengeId
        });

        Assert.IsType<OkObjectResult>(response);
        otp.VerifyAll();
    }

    private static StaffHubController CreateController(
        IWorkShiftService workShifts,
        IPosSessionExchangeService exchange,
        IOtpApprovalService? otp = null)
    {
        var configuration = new ConfigurationBuilder().Build();
        var controller = new StaffHubController(
            Mock.Of<IStaffScheduleService>(),
            configuration,
            Mock.Of<IAuthorizationService>(),
            exchange,
            workShifts,
            otp ?? Mock.Of<IOtpApprovalService>());
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
