using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Infrastructure.Interfaces.Operations;
using CafeChain.Models.Operations;
using Moq;

namespace CafeChain.Tests;

public sealed class InventoryReorderNotificationServiceTests
{
    [Fact]
    public async Task Refresh_twice_does_not_duplicate_same_reorder_alert()
    {
        var suggestion = new Mock<IReorderSuggestionService>();
        suggestion.Setup(x => x.CalculateForStoreAsync(
                1,
                30,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<ReorderSuggestionListDto>.Success(new ReorderSuggestionListDto
            {
                StoreId = 1,
                StoreName = "Store 1",
                Items =
                [
                    new ReorderSuggestionItemDto
                    {
                        StoreId = 1,
                        IngredientId = 20,
                        IngredientName = "Sữa tươi",
                        BaseUnitCode = "L",
                        Status = ReorderSuggestionStatuses.Ready,
                        RecommendationLevel = ReorderRecommendationLevels.Urgent,
                        UsableQuantity = 3,
                        ProjectedQuantity = 3,
                        SuggestedBaseQuantity = 7
                    }
                ]
            }));
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(10, 1)).ReturnsAsync(true);
        var permissions = new Mock<IAdminPermissionService>();
        permissions.Setup(x => x.HasPermissionAsync(100, PermissionConstants.AppAdminDashboard, 1))
            .ReturnsAsync(ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto { Allowed = true }));
        var repository = new FakeRepository();
        var service = new InventoryReorderNotificationService(
            suggestion.Object, repository, scope.Object, permissions.Object);

        var first = await service.RefreshStoreAsync(1, 30);
        var second = await service.RefreshStoreAsync(1, 30);

        Assert.Equal(1, first.Created);
        Assert.Equal(0, second.Created);
        Assert.Single(repository.Rows);
        Assert.Equal("CRITICAL", repository.Rows[0].Severity);
    }

    private sealed class FakeRepository : IInventoryReorderNotificationRepository
    {
        public List<StaffNotification> Rows { get; } = [];

        public Task<IReadOnlyList<ReorderNotificationRecipientRow>> GetRecipientCandidatesAsync() =>
            Task.FromResult<IReadOnlyList<ReorderNotificationRecipientRow>>(
                [new ReorderNotificationRecipientRow(10, 100, [RoleConstants.BusinessOwner])]);

        public Task<StaffNotification?> GetByDeduplicationKeyAsync(string key) =>
            Task.FromResult(Rows.FirstOrDefault(x => x.DeduplicationKey == key));

        public Task<List<StaffNotification>> GetActiveForStoreAsync(int storeId) =>
            Task.FromResult(Rows.Where(x => x.StoreId == storeId && x.ResolvedAt == null).ToList());

        public void Add(StaffNotification notification) => Rows.Add(notification);

        public Task SaveChangesAsync() => Task.CompletedTask;
    }
}
