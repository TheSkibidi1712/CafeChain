using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.DTOs.POS;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Operations;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Options;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Operations;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Operations;
using CafeChain.Models.Stores;
using Microsoft.Extensions.Options;
using Moq;

namespace CafeChain.Tests;

public sealed class InventoryNotificationAudienceResolverTests
{
    [Fact]
    public async Task Resolve_includes_mandatory_roles_without_email_or_permission_and_keeps_permission_recipient()
    {
        var repository = new Mock<IInventoryReorderNotificationRepository>();
        repository.Setup(x => x.GetRecipientCandidatesAsync()).ReturnsAsync(
        [
            Candidate(1, 101, RoleConstants.StoreManager, email: null),
            Candidate(2, 102, RoleConstants.AccountantWarehouse, email: null),
            Candidate(3, 103, RoleConstants.ShiftSupervisor, email: "scope@cafechain.vn"),
            Candidate(4, 104, RoleConstants.SalesStaff, email: "sales@cafechain.vn")
        ]);

        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), 7)).ReturnsAsync(true);

        var permissions = PermissionMock((accountId, storeId) =>
            accountId == 103 && storeId == 7);
        var resolver = new InventoryNotificationAudienceResolver(
            repository.Object,
            scope.Object,
            permissions.Object);

        var recipients = await resolver.ResolveAsync(7);

        Assert.Equal([1, 2, 3], recipients.Select(x => x.StaffId).OrderBy(x => x));
        Assert.All(recipients.Where(x => x.StaffId is 1 or 2), x => Assert.Null(x.Email));
        permissions.Verify(
            x => x.HasPermissionAsync(101, PermissionConstants.NotificationView, 7),
            Times.Never);
        permissions.Verify(
            x => x.HasPermissionAsync(102, PermissionConstants.NotificationView, 7),
            Times.Never);
    }

    [Fact]
    public async Task ResolveStoreIds_returns_only_scoped_stores_for_accountant()
    {
        var repository = new Mock<IInventoryReorderNotificationRepository>();
        repository.Setup(x => x.GetRecipientCandidatesAsync()).ReturnsAsync(
        [
            Candidate(2, 102, RoleConstants.AccountantWarehouse, email: null)
        ]);

        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.GetAllowedStoresAsync(2)).ReturnsAsync(
        [
            new Store { StoreId = 3, Name = "Store 3", Active = true },
            new Store { StoreId = 8, Name = "Store 8", Active = true }
        ]);
        var permissions = PermissionMock((_, _) => false);
        var resolver = new InventoryNotificationAudienceResolver(
            repository.Object,
            scope.Object,
            permissions.Object);

        var storeIds = await resolver.ResolveStoreIdsAsync(2);

        Assert.Equal([3, 8], storeIds.OrderBy(x => x));
    }

    private static ReorderNotificationRecipientRow Candidate(
        int staffId,
        int accountId,
        string role,
        string? email) =>
        new(staffId, accountId, [role])
        {
            Email = email,
            FullName = $"Staff {staffId}"
        };

    private static Mock<IAdminPermissionService> PermissionMock(
        Func<int, int?, bool> allowed)
    {
        var permissions = new Mock<IAdminPermissionService>();
        permissions
            .Setup(x => x.HasPermissionAsync(
                It.IsAny<int>(),
                PermissionConstants.NotificationView,
                It.IsAny<int?>()))
            .ReturnsAsync((int accountId, string code, int? storeId) =>
                ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
                {
                    AccountId = accountId,
                    PermissionCode = code,
                    Allowed = allowed(accountId, storeId)
                }));
        return permissions;
    }
}

public sealed class InventoryNotificationDeliveryServiceTests
{
    [Fact]
    public async Task Missing_recipient_email_does_not_block_database_commit_or_realtime_publish()
    {
        var audience = new Mock<IInventoryNotificationAudienceResolver>();
        audience.Setup(x => x.ResolveAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new InventoryNotificationRecipient(1, 101, null, "Store Manager")
            ]);

        StaffNotification? added = null;
        var repository = new Mock<IStaffNotificationRepository>();
        repository
            .Setup(x => x.GetActiveByDeduplicationKeyAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((StaffNotification?)null);
        repository.Setup(x => x.Add(It.IsAny<StaffNotification>()))
            .Callback<StaffNotification>(x => added = x);
        repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        InventoryNotificationChangedDto? published = null;
        var publisher = new Mock<IInventoryNotificationPublisher>();
        publisher
            .Setup(x => x.PublishAsync(
                It.IsAny<InventoryNotificationChangedDto>(),
                It.IsAny<CancellationToken>()))
            .Callback<InventoryNotificationChangedDto, CancellationToken>((x, _) => published = x)
            .Returns(Task.CompletedTask);

        var service = new InventoryNotificationDeliveryService(
            audience.Object,
            repository.Object,
            publisher.Object,
            Options.Create(new InventoryNotificationOptions
            {
                InventoryCooldownMinutes = 15
            }));

        var result = await service.DeliverAsync(new InventoryNotificationDeliveryRequest(
            7,
            "LOW_STOCK",
            "Low stock",
            "Ingredient is below threshold.",
            "WARNING",
            "Ingredient",
            15,
            InventoryNotificationChangeKinds.Created));

        Assert.True(result.Published);
        Assert.Equal(1, result.CreatedCount);
        Assert.NotNull(added);
        Assert.NotNull(published);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(
            x => x.PublishAsync(
                It.IsAny<InventoryNotificationChangedDto>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
