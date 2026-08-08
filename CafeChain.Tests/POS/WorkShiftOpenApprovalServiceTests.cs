using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.POS;
using CafeChain.Application.Options;
using CafeChain.Application.Results;
using CafeChain.Application.Services.POS;
using CafeChain.Infrastructure.Interfaces.Admin.POS;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Customers;
using CafeChain.Models.Operations;
using CafeChain.Models.Staffs;
using Microsoft.Extensions.Options;
using Moq;

namespace CafeChain.Tests.POS;

public sealed class WorkShiftOpenApprovalServiceTests
{
    private const int ManagerStaffId = 70;
    private const int ManagerAccountId = 700;
    private const int StoreId = 5;
    private static readonly DateTimeOffset Now = new(2026, 8, 6, 3, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("APPROVE", WorkShiftOpenApprovalStatuses.Approved)]
    [InlineData("REJECT", WorkShiftOpenApprovalStatuses.Rejected)]
    [InlineData("CONVERT", WorkShiftOpenApprovalStatuses.ConvertedToOutsideSchedule)]
    public async Task Manager_decisions_are_committed_and_published(string decision, string expectedStatus)
    {
        var approval = PendingApproval();
        var repository = RepositoryReturning(approval);
        var publisher = new Mock<IWorkShiftOpenApprovalPublisher>();
        WorkShiftOpenApprovalChangedDto? published = null;
        publisher.Setup(x => x.PublishAsync(It.IsAny<WorkShiftOpenApprovalChangedDto>(), It.IsAny<CancellationToken>()))
            .Callback<WorkShiftOpenApprovalChangedDto, CancellationToken>((value, _) => published = value)
            .Returns(Task.CompletedTask);
        var service = CreateService(repository.Object, publisher.Object);

        var result = await service.DecideAsync(
            ManagerStaffId,
            StoreId,
            approval.PublicId,
            new DecideWorkShiftOpenApprovalRequestDto
            {
                Decision = decision,
                Reason = "Quản lý đã kiểm tra vận hành",
                RowVersion = Convert.ToBase64String(approval.RowVersion!)
            });

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(expectedStatus, approval.Status);
        Assert.Equal(ManagerStaffId, approval.DecidedByStaffId);
        Assert.Equal(expectedStatus, published?.Status);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Invalid_rowversion_returns_stable_concurrency_error_instead_of_throwing()
    {
        var approval = PendingApproval();
        var repository = RepositoryReturning(approval);
        var service = CreateService(repository.Object);

        var result = await service.DecideAsync(
            ManagerStaffId,
            StoreId,
            approval.PublicId,
            new DecideWorkShiftOpenApprovalRequestDto
            {
                Decision = "APPROVE",
                RowVersion = "not-base64"
            });

        Assert.False(result.IsSuccess);
        Assert.Equal(WorkShiftErrorCodes.ConcurrencyConflict, result.ErrorCode);
        Assert.Equal(WorkShiftOpenApprovalStatuses.Pending, approval.Status);
        repository.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Repeated_decision_is_idempotent_and_does_not_mutate_again()
    {
        var approval = PendingApproval();
        approval.Status = WorkShiftOpenApprovalStatuses.Approved;
        var repository = RepositoryReturning(approval);
        var service = CreateService(repository.Object);

        var result = await service.DecideAsync(
            ManagerStaffId,
            StoreId,
            approval.PublicId,
            new DecideWorkShiftOpenApprovalRequestDto { Decision = "REJECT" });

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(WorkShiftOpenApprovalStatuses.Approved, result.Data!.Status);
        repository.Verify(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static WorkShiftOpenApprovalService CreateService(
        IWorkShiftOpenApprovalRepository repository,
        IWorkShiftOpenApprovalPublisher? publisher = null)
    {
        var staffLookup = new Mock<IOtpChallengeRepository>();
        staffLookup.Setup(x => x.GetRequestingStaffAsync(ManagerStaffId, StoreId))
            .ReturnsAsync(new Staff
            {
                StaffId = ManagerStaffId,
                AccountId = ManagerAccountId,
                StoreId = StoreId,
                Active = true,
                Account = new Account { AccountId = ManagerAccountId, Active = true }
            });
        var permissions = new Mock<IAdminPermissionService>();
        permissions.Setup(x => x.HasPermissionAsync(
                ManagerAccountId, PermissionConstants.PosWorkShiftApproveLateOpen, StoreId))
            .ReturnsAsync(ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
            {
                AccountId = ManagerAccountId,
                PermissionCode = PermissionConstants.PosWorkShiftApproveLateOpen,
                Allowed = true
            }));

        return new WorkShiftOpenApprovalService(
            repository,
            Mock.Of<IWorkShiftService>(),
            staffLookup.Object,
            Options.Create(new WorkShiftOptions()),
            permissions.Object,
            publisher: publisher,
            timeProvider: new FixedTimeProvider(Now));
    }

    private static Mock<IWorkShiftOpenApprovalRepository> RepositoryReturning(
        WorkShiftOpenApprovalRequest approval)
    {
        var repository = new Mock<IWorkShiftOpenApprovalRepository>();
        repository.Setup(x => x.GetByPublicIdAsync(approval.PublicId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(approval);
        repository.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repository.Setup(x => x.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repository.Setup(x => x.RollbackTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        return repository;
    }

    private static WorkShiftOpenApprovalRequest PendingApproval() => new()
    {
        PublicId = Guid.NewGuid(),
        RequestKey = Guid.NewGuid().ToString("N"),
        StoreId = StoreId,
        RequestedByStaffId = 80,
        SourceStaffShiftId = 90,
        TerminalId = "POS-01",
        MinutesLate = 31,
        Reason = "Tắc đường đến cửa hàng",
        Status = WorkShiftOpenApprovalStatuses.Pending,
        RequestedAtUtc = Now.UtcDateTime,
        ExpiresAtUtc = Now.AddHours(1).UtcDateTime,
        RowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }
    };

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
