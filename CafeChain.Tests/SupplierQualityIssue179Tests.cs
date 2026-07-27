using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Procurement;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class SupplierQualityIssue179Tests : IntegrationTestBase
{
    private const int StoreId = 1790;
    private const int OtherStoreId = 1791;
    private const int StaffId = 17902;
    private const int UnitId = 17903;
    private const int IngredientId = 17904;
    private const int SupplierId = 17905;
    private static readonly string[] Roles = [RoleConstants.StoreManager];

    [Fact]
    public async Task ConfirmedReceipt_CreatesAuditedIssue_WithoutInventoryMutation()
    {
        using var context = CreateDbContext();
        await SeedFoundationAsync(context);
        var line = await SeedDeliveryAsync(context, "PO-179-A", 10m, 8m, 2m,
            DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1), BranchReceiptStatuses.Confirmed);
        var inventoryTransactionsBefore = await context.InventoryTransactions.CountAsync();
        var costLayersBefore = await context.InventoryCostLayers.CountAsync();

        var result = await CreateService(context).CreateIssueAsync(new CreateSupplierReceiptIssueRequest
        {
            BranchReceiptLineId = line.BranchReceiptLineId,
            IssueType = SupplierReceiptIssueTypes.QualityFailure,
            AffectedBaseQuantity = 2m,
            Description = "Hai kilogram không đạt chất lượng khi nhận."
        }, StaffId, Roles);

        Assert.True(result.IsSuccess, result.Message);
        var issue = await context.SupplierReceiptIssues.Include(x => x.Transitions).SingleAsync();
        Assert.Equal(SupplierReceiptIssueStatuses.Open, issue.Status);
        Assert.Equal(line.BranchReceiptId, issue.BranchReceiptId);
        Assert.Equal(line.PurchaseOrderLineId, issue.PurchaseOrderLineId);
        Assert.Single(issue.Transitions);
        Assert.Equal(inventoryTransactionsBefore, await context.InventoryTransactions.CountAsync());
        Assert.Equal(costLayersBefore, await context.InventoryCostLayers.CountAsync());
    }

    [Fact]
    public async Task SameActiveIssueTypeForReceiptLine_IsRejected()
    {
        using var context = CreateDbContext();
        await SeedFoundationAsync(context);
        var line = await SeedDeliveryAsync(context, "PO-179-DUP", 10m, 8m, 2m,
            DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1), BranchReceiptStatuses.Confirmed);
        var service = CreateService(context);
        var input = new CreateSupplierReceiptIssueRequest
        {
            BranchReceiptLineId = line.BranchReceiptLineId,
            IssueType = SupplierReceiptIssueTypes.QualityFailure,
            AffectedBaseQuantity = 2m,
            Description = "Không đạt chất lượng."
        };

        var first = await service.CreateIssueAsync(input, StaffId, Roles);
        var duplicate = await service.CreateIssueAsync(input, StaffId, Roles);

        Assert.True(first.IsSuccess, first.Message);
        Assert.False(duplicate.IsSuccess);
        Assert.Equal("SUPPLIER_ISSUE_ACTIVE_DUPLICATE", duplicate.ErrorCode);
        Assert.Single(await context.SupplierReceiptIssues.ToListAsync());
    }

    [Fact]
    public async Task DraftReceipt_AndCrossStoreAccess_AreRejected()
    {
        using var context = CreateDbContext();
        await SeedFoundationAsync(context);
        var draftLine = await SeedDeliveryAsync(context, "PO-179-DRAFT", 10m, 10m, 0m,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, BranchReceiptStatuses.Draft);
        var service = CreateService(context);

        var draft = await service.CreateIssueAsync(new CreateSupplierReceiptIssueRequest
        {
            BranchReceiptLineId = draftLine.BranchReceiptLineId,
            IssueType = SupplierReceiptIssueTypes.DocumentMismatch,
            Description = "Chứng từ chưa khớp."
        }, StaffId, Roles);
        var crossStore = await service.GetDashboardAsync(
            OtherStoreId, SupplierId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(1), StaffId, Roles);

        Assert.False(draft.IsSuccess);
        Assert.Contains("đã xác nhận", draft.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(crossStore.IsSuccess);
        Assert.Contains("không có quyền", crossStore.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(context.SupplierReceiptIssues);
    }

    [Fact]
    public async Task Dismiss_RequiresReason_AndWritesTransitionAudit()
    {
        using var context = CreateDbContext();
        await SeedFoundationAsync(context);
        var line = await SeedDeliveryAsync(context, "PO-179-DISMISS", 10m, 10m, 0m,
            DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1), BranchReceiptStatuses.Confirmed);
        var service = CreateService(context);
        var created = await service.CreateIssueAsync(new CreateSupplierReceiptIssueRequest
        {
            BranchReceiptLineId = line.BranchReceiptLineId,
            IssueType = SupplierReceiptIssueTypes.LateDelivery,
            Description = "Hệ thống gợi ý giao trễ, cần người dùng xác nhận."
        }, StaffId, Roles);

        var missingReason = await service.TransitionAsync(created.Data!.SupplierReceiptIssueId,
            new SupplierReceiptIssueTransitionRequest
            {
                TargetStatus = SupplierReceiptIssueStatuses.Dismissed,
                RowVersion = created.Data.RowVersion
            }, StaffId, Roles);
        var dismissed = await service.TransitionAsync(created.Data.SupplierReceiptIssueId,
            new SupplierReceiptIssueTransitionRequest
            {
                TargetStatus = SupplierReceiptIssueStatuses.Dismissed,
                Reason = "Đã đối chiếu biên bản, thời gian giao đúng cam kết điều chỉnh.",
                RowVersion = created.Data.RowVersion
            }, StaffId, Roles);

        Assert.False(missingReason.IsSuccess);
        Assert.True(dismissed.IsSuccess, dismissed.Message);
        var issue = await context.SupplierReceiptIssues.AsNoTracking().SingleAsync();
        Assert.Equal(SupplierReceiptIssueStatuses.Dismissed, issue.Status);
        Assert.NotNull(issue.DismissedAtUtc);
        Assert.False(string.IsNullOrWhiteSpace(issue.DismissReason));
        Assert.Equal(2, await context.SupplierReceiptIssueTransitions.CountAsync());
    }

    [Fact]
    public async Task Transition_MissingOrStaleRowVersionRejected()
    {
        using var context = CreateDbContext();
        await SeedFoundationAsync(context);
        var line = await SeedDeliveryAsync(context, "PO-179-VERSION", 10m, 10m, 0m,
            DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1), BranchReceiptStatuses.Confirmed);
        var service = CreateService(context);
        var created = await CreateIssueAsync(service, line.BranchReceiptLineId, SupplierReceiptIssueTypes.Other);

        var missing = await service.TransitionAsync(created.SupplierReceiptIssueId,
            new SupplierReceiptIssueTransitionRequest
            {
                TargetStatus = SupplierReceiptIssueStatuses.UnderReview,
                Reason = "Kiểm tra",
                RowVersion = string.Empty
            }, StaffId, Roles);
        var stale = await service.TransitionAsync(created.SupplierReceiptIssueId,
            new SupplierReceiptIssueTransitionRequest
            {
                TargetStatus = SupplierReceiptIssueStatuses.UnderReview,
                Reason = "Kiểm tra",
                RowVersion = Convert.ToBase64String(new byte[] { 9 })
            }, StaffId, Roles);

        Assert.False(missing.IsSuccess);
        Assert.Equal(BranchReceiptErrorCodes.ValidationRowVersionRequired, missing.ErrorCode);
        Assert.False(stale.IsSuccess);
        Assert.Equal(BranchReceiptErrorCodes.ResourceChanged, stale.ErrorCode);
    }

    [Fact]
    public async Task Performance_UsesConfirmedEvidence_AndCountsEachIssueReceiptOnce()
    {
        using var context = CreateDbContext();
        await SeedFoundationAsync(context);
        var now = DateTime.UtcNow;
        var first = await SeedDeliveryAsync(context, "PO-179-1", 10m, 10m, 0m,
            now.AddDays(-10), now.AddDays(-10), BranchReceiptStatuses.Confirmed);
        var second = await SeedDeliveryAsync(context, "PO-179-2", 10m, 9m, 1m,
            now.AddDays(-9), now.AddDays(-7), BranchReceiptStatuses.Confirmed);
        await SeedDeliveryAsync(context, "PO-179-3", 10m, 8m, 2m,
            now.AddDays(-8), now.AddDays(-8), BranchReceiptStatuses.Confirmed);
        await SeedDeliveryAsync(context, "PO-179-DRAFT-METRIC", 100m, 0m, 100m,
            now.AddDays(-6), now.AddDays(-1), BranchReceiptStatuses.Draft);
        var service = CreateService(context);

        await CreateIssueAsync(service, first.BranchReceiptLineId, SupplierReceiptIssueTypes.Damaged);
        await CreateIssueAsync(service, first.BranchReceiptLineId, SupplierReceiptIssueTypes.PackagingFailure);
        var dismissedIssue = await CreateIssueAsync(
            service, second.BranchReceiptLineId, SupplierReceiptIssueTypes.ShortDelivery);
        var dismissed = await service.TransitionAsync(dismissedIssue.SupplierReceiptIssueId,
            new SupplierReceiptIssueTransitionRequest
            {
                TargetStatus = SupplierReceiptIssueStatuses.Dismissed,
                Reason = "Nhà cung cấp giao bù trong cùng lần nhận, không ghi nhận sự cố.",
                RowVersion = dismissedIssue.RowVersion
            }, StaffId, Roles);
        Assert.True(dismissed.IsSuccess, dismissed.Message);

        var dashboard = await service.GetDashboardAsync(
            StoreId, SupplierId, now.AddDays(-30), now.AddDays(1), StaffId, Roles);

        Assert.True(dashboard.IsSuccess, dashboard.Message);
        var performance = Assert.IsType<SupplierPerformanceDto>(dashboard.Data!.Performance);
        Assert.Equal(3, performance.CompletedDeliveryCount);
        Assert.Equal(3, performance.ConfirmedReceiptCount);
        Assert.Equal(66.67m, performance.OnTimeRate);
        Assert.Equal(90m, performance.FillRate);
        Assert.Equal(10m, performance.RejectionRate);
        Assert.Equal(33.33m, performance.IssueRate);
        Assert.Equal(0.67m, performance.AverageDelayDays);
        Assert.Equal(SupplierPerformanceStatuses.Risk, performance.Status);
    }

    [Fact]
    public async Task Performance_WithFewerThanThreeDeliveries_IsInsufficient()
    {
        using var context = CreateDbContext();
        await SeedFoundationAsync(context);
        var now = DateTime.UtcNow;
        await SeedDeliveryAsync(context, "PO-179-SMALL-1", 10m, 10m, 0m,
            now.AddDays(-2), now.AddDays(-2), BranchReceiptStatuses.Confirmed);
        await SeedDeliveryAsync(context, "PO-179-SMALL-2", 10m, 10m, 0m,
            now.AddDays(-1), now.AddDays(-1), BranchReceiptStatuses.Confirmed);

        var dashboard = await CreateService(context).GetDashboardAsync(
            StoreId, SupplierId, now.AddDays(-30), now.AddDays(1), StaffId, Roles);

        Assert.True(dashboard.IsSuccess, dashboard.Message);
        Assert.Equal(SupplierPerformanceStatuses.InsufficientData, dashboard.Data!.Performance!.Status);
    }

    [Fact]
    public async Task Dashboard_RangeOver366DaysRejected()
    {
        using var context = CreateDbContext();
        await SeedFoundationAsync(context);

        var result = await CreateService(context).GetDashboardAsync(
            StoreId,
            SupplierId,
            DateTime.UtcNow.AddDays(-367),
            DateTime.UtcNow,
            StaffId,
            Roles);

        Assert.False(result.IsSuccess);
        Assert.Contains("khoảng thời gian", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<SupplierReceiptIssueListItemDto> CreateIssueAsync(
        SupplierQualityService service,
        int lineId,
        string issueType)
    {
        var result = await service.CreateIssueAsync(new CreateSupplierReceiptIssueRequest
        {
            BranchReceiptLineId = lineId,
            IssueType = issueType,
            AffectedBaseQuantity = 1m,
            Description = "Sự cố dùng để kiểm chứng chỉ số nhà cung cấp."
        }, StaffId, Roles);
        Assert.True(result.IsSuccess, result.Message);
        return result.Data!;
    }

    private static SupplierQualityService CreateService(AppDbContext context)
    {
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), StoreId)).ReturnsAsync(true);
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), OtherStoreId)).ReturnsAsync(false);
        return new SupplierQualityService(context, scope.Object);
    }

    private static async Task SeedFoundationAsync(AppDbContext context)
    {
        var now = DateTime.UtcNow;
        context.Stores.AddRange(
            new Store { StoreId = StoreId, Name = "Store #179", Address = "Test", Phone = "0900179000", Active = true, CreatedAt = now },
            new Store { StoreId = OtherStoreId, Name = "Other Store #179", Address = "Test", Phone = "0900179100", Active = true, CreatedAt = now });
        context.Accounts.Add(new Account { AccountId = StaffId, Email = "quality179@test.local", PasswordHash = "x", Active = true, CreatedAt = now });
        context.Staffs.Add(new Staff { StaffId = StaffId, AccountId = StaffId, StoreId = StoreId, FullName = "Quality Manager #179", Active = true, CreatedAt = now});
        context.Units.Add(new Unit { UnitId = UnitId, UnitCode = "kg179", Name = "Kilogram #179", Active = true });
        context.Ingredients.Add(new Ingredient { IngredientId = IngredientId, Code = "ING-179", Name = "Ingredient #179", BaseUnitId = UnitId, Active = true });
        context.Suppliers.Add(new Supplier { SupplierId = SupplierId, Code = "SUP-179", Name = "Supplier #179", Active = true, CreatedAt = now, UpdatedAt = now });
        await context.SaveChangesAsync();
        context.IngredientSuppliers.Add(new IngredientSupplier
        {
            IngredientId = IngredientId,
            SupplierId = SupplierId,
            UnitId = UnitId,
            PackageQuantity = 1m,
            CurrentPrice = 10m,
            MinimumOrderPackageCount = 1,
            LeadTimeDays = 1,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now
        });
        await context.SaveChangesAsync();
    }

    private static async Task<BranchReceiptLine> SeedDeliveryAsync(
        AppDbContext context,
        string code,
        decimal ordered,
        decimal accepted,
        decimal rejected,
        DateTime expectedAt,
        DateTime receivedAt,
        string receiptStatus)
    {
        var now = DateTime.UtcNow;
        var offer = await context.IngredientSuppliers.SingleAsync(
            x => x.IngredientId == IngredientId && x.SupplierId == SupplierId);

        var order = new PurchaseOrder
        {
            Code = code,
            StoreId = StoreId,
            SupplierId = SupplierId,
            Status = PurchaseOrderStatuses.Completed,
            OrderDate = expectedAt.AddDays(-1),
            ExpectedDeliveryAtUtc = expectedAt,
            CreatedByStaffId = StaffId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CompletedAtUtc = receivedAt
        };
        var orderLine = new PurchaseOrderLine
        {
            IngredientId = IngredientId,
            IngredientSupplierId = offer.IngredientSupplierId,
            PackageUnitIdSnapshot = UnitId,
            PackageQuantitySnapshot = 1m,
            PackagePriceSnapshot = 10m,
            PackageCount = ordered,
            OrderedPackageCount = ordered,
            UnitPricePerPackage = 10m,
            OrderedBaseQuantity = ordered,
            PromisedLeadTimeDaysSnapshot = 1
        };
        order.Lines.Add(orderLine);
        context.PurchaseOrders.Add(order);
        await context.SaveChangesAsync();

        var receipt = new BranchReceipt
        {
            ReceiptCode = "BR-" + code,
            ReceiptKey = "BR-" + code,
            StoreId = StoreId,
            SupplierId = SupplierId,
            Status = receiptStatus,
            ReceivedAt = receivedAt,
            ReceivedByStaffId = StaffId,
            ConfirmedAt = receiptStatus == BranchReceiptStatuses.Confirmed ? receivedAt : null,
            ConfirmedByStaffId = receiptStatus == BranchReceiptStatuses.Confirmed ? StaffId : null,
            CreatedAt = now,
            CreatedByStaffId = StaffId
        };
        var receiptLine = new BranchReceiptLine
        {
            PurchaseOrderLineId = orderLine.PurchaseOrderLineId,
            IngredientId = IngredientId,
            InputQuantity = accepted + rejected,
            InputUnitId = UnitId,
            ReceivedBaseQuantity = accepted,
            RejectedBaseQuantity = rejected,
            BaseUnitId = UnitId,
            BaseUnitCostSnapshot = 10m,
            LineTotalCost = accepted * 10m,
            CreatedAt = now
        };
        receipt.Lines.Add(receiptLine);
        context.BranchReceipts.Add(receipt);
        await context.SaveChangesAsync();

        if (receiptStatus == BranchReceiptStatuses.Confirmed)
        {
            context.PurchaseOrderReceiptPostings.Add(new PurchaseOrderReceiptPosting
            {
                PurchaseOrderLineId = orderLine.PurchaseOrderLineId,
                BranchReceiptLineId = receiptLine.BranchReceiptLineId,
                AcceptedBaseQuantity = accepted,
                RejectedBaseQuantity = rejected,
                CreatedByStaffId = StaffId,
                CreatedAtUtc = receivedAt
            });
            await context.SaveChangesAsync();
        }

        return receiptLine;
    }
}
