using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Application.DTOs.Admin.InventoryTransfers;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.InventoryTransfers;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class InventoryTransferAuthorizationTests
{
    [Fact]
    public async Task AreaManager_OutsideScope_IsRejectedBeforeMutationService()
    {
        var fixture = CreateController(RoleConstants.AreaManager, staffId: 41);
        fixture.Scope
            .Setup(x => x.CanAccessStoreAsync(41, It.IsAny<int>()))
            .ReturnsAsync(false);

        var result = await fixture.Controller.Preflight(Transfer(1, 2));

        Assert.IsType<ForbidResult>(result);
        fixture.Service.Verify(x => x.ValidateStockAsync(It.IsAny<InventoryTransferMutationDTO>()), Times.Never);
    }

    [Fact]
    public async Task AreaManager_InScopeForBothStores_RemainsReadOnly()
    {
        var fixture = CreateController(RoleConstants.AreaManager, staffId: 42);
        fixture.Scope
            .Setup(x => x.CanAccessStoreAsync(42, It.IsAny<int>()))
            .ReturnsAsync(true);
        var result = await fixture.Controller.Preflight(Transfer(1, 2));

        Assert.IsType<ForbidResult>(result);
        fixture.Service.Verify(x => x.ValidateStockAsync(It.IsAny<InventoryTransferMutationDTO>()), Times.Never);
    }

    [Fact]
    public async Task AccountantWarehouse_GlobalProcessing_IsAllowed()
    {
        var fixture = CreateController(RoleConstants.AccountantWarehouse, staffId: 43);
        fixture.Service
            .Setup(x => x.ValidateStockAsync(It.IsAny<InventoryTransferMutationDTO>()))
            .ReturnsAsync([]);

        var result = await fixture.Controller.Preflight(Transfer(1, 999));

        Assert.IsType<JsonResult>(result);
        fixture.Scope.Verify(
            x => x.CanAccessStoreAsync(It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task StoreManager_CannotMutateCrossStoreTransfer()
    {
        var fixture = CreateController(RoleConstants.StoreManager, staffId: 44, storeId: 1);

        var result = await fixture.Controller.SaveDraft(Transfer(1, 2));

        Assert.IsType<ForbidResult>(result);
        fixture.Service.Verify(x => x.CreateDraftAsync(It.IsAny<InventoryTransferMutationDTO>()), Times.Never);
    }

    [Fact]
    public async Task AreaManager_IndexReceivesOnlyAllowedStoreIds()
    {
        var fixture = CreateController(RoleConstants.AreaManager, staffId: 45);
        fixture.Scope
            .Setup(x => x.GetAllowedStoresAsync(45))
            .ReturnsAsync(
            [
                new Store { StoreId = 7, Name = "S7", Active = true },
                new Store { StoreId = 8, Name = "S8", Active = true }
            ]);
        fixture.Service
            .Setup(x => x.GetIndexAsync(
                It.IsAny<CafeChain.ViewModels.Admin.InventoryTransfers.AdminInventoryTransferIndexVM>(),
                It.Is<IReadOnlyCollection<int>>(ids => ids.OrderBy(x => x).SequenceEqual(new[] { 7, 8 }))))
            .ReturnsAsync(new CafeChain.ViewModels.Admin.InventoryTransfers.AdminInventoryTransferIndexVM());

        var result = await fixture.Controller.Index(
            new CafeChain.ViewModels.Admin.InventoryTransfers.AdminInventoryTransferIndexVM());

        Assert.IsType<ViewResult>(result);
        fixture.Service.VerifyAll();
    }

    private static InventoryTransferMutationDTO Transfer(int fromStoreId, int toStoreId) => new()
    {
        RequestKey = Guid.NewGuid().ToString("N"),
        FromStoreId = fromStoreId,
        ToStoreId = toStoreId,
        Details =
        [
            new InventoryTransferDetailInputDTO
            {
                IngredientId = 1,
                Quantity = 1,
                UnitId = 1
            }
        ]
    };

    private static ControllerFixture CreateController(
        string role,
        int staffId,
        int storeId = 0)
    {
        var service = new Mock<IAdminInventoryTransferService>();
        var actor = new Mock<IAdminActorContextAccessor>();
        var scope = new Mock<IScopeAuthorizationService>();
        actor.Setup(x => x.Get(It.IsAny<ClaimsPrincipal>())).Returns(new AdminActorContext
        {
            StaffId = staffId,
            StoreId = storeId,
            RoleNames = [role]
        });

        var controller = new AdminInventoryTransferController(
            service.Object,
            actor.Object,
            scope.Object,
            NullLogger<AdminInventoryTransferController>.Instance);
        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, role),
            new("StaffId", staffId.ToString()),
            new("StoreId", storeId.ToString())
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
            }
        };

        return new ControllerFixture(controller, service, scope);
    }

    private sealed record ControllerFixture(
        AdminInventoryTransferController Controller,
        Mock<IAdminInventoryTransferService> Service,
        Mock<IScopeAuthorizationService> Scope);
}
