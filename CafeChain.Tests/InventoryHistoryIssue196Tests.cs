using System.Security.Claims;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.StoreInventories;
using CafeChain.Application.DTOs.Admin.StoreScope;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.StoreInventories;
using CafeChain.Application.Interfaces.Admin.StoreScope;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Infrastrusture.Repositories.Admin.StoreInventories;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.PreparedItems;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class InventoryHistoryIssue196Tests : IntegrationTestBase
{
    [Fact]
    public async Task InventoryHistory_LoadsForSelectedStore()
    {
        var service = new Mock<IAdminStoreInventoryService>();
        service.Setup(x => x.GetStoresByStaffAsync(1))
            .ReturnsAsync(new List<InventoryStoreDTO>
            {
                new() { StoreId = 1, StoreName = "CafeChain Thủ Dầu Một" }
            });
        service.Setup(x => x.GetAllTransactionsByStaffAsync(1, 1, 1, 10))
            .ReturnsAsync((new List<InventoryTransactionDTO>(), 0));

        var actor = new AdminActorContext { StaffId = 3, StoreId = 1 };
        var actorAccessor = new Mock<IAdminActorContextAccessor>();
        actorAccessor.Setup(x => x.Get(It.IsAny<ClaimsPrincipal>())).Returns(actor);
        var scope = new Mock<IAdminStoreScopeResolver>();
        scope.Setup(x => x.ResolveAsync(actor, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResolvedStore(1));

        var controller = Controller(service.Object, actorAccessor.Object, scope.Object);
        var result = await controller.Transactions(0, 1);

        var partial = Assert.IsType<PartialViewResult>(result);
        Assert.Equal("Partials/_TransactionPartial", partial.ViewName);
        service.Verify(x => x.GetAllTransactionsByStaffAsync(1, 1, 1, 10), Times.Once);
        scope.Verify(x => x.ResolveAsync(actor, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InventoryHistory_ZeroStoreIdUsesResolvedScopeNotFirstAccessibleStore()
    {
        var service = new Mock<IAdminStoreInventoryService>();
        service.Setup(x => x.GetStoresByStaffAsync(1))
            .ReturnsAsync(new List<InventoryStoreDTO>
            {
                new() { StoreId = 1, StoreName = "Store đầu tiên" },
                new() { StoreId = 2, StoreName = "Store đã resolve" }
            });
        service.Setup(x => x.GetAllTransactionsByStaffAsync(1, 2, 1, 10))
            .ReturnsAsync((new List<InventoryTransactionDTO>(), 0));

        var actor = new AdminActorContext { StaffId = 3, StoreId = 2 };
        var actorAccessor = new Mock<IAdminActorContextAccessor>();
        actorAccessor.Setup(x => x.Get(It.IsAny<ClaimsPrincipal>())).Returns(actor);
        var scope = new Mock<IAdminStoreScopeResolver>();
        scope.Setup(x => x.ResolveAsync(actor, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResolvedStore(2, 1, 2));

        var controller = Controller(service.Object, actorAccessor.Object, scope.Object);
        var result = await controller.Transactions(0, 1);

        Assert.IsType<PartialViewResult>(result);
        service.Verify(x => x.GetAllTransactionsByStaffAsync(1, 2, 1, 10), Times.Once);
        service.Verify(x => x.GetAllTransactionsByStaffAsync(1, 1, 1, 10), Times.Never);
    }

    [Fact]
    public void InventoryHistory_QueryKeyIncludesStoreId()
    {
        var source = ReadRepoFile("CafeChain", "wwwroot", "js", "Admin", "StoreInventory", "storeinventory.js");

        Assert.Contains("storeId: currentStoreId", source, StringComparison.Ordinal);
        Assert.Contains("page: currentTransactionPage", source, StringComparison.Ordinal);
        Assert.Contains("/Admin/AdminStoreInventory/Transactions?", source, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryHistory_StoreChangeResetsPagination()
    {
        var partial = ReadRepoFile("CafeChain", "Areas", "Admin", "Views", "AdminStoreInventory", "Partials", "_TransactionPartial.cshtml");
        var index = ReadRepoFile("CafeChain", "Areas", "Admin", "Views", "AdminStoreInventory", "Index.cshtml");
        var modal = ReadRepoFile("CafeChain", "Areas", "Admin", "Views", "AdminStoreInventory", "Partials", "_TransactionModalPartial.cshtml");

        Assert.Contains("loadTransactionPage(1, @store.StoreId)", partial, StringComparison.Ordinal);
        Assert.Contains("openTransactionModal(@Model.StoreId)", index, StringComparison.Ordinal);
        Assert.DoesNotContain("openTransactionModal(0)", index, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", modal, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InventoryHistory_NullOptionalFieldsDoNotCrash()
    {
        await SeedMixedHistoryAsync();
        using var context = CreateDbContext();
        var repository = new AdminStoreInventoryRepository(context);

        var (rows, _) = await repository.GetTransactionsByStoreIdsAsync(
            new List<int> { 1961 }, 1961, 1, 20);

        Assert.Contains(rows, x => x.UnitPrice == null
                                   && x.TotalAmount == null
                                   && x.ReferenceOrderId == null
                                   && x.ReferenceType == "Giao dịch kho");
    }

    [Fact]
    public void InventoryHistory_EmptyStateIsShown()
    {
        var partial = ReadRepoFile("CafeChain", "Areas", "Admin", "Views", "AdminStoreInventory", "Partials", "_TransactionPartial.cshtml");
        Assert.Contains("Chưa có giao dịch tồn kho trong khoảng thời gian đã chọn.", partial, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryHistory_ForbiddenStateIsShown()
    {
        var source = ReadRepoFile("CafeChain", "wwwroot", "js", "Admin", "StoreInventory", "storeinventory.js");
        Assert.Contains("response.status === 403", source, StringComparison.Ordinal);
        Assert.Contains("Bạn không có quyền xem lịch sử tồn kho của chi nhánh này.", source, StringComparison.Ordinal);
    }

    [Fact]
    public void InventoryHistory_RetryWorks()
    {
        var source = ReadRepoFile("CafeChain", "wwwroot", "js", "Admin", "StoreInventory", "storeinventory.js");
        Assert.Contains("Không thể tải lịch sử tồn kho. Vui lòng thử lại.", source, StringComparison.Ordinal);
        Assert.Contains("retry.textContent = \"Thử lại\"", source, StringComparison.Ordinal);
        Assert.Contains("loadTransactionPage(currentTransactionPage, currentStoreId)", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InventoryHistory_IngredientAndPreparedItemRowsRender()
    {
        await SeedMixedHistoryAsync();
        using var context = CreateDbContext();
        var repository = new AdminStoreInventoryRepository(context);

        var (rows, _) = await repository.GetTransactionsByStoreIdsAsync(
            new List<int> { 1961 }, 1961, 1, 20);

        Assert.Contains(rows, x => x.IngredientName == "Nguyên liệu lịch sử");
        Assert.Contains(rows, x => x.IngredientName == "BTP lịch sử" && x.IdentityBadge == "BTP");
        Assert.Contains(rows, x => x.IngredientName == "Công thức #1961" && x.IdentityBadge == "BTP legacy");
    }

    [Fact]
    public async Task InventoryHistory_ReturnsTransactionsForStore()
    {
        await SeedMixedHistoryAsync();
        using var context = CreateDbContext();
        var repository = new AdminStoreInventoryRepository(context);

        var (rows, total) = await repository.GetTransactionsByStoreIdsAsync(
            new List<int> { 1961 }, 1961, 1, 20);

        Assert.Equal(3, total);
        Assert.All(rows, x => Assert.Equal(1961, x.StoreId));
    }

    [Fact]
    public async Task InventoryHistory_DoesNotLeakOtherStoreData()
    {
        await SeedMixedHistoryAsync();
        using var context = CreateDbContext();
        var repository = new AdminStoreInventoryRepository(context);

        var (rows, total) = await repository.GetTransactionsByStoreIdsAsync(
            new List<int> { 1961 }, 1962, 1, 20);

        Assert.Empty(rows);
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task InventoryHistory_AreaScopeIsEnforced()
    {
        var service = new Mock<IAdminStoreInventoryService>();
        var actor = new AdminActorContext { StaffId = 3, StoreId = 1 };
        var actorAccessor = new Mock<IAdminActorContextAccessor>();
        actorAccessor.Setup(x => x.Get(It.IsAny<ClaimsPrincipal>())).Returns(actor);
        var scope = new Mock<IAdminStoreScopeResolver>();
        scope.Setup(x => x.ResolveAsync(actor, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminStoreScopeResolution
            {
                Status = AdminStoreScopeResolutionStatus.RequestedStoreForbidden,
                ErrorCode = AdminStoreScopeErrorCodes.StoreScopeForbidden,
                Message = "Bạn không có quyền truy cập cửa hàng đã chọn."
            });

        var controller = Controller(service.Object, actorAccessor.Object, scope.Object);
        var result = await controller.Transactions(2, 1);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, controller.Response.StatusCode);
        service.Verify(x => x.GetAllTransactionsByStaffAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task InventoryHistory_HandlesNullableReferences()
    {
        await InventoryHistory_NullOptionalFieldsDoNotCrash();
    }

    [Fact]
    public async Task InventoryHistory_UsesDeterministicPagination()
    {
        await SeedMixedHistoryAsync();
        using var context = CreateDbContext();
        var repository = new AdminStoreInventoryRepository(context);

        var (firstPage, _) = await repository.GetTransactionsByStoreIdsAsync(
            new List<int> { 1961 }, 1961, 1, 1);
        var (secondPage, _) = await repository.GetTransactionsByStoreIdsAsync(
            new List<int> { 1961 }, 1961, 2, 1);

        Assert.Equal(19613, Assert.Single(firstPage).InventoryTransactionId);
        Assert.Equal(19612, Assert.Single(secondPage).InventoryTransactionId);
    }

    [Fact]
    public async Task InventoryHistory_DateRangeIsAppliedCorrectly()
    {
        // The current MVC screen has no date picker, so its effective range is unbounded.
        // Old transactions must remain visible instead of being dropped by a hidden default.
        await SeedMixedHistoryAsync();
        using var context = CreateDbContext();
        var repository = new AdminStoreInventoryRepository(context);

        var (rows, _) = await repository.GetTransactionsByStoreIdsAsync(
            new List<int> { 1961 }, 1961, 1, 20);

        Assert.Contains(rows, x => x.CreatedAt.Year == 2025);
        Assert.Contains(rows, x => x.CreatedAt.Year == 2026);
    }

    [Fact]
    public async Task InventoryHistory_ReturnsLegacyAndPreparedItemTransactions()
    {
        await InventoryHistory_IngredientAndPreparedItemRowsRender();
    }

    private async Task SeedMixedHistoryAsync()
    {
        using var context = CreateDbContext();
        if (await context.Stores.AnyAsync(x => x.StoreId == 1961)) return;

        var unit = await context.Units.FirstOrDefaultAsync(x => x.UnitCode == "g");
        if (unit == null)
        {
            unit = new Unit
            {
                UnitCode = "g",
                Name = "Gram",
                Type = UnitType.KhoiLuong,
                Active = true
            };
            context.Units.Add(unit);
            await context.SaveChangesAsync();
        }

        var createdAt = new DateTime(2026, 7, 22, 1, 0, 0, DateTimeKind.Utc);
        context.Stores.AddRange(Store(1961, "Thủ Dầu Một test"), Store(1962, "Store ngoài scope"));
        context.Ingredients.Add(new Ingredient
        {
            IngredientId = 1961,
            Code = "ING-HISTORY",
            Name = "Nguyên liệu lịch sử",
            BaseUnitId = unit.UnitId,
            Active = true
        });
        context.PreparedItems.Add(new PreparedItem
        {
            PreparedItemId = 1961,
            Code = "BTP-HISTORY",
            Name = "BTP lịch sử",
            BaseUnitId = unit.UnitId,
            Active = true
        });
        context.Recipes.Add(new Recipe
        {
            RecipeId = 1961,
            RecipeCode = "RECIPE-HISTORY",
            Name = "Công thức legacy lịch sử",
            Active = true,
            Status = "Active",
            YieldPercentage = 100m
        });
        context.StoreInventories.AddRange(
            Inventory(19611, 1961, ingredientId: 1961),
            Inventory(19612, 1961, preparedItemId: 1961),
            Inventory(19613, 1961, recipeId: 1961),
            Inventory(19614, 1962, ingredientId: 1961));
        context.InventoryTransactions.AddRange(
            Transaction(19611, 19611, InventoryTransactionTypeEnum.IMPORT, createdAt.AddYears(-1)),
            Transaction(19612, 19612, InventoryTransactionTypeEnum.PRODUCTION_IN, createdAt),
            Transaction(19613, 19613, InventoryTransactionTypeEnum.IN_TRANSFER, createdAt),
            Transaction(19614, 19614, InventoryTransactionTypeEnum.ADJUSTMENT_IN, createdAt));
        await context.SaveChangesAsync();
    }

    private static Store Store(int id, string name) => new()
    {
        StoreId = id,
        Name = name,
        Address = "Test",
        Phone = id.ToString(),
        Active = true,
        CreatedAt = DateTime.UtcNow
    };

    private static StoreInventory Inventory(
        int id,
        int storeId,
        int? ingredientId = null,
        int? preparedItemId = null,
        int? recipeId = null) => new()
    {
        StoreInventoryId = id,
        StoreId = storeId,
        IngredientId = ingredientId,
        PreparedItemId = preparedItemId,
        RecipeId = recipeId,
        AvailableQty = 10m,
        ReservedQty = 0m,
        LastUpdated = DateTime.UtcNow,
        RowVersion = new byte[] { 0 }
    };

    private static InventoryTransaction Transaction(
        int id,
        int inventoryId,
        InventoryTransactionTypeEnum type,
        DateTime createdAt) => new()
    {
        InventoryTransactionId = id,
        StoreInventoryId = inventoryId,
        Type = type,
        StockStatus = InventoryStockStatus.NORMAL,
        Quantity = 1m,
        BeforeQty = 0m,
        AfterQty = 1m,
        UnitCost = null,
        TotalCost = null,
        CreatedAt = createdAt
    };

    private static AdminStoreInventoryController Controller(
        IAdminStoreInventoryService service,
        IAdminActorContextAccessor actor,
        IAdminStoreScopeResolver scope)
    {
        var controller = new AdminStoreInventoryController(service, actor, scope);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "1")
                }, "Test"))
            }
        };
        return controller;
    }

    private static AdminStoreScopeResolution ResolvedStore(
        int storeId,
        params int[] accessibleStoreIds) => new()
    {
        Status = AdminStoreScopeResolutionStatus.Resolved,
        StoreId = storeId,
        Source = AdminStoreScopeResolutionSource.SelectedSessionStore,
        AccessibleStores = (accessibleStoreIds.Length == 0
                ? new[] { storeId }
                : accessibleStoreIds)
            .Select(id => new AdminStoreOptionDto { StoreId = id, StoreName = "Store " + id })
            .ToArray()
    };

    private static string ReadRepoFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "CafeChain")))
        {
            directory = directory.Parent;
        }

        if (directory == null)
            throw new DirectoryNotFoundException("Không tìm thấy repo root.");

        return File.ReadAllText(Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray()));
    }
}
