using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using Moq;

namespace CafeChain.Tests;

public sealed class ReorderSuggestionAuthorizationTests : IntegrationTestBase
{
    [Fact]
    public async Task PermissionGrantAndReorderScopeAllowWithoutRoleGate()
    {
        await using var db = CreateDbContext();
        var permissions = new Mock<IAdminPermissionService>(MockBehavior.Strict);
        permissions
            .Setup(x => x.HasPermissionAsync(
                900,
                PermissionConstants.ReorderSuggestionView,
                null))
            .ReturnsAsync(Allowed(PermissionConstants.ReorderSuggestionView));
        permissions
            .Setup(x => x.HasPermissionAsync(
                900,
                PermissionConstants.RestockCreate,
                null))
            .ReturnsAsync(Allowed(PermissionConstants.RestockCreate));
        var scope = new Mock<IScopeAuthorizationService>(MockBehavior.Strict);
        scope
            .Setup(x => x.CanAccessStoreAsync(
                901,
                1,
                StoreScopePurpose.ReorderSuggestion))
            .ReturnsAsync(true);
        var service = new ReorderSuggestionAuthorizationService(
            db,
            permissions.Object,
            scope.Object);
        var actor = new AdminActorContext
        {
            AccountId = 900,
            StaffId = 901,
            RoleNames = Array.Empty<string>()
        };

        Assert.True(await service.CanViewAsync(actor, 1));
        Assert.True(await service.CanConfirmAsync(actor, 1));
    }

    [Fact]
    public async Task PermissionDenyStopsBeforeStoreScope()
    {
        await using var db = CreateDbContext();
        var permissions = new Mock<IAdminPermissionService>(MockBehavior.Strict);
        permissions
            .Setup(x => x.HasPermissionAsync(
                900,
                PermissionConstants.ReorderSuggestionView,
                null))
            .ReturnsAsync(ServiceResult<PermissionDecisionDto>.Success(
                new PermissionDecisionDto
                {
                    AccountId = 900,
                    StaffId = 901,
                    PermissionCode = PermissionConstants.ReorderSuggestionView,
                    Allowed = false,
                    DenyReason = "Denied by account override."
                }));
        var scope = new Mock<IScopeAuthorizationService>(MockBehavior.Strict);
        var service = new ReorderSuggestionAuthorizationService(
            db,
            permissions.Object,
            scope.Object);

        var allowed = await service.CanViewAsync(
            new AdminActorContext
            {
                AccountId = 900,
                StaffId = 901
            },
            1);

        Assert.False(allowed);
        scope.VerifyNoOtherCalls();
    }

    private static ServiceResult<PermissionDecisionDto> Allowed(string code) =>
        ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
        {
            AccountId = 900,
            StaffId = 901,
            PermissionCode = code,
            Allowed = true
        });
}
