using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Interfaces.Systems;
using CafeChain.Application.Services.Admin.Permissions;
using CafeChain.Infrastructure.Interfaces.Admin.Permissions;
using CafeChain.Models.Enums.Permissions;
using CafeChain.Models.Permissions;
using Moq;

namespace CafeChain.Tests;

public sealed class BomProductionRbacAlignmentTests : IntegrationTestBase
{
    [Fact]
    public async Task AccountDeny_OverridesRolePermission()
    {
        using var fixture = CreatePermissionService(
            roleAllowed: true,
            overrideEffect: PermissionEffect.Deny,
            scopeAllowed: true);

        var result = await fixture.Service.HasPermissionAsync(
            31,
            PermissionConstants.RecipeUpdate);

        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.Allowed);
        Assert.Equal(PermissionEffect.Deny, result.Data.OverrideEffect);
        Assert.Equal("Denied by account override.", result.Data.DenyReason);
    }

    [Fact]
    public async Task ExplicitAccountAllow_DoesNotBroadenOtherAccounts()
    {
        using var context = CreateDbContext();
        var repository = new Mock<IAdminPermissionRepository>(MockBehavior.Strict);
        repository.Setup(x => x.GetActivePermissionByCodeAsync(PermissionConstants.ProductionOrderView))
            .ReturnsAsync(ActivePermission(PermissionConstants.ProductionOrderView));
        repository.Setup(x => x.GetAccountPermissionFactsAsync(41, 901))
            .ReturnsAsync(PermissionFacts(roleAllowed: false, PermissionEffect.Allow));
        repository.Setup(x => x.GetAccountPermissionFactsAsync(42, 901))
            .ReturnsAsync(PermissionFacts(roleAllowed: false, overrideEffect: null));
        var scope = new Mock<IScopeAuthorizationService>(MockBehavior.Strict);
        var deduplication = new Mock<IRequestDeduplicationService>(MockBehavior.Strict);
        var service = new AdminPermissionService(
            repository.Object,
            scope.Object,
            deduplication.Object,
            context);

        var allowed = await service.HasPermissionAsync(41, PermissionConstants.ProductionOrderView);
        var unchanged = await service.HasPermissionAsync(42, PermissionConstants.ProductionOrderView);

        Assert.True(allowed.Data!.Allowed);
        Assert.Equal(PermissionEffect.Allow, allowed.Data.OverrideEffect);
        Assert.False(unchanged.Data!.Allowed);
        Assert.Null(unchanged.Data.OverrideEffect);
    }

    [Fact]
    public async Task ProductionPermission_RequiresAuthorizedStoreScope()
    {
        const int storeA = 7101;
        const int storeB = 7102;
        using var fixture = CreatePermissionService(
            roleAllowed: true,
            overrideEffect: null,
            scopeAllowed: storeId => storeId == storeA);

        var ownStore = await fixture.Service.HasPermissionAsync(
            31,
            PermissionConstants.ProductionOrderStart,
            storeA);
        var otherStore = await fixture.Service.HasPermissionAsync(
            31,
            PermissionConstants.ProductionOrderStart,
            storeB);

        Assert.True(ownStore.Data!.Allowed);
        Assert.True(ownStore.Data.ScopeAllowed);
        Assert.False(otherStore.Data!.Allowed);
        Assert.False(otherStore.Data.ScopeAllowed);
        Assert.Equal("Store is outside staff scope.", otherStore.Data.DenyReason);
    }

    private PermissionFixture CreatePermissionService(
        bool roleAllowed,
        PermissionEffect? overrideEffect,
        bool scopeAllowed) =>
        CreatePermissionService(roleAllowed, overrideEffect, _ => scopeAllowed);

    private PermissionFixture CreatePermissionService(
        bool roleAllowed,
        PermissionEffect? overrideEffect,
        Func<int, bool> scopeAllowed)
    {
        var context = CreateDbContext();
        var repository = new Mock<IAdminPermissionRepository>(MockBehavior.Strict);
        repository.Setup(x => x.GetActivePermissionByCodeAsync(It.IsAny<string>()))
            .ReturnsAsync((string code) => ActivePermission(code));
        repository.Setup(x => x.GetAccountPermissionFactsAsync(31, 901))
            .ReturnsAsync(PermissionFacts(roleAllowed, overrideEffect));
        var scope = new Mock<IScopeAuthorizationService>(MockBehavior.Strict);
        scope.Setup(x => x.CheckIfStoreIsWithinManagerScopeAsync(301, It.IsAny<int>()))
            .ReturnsAsync((int _, int storeId) => scopeAllowed(storeId));
        var deduplication = new Mock<IRequestDeduplicationService>(MockBehavior.Strict);
        return new PermissionFixture(
            new AdminPermissionService(
                repository.Object,
                scope.Object,
                deduplication.Object,
                context),
            context);
    }

    private static Permission ActivePermission(string code) => new()
    {
        PermissionId = 901,
        PermissionGroupId = 1,
        Code = code,
        Name = code,
        Action = "View",
        Active = true,
        CreatedAt = new DateTime(2026, 1, 1)
    };

    private static AccountPermissionFactsDto PermissionFacts(
        bool roleAllowed,
        PermissionEffect? overrideEffect) => new()
    {
        AccountExists = true,
        AccountActive = true,
        StaffId = 301,
        RoleAllowed = roleAllowed,
        OverrideEffect = overrideEffect
    };

    private sealed record PermissionFixture(
        AdminPermissionService Service,
        IDisposable Context) : IDisposable
    {
        public void Dispose() => Context.Dispose();
    }
}
