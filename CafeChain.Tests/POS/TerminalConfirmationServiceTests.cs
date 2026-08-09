using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Results;
using CafeChain.Application.Services.POS;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Models.Operations;
using CafeChain.Models.Stores;
using CafeChain.Models.Staffs;
using CafeChain.Models.Customers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CafeChain.Tests.POS;

public sealed class TerminalConfirmationServiceTests
{
    private const string ValidOtp = "ABC234";
    private static readonly DateTimeOffset Now = new(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Confirm_rejects_missing_request_key_before_starting_transaction(string requestKey)
    {
        var otpRepository = new Mock<IOtpChallengeRepository>(MockBehavior.Strict);
        var service = CreateService(Mock.Of<IWorkShiftRepository>(), otpRepository.Object);

        var result = await service.ConfirmTerminalRegistrationAsync(9, 1, Guid.NewGuid(), ValidOtp, requestKey);

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkShiftErrorCodes.InvalidRequestKey, result.ErrorCode);
        otpRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Confirm_rejects_request_key_longer_than_database_contract()
    {
        var otpRepository = new Mock<IOtpChallengeRepository>(MockBehavior.Strict);
        var service = CreateService(Mock.Of<IWorkShiftRepository>(), otpRepository.Object);

        var result = await service.ConfirmTerminalRegistrationAsync(
            9, 1, Guid.NewGuid(), ValidOtp, new string('K', 201));

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkShiftErrorCodes.InvalidRequestKey, result.ErrorCode);
        otpRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Confirm_creates_terminal_and_consumes_challenge_atomically()
    {
        var challenge = PendingChallenge();
        var otpRepository = RepositoryFor(challenge);
        var shifts = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
        shifts.Setup(x => x.RegisterPosTerminalAsync("TERM-1", 1, "Quầy 1"))
            .ReturnsAsync(new PosTerminal
            {
                TerminalId = "TERM-1",
                StoreId = 1,
                Name = "Quầy 1",
                Active = true
            });
        var service = CreateService(shifts.Object, otpRepository.Object);

        var result = await service.ConfirmTerminalRegistrationAsync(
            9, 1, challenge.PublicId, ValidOtp, "confirm-terminal-1");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("TERM-1", result.Data.TerminalId);
        Assert.Equal("APPROVED", result.Data.Status);
        Assert.False(result.Data.AlreadyProcessed);
        Assert.Equal(OtpConstants.Statuses.Used, challenge.Status);
        Assert.Equal(9, challenge.ConfirmedByStaffId);
        Assert.Equal(Now.UtcDateTime, challenge.ApprovedAt);
        Assert.Equal(Now.UtcDateTime, challenge.UsedAt);
        Assert.Null(challenge.ProtectedOtpPayload);
        otpRepository.Verify(x => x.SaveChangesAsync(), Times.Once);
        otpRepository.Verify(x => x.CommitTransactionAsync(), Times.Once);
        otpRepository.Verify(x => x.RollbackTransactionAsync(), Times.Never);
        shifts.VerifyAll();
    }

    [Fact]
    public async Task Confirm_preserves_business_error_instead_of_returning_generic_conflict()
    {
        var challenge = PendingChallenge();
        var otpRepository = RepositoryFor(challenge, expectSaveAndCommit: false);
        var shifts = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
        shifts.Setup(x => x.RegisterPosTerminalAsync("TERM-1", 1, "Quầy 1"))
            .ThrowsAsync(new WorkShiftBusinessException(
                WorkShiftErrorCodes.TerminalStoreMismatch,
                "Terminal đã thuộc cửa hàng khác."));
        var service = CreateService(shifts.Object, otpRepository.Object);

        var result = await service.ConfirmTerminalRegistrationAsync(
            9, 1, challenge.PublicId, ValidOtp, "confirm-terminal-2");

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkShiftErrorCodes.TerminalStoreMismatch, result.ErrorCode);
        otpRepository.Verify(x => x.RollbackTransactionAsync(), Times.Once);
        otpRepository.Verify(x => x.CommitTransactionAsync(), Times.Never);
        shifts.VerifyAll();
    }

    [Fact]
    public async Task Confirm_used_challenge_is_idempotent_and_does_not_create_duplicate_terminal()
    {
        var challenge = PendingChallenge();
        challenge.Status = OtpConstants.Statuses.Used;
        var otpRepository = new Mock<IOtpChallengeRepository>(MockBehavior.Strict);
        otpRepository.Setup(x => x.BeginTransactionAsync()).Returns(Task.CompletedTask);
        otpRepository.Setup(x => x.GetByPublicIdForUpdateAsync(challenge.PublicId)).ReturnsAsync(challenge);
        otpRepository.Setup(x => x.RollbackTransactionAsync()).Returns(Task.CompletedTask);
        var shifts = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
        var service = CreateService(shifts.Object, otpRepository.Object);

        var result = await service.ConfirmTerminalRegistrationAsync(
            9, 1, challenge.PublicId, ValidOtp, "confirm-terminal-retry");

        Assert.True(result.IsSuccess);
        Assert.True(result.Data?.AlreadyProcessed);
        Assert.Equal("TERM-1", result.Data?.TerminalId);
        shifts.VerifyNoOtherCalls();
        otpRepository.Verify(x => x.RollbackTransactionAsync(), Times.Once);
    }

    [Fact]
    public async Task Reject_requires_specific_permission_and_valid_reason()
    {
        var shifts = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
        var otpRepository = new Mock<IOtpChallengeRepository>(MockBehavior.Strict);
        var service = CreateService(shifts.Object, otpRepository.Object);

        var result = await service.RejectTerminalRegistrationAsync(
            12, 1, Guid.NewGuid(), "ngắn", "reject-terminal-1");

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkShiftErrorCodes.TerminalRejectionReasonInvalid, result.ErrorCode);
        shifts.VerifyNoOtherCalls();
        otpRepository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Reject_marks_challenge_rejected_without_creating_terminal()
    {
        var challenge = PendingChallenge();
        var otpRepository = RepositoryFor(challenge);
        var shifts = new Mock<IWorkShiftRepository>(MockBehavior.Strict);
        shifts.Setup(x => x.GetStaffForOperatorAsync(12)).ReturnsAsync(new Staff
        {
            StaffId = 12,
            AccountId = 22,
            StoreId = 1,
            Active = true,
            Account = new Account { AccountId = 22, Active = true }
        });
        var permissions = new Mock<IAdminPermissionService>(MockBehavior.Strict);
        permissions.Setup(x => x.HasPermissionAsync(
                22, PermissionConstants.PosWorkShiftRejectTerminal, 1))
            .ReturnsAsync(ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
            {
                AccountId = 22,
                PermissionCode = PermissionConstants.PosWorkShiftRejectTerminal,
                TargetStoreId = 1,
                Allowed = true,
                ScopeAllowed = true
            }));
        var service = CreateService(shifts.Object, otpRepository.Object, permissions.Object);

        var result = await service.RejectTerminalRegistrationAsync(
            12,
            1,
            challenge.PublicId,
            "Thiết bị không thuộc tài sản cửa hàng",
            "reject-terminal-2");

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal("REJECTED", result.Data?.Status);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.AlreadyProcessed);
        Assert.Equal(OtpConstants.Statuses.Rejected, challenge.Status);
        Assert.Equal(12, challenge.ConfirmedByStaffId);
        Assert.Equal(Now.UtcDateTime, challenge.CancelledAt);
        Assert.Null(challenge.ProtectedOtpPayload);
        shifts.Verify(x => x.RegisterPosTerminalAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        permissions.VerifyAll();
        otpRepository.Verify(x => x.CommitTransactionAsync(), Times.Once);
    }

    private static WorkShiftService CreateService(
        IWorkShiftRepository shifts,
        IOtpChallengeRepository otpRepository,
        IAdminPermissionService? permissions = null)
    {
        var generator = new Mock<IOtpCodeGenerator>();
        generator.Setup(x => x.NormalizeAndValidate(It.IsAny<string?>()))
            .Returns((string? value) => string.Equals(value?.Trim(), ValidOtp, StringComparison.OrdinalIgnoreCase)
                ? ValidOtp
                : null);
        return new WorkShiftService(
            shifts,
            Mock.Of<IPOSOrderRepository>(),
            otpRepository,
            Mock.Of<IOtpPayloadFingerprintService>(),
            NullLogger<WorkShiftService>.Instance,
            timeProvider: new FixedTimeProvider(Now),
            permissions: permissions,
            otpCodeGenerator: generator.Object);
    }

    private static Mock<IOtpChallengeRepository> RepositoryFor(
        OtpChallenge challenge,
        bool expectSaveAndCommit = true)
    {
        var repository = new Mock<IOtpChallengeRepository>(MockBehavior.Strict);
        repository.Setup(x => x.BeginTransactionAsync()).Returns(Task.CompletedTask);
        repository.Setup(x => x.GetByPublicIdForUpdateAsync(challenge.PublicId)).ReturnsAsync(challenge);
        if (expectSaveAndCommit)
        {
            repository.Setup(x => x.SaveChangesAsync()).Returns(Task.CompletedTask);
            repository.Setup(x => x.CommitTransactionAsync()).Returns(Task.CompletedTask);
        }
        else
        {
            repository.Setup(x => x.RollbackTransactionAsync()).Returns(Task.CompletedTask);
        }
        return repository;
    }

    private static OtpChallenge PendingChallenge()
    {
        return new OtpChallenge
        {
            PublicId = Guid.NewGuid(),
            StoreId = 1,
            RequestedByStaffId = 4,
            ApproverStaffId = 9,
            ActionType = OtpConstants.ActionTypes.RegisterTerminal,
            TargetType = "POS_TERMINAL",
            TerminalId = "TERM-1",
            TerminalName = "Quầy 1",
            OtpHash = BCrypt.Net.BCrypt.HashPassword(ValidOtp),
            ProtectedOtpPayload = "protected-not-plaintext",
            Status = OtpConstants.Statuses.Pending,
            CreatedAt = Now.UtcDateTime.AddMinutes(-1),
            LastSentAt = Now.UtcDateTime.AddMinutes(-1),
            ExpiresAt = Now.UtcDateTime.AddMinutes(4)
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
