using System.Security.Claims;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.InventoryDocuments.Create;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Interfaces.Systems;
using CafeChain.Application.Services.Admin.InventoryDocuments;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Http;
using Moq;

namespace CafeChain.Tests;

public sealed class InventoryDocumentStoreInventorySourceTests
{
    [Theory]
    [InlineData(InventoryDocumentType.EXPORT, InventoryDocumentPurpose.SALE, 1, 2, 3)]
    [InlineData(InventoryDocumentType.EXPORT, InventoryDocumentPurpose.GIFT, 1, 2, 3)]
    [InlineData(InventoryDocumentType.EXPORT, InventoryDocumentPurpose.ADJUSTMENT_OUT, 1)]
    [InlineData(InventoryDocumentType.WASTE, InventoryDocumentPurpose.DAMAGED, 1)]
    [InlineData(InventoryDocumentType.STOCK_TAKE, InventoryDocumentPurpose.STOCK_TAKE, 1, 2, 3)]
    public async Task StoreInventorySource_ReturnsOnlyEligibleActiveIngredientsForSelectedStore(
        InventoryDocumentType type,
        InventoryDocumentPurpose purpose,
        params int[] expectedIngredientIds)
    {
        var repository = new Mock<IAdminInventoryDocumentRepository>();
        var service = CreateService(repository);
        var inventories = new List<StoreInventory>
        {
            Inventory(3, 1, 8, active: true),
            Inventory(3, 2, 0, active: true),
            Inventory(3, 3, -2, active: true),
            Inventory(3, 4, 10, active: false)
        };

        repository
            .Setup(x => x.GetStoreInventoriesAsync(3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inventories);
        repository
            .Setup(x => x.GetAvailableCostLayersAsync(3, It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync([]);
        repository
            .Setup(x => x.GetActiveIngredientSuppliersByIngredientIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync([]);

        var result = await service.GetStoreInventoryIngredientsAsync(3, type, purpose);

        Assert.Equal(expectedIngredientIds, result.Select(x => x.IngredientId));
        Assert.All(result, item => Assert.Contains(item.IngredientId, new[] { 1, 2, 3 }));
        repository.Verify(x => x.GetActiveIngredientsAsync(), Times.Never);
    }

    [Theory]
    [InlineData(InventoryDocumentType.EXPORT)]
    [InlineData(InventoryDocumentType.WASTE)]
    [InlineData(InventoryDocumentType.STOCK_TAKE)]
    public async Task Validation_RejectsIngredientWithoutStoreInventory(InventoryDocumentType type)
    {
        var repository = new Mock<IAdminInventoryDocumentRepository>();
        var validation = new AdminInventoryDocumentValidationService(repository.Object);
        var purpose = type switch
        {
            InventoryDocumentType.EXPORT => InventoryDocumentPurpose.SALE,
            InventoryDocumentType.WASTE => InventoryDocumentPurpose.DAMAGED,
            _ => InventoryDocumentPurpose.STOCK_TAKE
        };
        var dto = new CreateInventoryDocumentDTO
        {
            StoreId = 3,
            Type = type,
            Purpose = purpose,
            DocumentDate = DateTime.Today,
            Note = type == InventoryDocumentType.WASTE ? "Hàng hỏng" : null,
            Details =
            [
                new CreateInventoryDocumentItemDTO
                {
                    IngredientId = 2,
                    UnitId = 1,
                    Quantity = type == InventoryDocumentType.STOCK_TAKE ? 0 : 1,
                    BaseQuantity = type == InventoryDocumentType.STOCK_TAKE ? 0 : 1
                }
            ]
        };

        repository.Setup(x => x.GetStoreAsync(3)).ReturnsAsync(new Store { StoreId = 3, Active = true });
        repository.Setup(x => x.GetIngredientAsync(2)).ReturnsAsync(Ingredient(2, true));
        repository.Setup(x => x.GetUnitAsync(1)).ReturnsAsync(BaseUnit());
        repository.Setup(x => x.GetStoreInventoryAsync(3, 2)).ReturnsAsync((StoreInventory?)null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => validation.ValidateCreateAsync(dto));

        Assert.Equal("INGREDIENT_NOT_IN_STORE_INVENTORY", exception.Message);
    }

    [Fact]
    public async Task ConfirmDraft_RejectsLegacyOutboundWithoutStoreInventory()
    {
        var repository = new Mock<IAdminInventoryDocumentRepository>();
        var validation = new AdminInventoryDocumentValidationService(repository.Object);
        var document = new InventoryDocument
        {
            StoreId = 3,
            Type = InventoryDocumentType.EXPORT,
            Purpose = InventoryDocumentPurpose.SALE,
            Status = InventoryDocumentStatus.DRAFT,
            Details = [new InventoryDocumentDetail { IngredientId = 2 }]
        };
        repository.Setup(x => x.GetStoreInventoryAsync(3, 2)).ReturnsAsync((StoreInventory?)null);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => validation.ValidateConfirmAsync(document));

        Assert.Equal("INGREDIENT_NOT_IN_STORE_INVENTORY", exception.Message);
        repository.Verify(
            x => x.GetOrCreateStoreInventoryForIngredientAsync(It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    private static AdminInventoryDocumentCreateService CreateService(
        Mock<IAdminInventoryDocumentRepository> repository)
    {
        var actorAccessor = new Mock<IAdminActorContextAccessor>();
        actorAccessor
            .Setup(x => x.Get(It.IsAny<ClaimsPrincipal>()))
            .Returns(new AdminActorContext { StaffId = 42, StoreId = 3 });
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(42, 3)).ReturnsAsync(true);

        return new AdminInventoryDocumentCreateService(
            repository.Object,
            Mock.Of<IAdminInventoryDocumentValidationService>(),
            Mock.Of<IAdminInventoryDocumentConfirmService>(),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Mock.Of<IRequestDeduplicationService>(),
            Mock.Of<IInventoryIssuePolicy>(),
            actorAccessor.Object,
            scope.Object);
    }

    private static StoreInventory Inventory(int storeId, int ingredientId, decimal quantity, bool active)
    {
        var ingredient = Ingredient(ingredientId, active);
        return new StoreInventory
        {
            StoreInventoryId = ingredientId,
            StoreId = storeId,
            IngredientId = ingredientId,
            Ingredient = ingredient,
            AvailableQty = quantity
        };
    }

    private static Ingredient Ingredient(int ingredientId, bool active)
    {
        var unit = BaseUnit();
        return new Ingredient
        {
            IngredientId = ingredientId,
            Code = $"NL{ingredientId}",
            Name = $"Nguyên liệu {ingredientId}",
            Active = active,
            BaseUnitId = unit.UnitId,
            BaseUnit = unit
        };
    }

    private static Unit BaseUnit() => new()
    {
        UnitId = 1,
        UnitCode = "ml",
        Name = "ml",
        Active = true
    };
}
