using System.Security.Claims;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Services.Admin.InventoryDocuments;
using CafeChain.Infrastrusture.Interfaces.Admin.InventoryDocuments;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Approvals;
using CafeChain.Models.Inventories.Documents;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Http;
using Moq;

namespace CafeChain.Tests;

public sealed class InventoryDocumentApprovalVisibilityTests
{
    [Theory]
    [InlineData(RoleConstants.BusinessOwner, 99, InventoryNegativeApprovalStatuses.Requested, InventoryDocumentStatus.PENDING, true, null)]
    [InlineData(RoleConstants.BusinessOwner, 42, InventoryNegativeApprovalStatuses.Requested, InventoryDocumentStatus.PENDING, false, "không thể tự duyệt")]
    [InlineData(RoleConstants.StoreManager, 99, InventoryNegativeApprovalStatuses.Requested, InventoryDocumentStatus.PENDING, false, "không có quyền duyệt")]
    [InlineData(RoleConstants.BusinessOwner, 99, InventoryNegativeApprovalStatuses.Approved, InventoryDocumentStatus.CONFIRMED, false, "đã được duyệt")]
    public async Task Detail_ExplainsWhetherCurrentActorCanReview(
        string role,
        int actorStaffId,
        string approvalStatus,
        InventoryDocumentStatus documentStatus,
        bool expectedCanReview,
        string? expectedMessageFragment)
    {
        var repository = new Mock<IAdminInventoryDocumentRepository>();
        repository.Setup(x => x.GetDocumentWithDetailsAsync(17))
            .ReturnsAsync(Document(approvalStatus, documentStatus));
        repository.Setup(x => x.GetNegativeCostGapsByDocumentAsync(17))
            .ReturnsAsync([]);

        var actorAccessor = new Mock<IAdminActorContextAccessor>();
        actorAccessor.Setup(x => x.Get(It.IsAny<ClaimsPrincipal>()))
            .Returns(new AdminActorContext
            {
                StaffId = actorStaffId,
                StoreId = 3,
                RoleNames = [role]
            });
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(actorStaffId, 3)).ReturnsAsync(true);
        var service = new AdminInventoryDocumentService(
            repository.Object,
            Mock.Of<IAdminInventoryDocumentSnapshotService>(),
            Mock.Of<IAdminInventoryDocumentExportService>(),
            actorAccessor.Object,
            scope.Object,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        var result = await service.GetDetailAsync(17);

        Assert.NotNull(result);
        Assert.Equal(expectedCanReview, result!.CanReviewNegativeApproval);
        if (expectedMessageFragment == null)
        {
            Assert.Null(result.NegativeApprovalReviewMessage);
        }
        else
        {
            Assert.Contains(expectedMessageFragment, result.NegativeApprovalReviewMessage, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Detail_OutsideStoreScope_IsNotDisclosed()
    {
        var repository = new Mock<IAdminInventoryDocumentRepository>();
        repository.Setup(x => x.GetDocumentWithDetailsAsync(17))
            .ReturnsAsync(Document(InventoryNegativeApprovalStatuses.Requested, InventoryDocumentStatus.PENDING));
        var actorAccessor = new Mock<IAdminActorContextAccessor>();
        actorAccessor.Setup(x => x.Get(It.IsAny<ClaimsPrincipal>()))
            .Returns(new AdminActorContext
            {
                StaffId = 99,
                RoleNames = [RoleConstants.BusinessOwner]
            });
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(99, 3)).ReturnsAsync(false);
        var service = new AdminInventoryDocumentService(
            repository.Object,
            Mock.Of<IAdminInventoryDocumentSnapshotService>(),
            Mock.Of<IAdminInventoryDocumentExportService>(),
            actorAccessor.Object,
            scope.Object,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });

        Assert.Null(await service.GetDetailAsync(17));
    }

    private static InventoryDocument Document(string approvalStatus, InventoryDocumentStatus documentStatus) => new()
    {
        InventoryDocumentId = 17,
        StoreId = 3,
        Store = new Store { StoreId = 3, Name = "CafeChain Dĩ An", Active = true },
        Code = "PX-17",
        Status = documentStatus,
        Type = InventoryDocumentType.EXPORT,
        Purpose = InventoryDocumentPurpose.SALE,
        DocumentDate = DateTime.Today,
        NegativeApproval = new InventoryNegativeApproval
        {
            InventoryNegativeApprovalId = 7,
            InventoryDocumentId = 17,
            StoreId = 3,
            RequesterStaffId = 42,
            Status = approvalStatus,
            Reason = "Đơn hàng đã giao",
            PolicyVersion = "manual-export-v1",
            RequestedAt = DateTime.UtcNow,
            Lines = []
        },
        Details = []
    };
}
