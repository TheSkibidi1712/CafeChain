using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.StoreScope;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Application.Interfaces.Operations;
using CafeChain.Application.Results;
using CafeChain.Areas.Admin.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CafeChain.Tests.POS;

public sealed class AdminTerminalConfirmationControllerTests
{
    [Theory]
    [InlineData(WorkShiftErrorCodes.TerminalApprovalForbidden, StatusCodes.Status403Forbidden)]
    [InlineData(WorkShiftErrorCodes.TerminalStoreScopeInvalid, StatusCodes.Status403Forbidden)]
    [InlineData(WorkShiftErrorCodes.TerminalApprovalNotFound, StatusCodes.Status404NotFound)]
    [InlineData(OtpConstants.ErrorCodes.VerificationLocked, StatusCodes.Status423Locked)]
    [InlineData(WorkShiftErrorCodes.TerminalNotPending, StatusCodes.Status409Conflict)]
    [InlineData(WorkShiftErrorCodes.TerminalApprovalConflict, StatusCodes.Status409Conflict)]
    [InlineData(OtpConstants.ErrorCodes.Invalid, StatusCodes.Status400BadRequest)]
    public async Task ConfirmTerminal_maps_business_error_to_json_http_status(
        string errorCode,
        int expectedStatus)
    {
        var terminal = new Mock<ITerminalRegistrationNotificationService>();
        terminal.Setup(x => x.ConfirmAsync(
                9,
                41,
                It.Is<ConfirmTerminalNotificationRequestDto>(r =>
                    r.OtpCode == "ABC234" && r.RequestKey == "request-1"),
                It.Is<IReadOnlyCollection<int>>(stores => stores.SequenceEqual(new[] { 1 }))))
            .ReturnsAsync(ServiceResult<TerminalApprovalResultDto>.Failure(
                "Terminal confirmation failed.",
                errorCode: errorCode));
        var controller = CreateController(terminal.Object);

        var action = await controller.ConfirmTerminal(41, new ConfirmTerminalNotificationRequestDto
        {
            OtpCode = "ABC234",
            RequestKey = "request-1"
        });

        var response = Assert.IsType<ObjectResult>(action);
        Assert.Equal(expectedStatus, response.StatusCode);
        terminal.VerifyAll();
    }

    [Fact]
    public async Task ConfirmTerminal_returns_approved_payload_for_ajax_request()
    {
        var terminal = new Mock<ITerminalRegistrationNotificationService>();
        terminal.Setup(x => x.ConfirmAsync(
                9,
                41,
                It.IsAny<ConfirmTerminalNotificationRequestDto>(),
                It.IsAny<IReadOnlyCollection<int>>()))
            .ReturnsAsync(ServiceResult<TerminalApprovalResultDto>.Success(
                new TerminalApprovalResultDto
                {
                    TerminalId = "TERM-1",
                    Status = "APPROVED",
                    AlreadyProcessed = false
                },
                "Terminal đã được xác nhận."));
        var controller = CreateController(terminal.Object);

        var action = await controller.ConfirmTerminal(41, new ConfirmTerminalNotificationRequestDto
        {
            OtpCode = "ABC234",
            RequestKey = "request-1"
        });

        var response = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Contains("APPROVED", System.Text.Json.JsonSerializer.Serialize(response.Value));
    }

    [Theory]
    [InlineData(WorkShiftErrorCodes.TerminalRejectionForbidden, StatusCodes.Status403Forbidden)]
    [InlineData(WorkShiftErrorCodes.TerminalStoreScopeInvalid, StatusCodes.Status403Forbidden)]
    [InlineData(WorkShiftErrorCodes.TerminalApprovalNotFound, StatusCodes.Status404NotFound)]
    [InlineData(WorkShiftErrorCodes.TerminalAlreadyApproved, StatusCodes.Status409Conflict)]
    [InlineData(WorkShiftErrorCodes.TerminalApprovalConflict, StatusCodes.Status409Conflict)]
    [InlineData(WorkShiftErrorCodes.TerminalRejectionReasonInvalid, StatusCodes.Status400BadRequest)]
    public async Task RejectTerminal_maps_business_error_to_json_http_status(
        string errorCode,
        int expectedStatus)
    {
        var terminal = new Mock<ITerminalRegistrationNotificationService>();
        terminal.Setup(x => x.RejectAsync(
                9,
                41,
                It.Is<RejectTerminalNotificationRequestDto>(r =>
                    r.Reason == "Thiết bị không hợp lệ" && r.RequestKey == "request-reject-1"),
                It.Is<IReadOnlyCollection<int>>(stores => stores.SequenceEqual(new[] { 1 }))))
            .ReturnsAsync(ServiceResult<TerminalApprovalResultDto>.Failure(
                "Terminal rejection failed.",
                errorCode: errorCode));
        var controller = CreateController(terminal.Object);

        var action = await controller.RejectTerminal(41, new RejectTerminalNotificationRequestDto
        {
            Reason = "Thiết bị không hợp lệ",
            RequestKey = "request-reject-1"
        });

        var response = Assert.IsType<ObjectResult>(action);
        Assert.Equal(expectedStatus, response.StatusCode);
        terminal.VerifyAll();
    }

    private static AdminNotificationsController CreateController(
        ITerminalRegistrationNotificationService terminal)
    {
        var actor = new AdminActorContext
        {
            AccountId = 3,
            StaffId = 9,
            StoreId = 1
        };
        var actorAccessor = new Mock<IAdminActorContextAccessor>();
        actorAccessor.Setup(x => x.Get(It.IsAny<ClaimsPrincipal>())).Returns(actor);
        var scopeResolver = new Mock<IAdminStoreScopeResolver>();
        scopeResolver.Setup(x => x.ResolveAsync(actor, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminStoreScopeResolution
            {
                Status = AdminStoreScopeResolutionStatus.Resolved,
                StoreId = 1,
                AccessibleStores = new[]
                {
                    new AdminStoreOptionDto { StoreId = 1, StoreName = "Store 1" }
                }
            });
        var controller = new AdminNotificationsController(
            Mock.Of<IStaffNotificationQueryService>(),
            actorAccessor.Object,
            scopeResolver.Object,
            terminal);
        var http = new DefaultHttpContext();
        http.Request.Headers.Accept = "application/json";
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim("StaffId", "9") },
            "Test"));
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return controller;
    }
}
