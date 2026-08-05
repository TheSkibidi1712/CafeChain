using CafeChain.Application.Constants;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Inventories.Ice;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Orders;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class OperationalIceReportEndToEndIssue250Tests : IntegrationTestBase
{
    private const int StoreId = 25001;
    private const int IngredientId = 25002;
    private const int UnitId = 25003;
    private const int ManagerStaffId = 25004;
    private const int ReceiverStaffId = 25005;

    [Fact]
    public async Task TheoreticalUsage_UsesNetInventoryImpactingSales()
    {
        using var context = CreateDbContext();
        var setup = SeedClosedAllocation(context, salesCostComplete: true);
        await context.SaveChangesAsync();
        var report = await CreateReportService(context).BuildAsync(setup.AllocationId);

        Assert.True(report.IsSuccess, report.Message);
        Assert.Equal(7m, report.Data.LedgerTheoreticalUsage);
        Assert.Equal(7m, report.Data.TheoreticalUsage);
        Assert.False(report.Data.HasUsageSnapshotMismatch);
    }

    [Fact]
    public async Task CancelledOrder_ReversesIceConsumptionByExistingContract()
    {
        using var context = CreateDbContext();
        var setup = SeedClosedAllocation(context, salesCostComplete: true);
        await context.SaveChangesAsync();

        var report = await CreateReportService(context).BuildAsync(setup.AllocationId);

        var deduction = await context.InventoryTransactions.SingleAsync(x => x.Type == InventoryTransactionTypeEnum.SALES_DEDUCTION);
        var salesReturn = await context.InventoryTransactions.SingleAsync(x => x.Type == InventoryTransactionTypeEnum.SALES_RETURN);
        Assert.Equal(8m, deduction.Quantity);
        Assert.Equal(1m, salesReturn.Quantity);
        Assert.Equal(7m, report.Data.LedgerTheoreticalUsage);
    }

    [Fact]
    public async Task IceReport_UsesLedgerAndPostingCostAuthority()
    {
        using var context = CreateDbContext();
        var setup = SeedClosedAllocation(context, salesCostComplete: true);
        await context.SaveChangesAsync();

        var result = await CreateReportService(context).BuildAsync(setup.AllocationId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(14_000m, result.Data.TheoreticalCost);
        Assert.Equal(2_500m, result.Data.VarianceCost);
        Assert.Equal(16_500m, result.Data.ActualCost);
        Assert.Equal("Đủ dữ liệu giá vốn theo phiếu xuất kho", result.Data.CostStatus);
        var posting = Assert.Single(result.Data.InventoryPostings);
        Assert.Equal(setup.VarianceTransactionId, posting.InventoryTransactionId);
        Assert.Equal("IceVariancePosting:250:2", posting.IdempotencyKey);
    }

    [Fact]
    public async Task IceReport_MissingLedgerCost_DoesNotInventSeedCost()
    {
        using var context = CreateDbContext();
        var setup = SeedClosedAllocation(context, salesCostComplete: false);
        await context.SaveChangesAsync();

        var result = await CreateReportService(context).BuildAsync(setup.AllocationId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Null(result.Data.TheoreticalCost);
        Assert.Null(result.Data.ActualCost);
        Assert.Contains("Thiếu dữ liệu giá vốn theo phiếu xuất kho", result.Data.CostStatus);
    }

    [Fact]
    public async Task IceReport_PdfContainsOperationalAuditEvidence()
    {
        using var context = CreateDbContext();
        var setup = SeedClosedAllocation(context, salesCostComplete: true);
        await context.SaveChangesAsync();
        var result = await CreateReportService(context).BuildAsync(setup.AllocationId);
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var pdf = new OperationalIceReportPdfRenderer().Render(result.Data, DateTime.UtcNow);

        Assert.True(pdf.Length > 1_000);
        Assert.Equal((byte)'%', pdf[0]);
        Assert.Equal((byte)'P', pdf[1]);
        Assert.Equal((byte)'D', pdf[2]);
        Assert.Equal((byte)'F', pdf[3]);
    }

    [Fact]
    public void IceReport_UiContainsRequiredAuditFieldsAndResponsivePrintRules()
    {
        var view = Read("CafeChain", "Areas", "Admin", "Views", "AdminOperationalIce", "Report.cshtml");
        var css = Read("CafeChain", "wwwroot", "css", "Admin", "OperationalIce", "operational-ice.css");

        Assert.Contains("asp-action=\"DownloadReport\"", view);
        Assert.Contains("Tồn chuyển đầu ca", view);
        Assert.Contains("Cấp đầu ca", view);
        Assert.Contains("Cấp bổ sung", view);
        Assert.Contains("Tồn chuyển cuối ca", view);
        Assert.Contains("Tiêu hao thực tế", view);
        Assert.Contains("Dùng theo POS", view);
        Assert.Contains("Chênh lệch", view);
        Assert.Contains("Giá vốn theo POS", view);
        Assert.Contains("Giá vốn chênh lệch", view);
        Assert.Contains("Giá vốn thực tế", view);
        Assert.Contains("Người giao / nhận / duyệt", view);
        Assert.Contains("Tham chiếu bút toán kho", view);
        Assert.Contains("Ca bán hàng POS", view);
        Assert.Contains("@media (max-width: 640px)", css);
        Assert.Contains("@media print", css);
    }

    [Fact]
    public void IceIngredient_RemainsInBom()
    {
        using var context = CreateDbContext();
        context.Units.Add(new Unit { UnitId = UnitId, UnitCode = "G", Name = "Gram", Active = true });
        context.Ingredients.Add(new Ingredient
        {
            IngredientId = IngredientId,
            Code = "ICE_BOM",
            Name = "Đá viên",
            BaseUnitId = UnitId,
            Active = true
        });
        var recipe = new Recipe
        {
            RecipeCode = "BOM_ICE_TEST",
            Name = "Công thức có đá",
            Active = true,
            Status = "Active",
            EffectiveDate = DateTime.UtcNow.Date
        };
        context.Recipes.Add(recipe);
        context.SaveChanges();
        context.RecipeDetails.Add(new RecipeDetail
        {
            RecipeId = recipe.RecipeId,
            IngredientId = IngredientId,
            Quantity = 120m,
            UnitId = UnitId
        });
        context.SaveChanges();

        var line = context.RecipeDetails.Include(x => x.Ingredient)
            .Single(x => x.RecipeId == recipe.RecipeId && x.IngredientId == IngredientId);
        Assert.Equal(120m, line.Quantity);
        Assert.Equal("Đá viên", line.Ingredient.Name);
    }

    [Fact]
    public async Task StoreUsingIce_RequiresInventoryRecord()
    {
        using var context = CreateDbContext();
        SeedStorePolicyAndShiftWithoutInventory(context);
        await context.SaveChangesAsync();
        var scope = new Mock<CafeChain.Application.Interfaces.Security.IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), StoreId)).ReturnsAsync(true);
        var service = new OperationalIceService(context, scope.Object);

        var result = await service.OpenAllocationAsync(
            new CafeChain.Application.DTOs.Inventories.OpenIceAllocationRequest
            {
                OperationalShiftId = 250,
                InitialIssuedQuantity = 1m
            },
            new CafeChain.Application.DTOs.Admin.Actor.AdminActorContext
            {
                StaffId = ManagerStaffId,
                StoreId = StoreId,
                RoleNames = [RoleConstants.StoreManager]
            });

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationalIceErrorCodes.NotFound, result.ErrorCode);
        Assert.Contains("chưa có tồn kho", result.Message);
    }

    private static OperationalIceReportService CreateReportService(CafeChain.Data.AppDbContext context)
    {
        var conversion = new Mock<IUnitConversionService>();
        conversion.Setup(x => x.ConvertAsync(IngredientId, 1m, UnitId, null))
            .ReturnsAsync(ServiceResult<decimal>.Success(1m));
        return new OperationalIceReportService(context, conversion.Object);
    }

    private static (int AllocationId, int VarianceTransactionId) SeedClosedAllocation(
        CafeChain.Data.AppDbContext context,
        bool salesCostComplete)
    {
        context.Stores.Add(new Store
        {
            StoreId = StoreId,
            Name = "CafeChain Report Test",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
        context.Units.Add(new Unit { UnitId = UnitId, UnitCode = "G", Name = "g", Active = true });
        context.Ingredients.Add(new Ingredient
        {
            IngredientId = IngredientId,
            Code = "ICE_REPORT",
            Name = "Đá viên",
            BaseUnitId = UnitId,
            Active = true
        });
        context.Staffs.AddRange(
            new Staff { StaffId = ManagerStaffId, AccountId = 25104, StoreId = StoreId, FullName = "Quản lý báo cáo", Active = true, CreatedAt = DateTime.UtcNow },
            new Staff { StaffId = ReceiverStaffId, AccountId = 25105, StoreId = StoreId, FullName = "Người nhận đá", Active = true, CreatedAt = DateTime.UtcNow });
        var inventory = new StoreInventory
        {
            StoreId = StoreId,
            IngredientId = IngredientId,
            AvailableQty = 92m,
            ReservedQty = 0m,
            LastUpdated = DateTime.UtcNow,
            RowVersion = [0]
        };
        context.StoreInventories.Add(inventory);
        var policy = new IcePolicy
        {
            StoreId = StoreId,
            IngredientId = IngredientId,
            DisplayUnitId = UnitId,
            SuggestedDailyQuantity = 20m,
            SuggestedShiftQuantity = 10m,
            AllowSupplementalIssue = true,
            AllowSameDayCarryOver = true,
            RequireVarianceApproval = true,
            UpdatedByStaffId = ManagerStaffId,
            UpdatedAtUtc = DateTime.UtcNow,
            Active = true,
            RowVersion = [0]
        };
        context.IcePolicies.Add(policy);
        context.SaveChanges();

        var shift = new OperationalShift
        {
            OperationalShiftId = 250,
            StoreId = StoreId,
            BusinessDate = DateTime.UtcNow.Date,
            Name = "Ca báo cáo",
            StartAtUtc = DateTime.UtcNow.AddHours(-8),
            EndAtUtc = DateTime.UtcNow,
            ShiftLeadId = ReceiverStaffId,
            Status = OperationalIceStatuses.Closed,
            CreatedByStaffId = ManagerStaffId,
            OpenedByStaffId = ManagerStaffId,
            ClosedByStaffId = ManagerStaffId,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-9),
            OpenedAtUtc = DateTime.UtcNow.AddHours(-8),
            ClosedAtUtc = DateTime.UtcNow,
            RowVersion = [0]
        };
        context.OperationalShifts.Add(shift);
        const int workShiftId = 25250;
        context.WorkShifts.Add(new WorkShift
        {
            ShiftId = workShiftId,
            StoreId = StoreId,
            UserId = ManagerStaffId,
            StartTime = DateTime.UtcNow.AddHours(-8),
            EndTime = DateTime.UtcNow,
            StartingCash = 0,
            ExpectedEndingCash = 0,
            Status = "Closed"
        });
        context.OperationalShiftWorkShifts.Add(new OperationalShiftWorkShift
        {
            OperationalShiftId = shift.OperationalShiftId,
            WorkShiftId = workShiftId,
            LinkedByStaffId = ManagerStaffId,
            LinkedAtUtc = DateTime.UtcNow.AddHours(-8)
        });
        var allocation = new IceAllocation
        {
            IceAllocationId = 250,
            PublicId = Guid.NewGuid(),
            OperationalShiftId = shift.OperationalShiftId,
            IcePolicyId = policy.IcePolicyId,
            StoreInventoryId = inventory.StoreInventoryId,
            IngredientId = IngredientId,
            OpeningCarryQuantity = 1m,
            InitialIssuedQuantity = 6m,
            SupplementalIssuedQuantity = 2m,
            ClosingCarryQuantity = 1m,
            TheoreticalUsageQuantity = 7m,
            ActualUsageQuantity = 8m,
            VarianceQuantity = 1m,
            ReservedOutstandingQuantity = 0m,
            ReservationReference = "ICE:REPORT:250",
            Status = OperationalIceStatuses.Closed,
            CloseReason = "Đá tan trong vận hành",
            CreatedByStaffId = ManagerStaffId,
            OpenedByStaffId = ManagerStaffId,
            ClosedByStaffId = ManagerStaffId,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-9),
            OpenedAtUtc = DateTime.UtcNow.AddHours(-8),
            ClosedAtUtc = DateTime.UtcNow,
            Revision = 2,
            RowVersion = [0]
        };
        context.IceAllocations.Add(allocation);

        var order = new Order
        {
            OrderId = 25300,
            StoreId = StoreId,
            WorkShiftId = workShiftId,
            OrderStatusId = SystemConstants.OrderStatuses.Completed,
            PaymentStatusId = SystemConstants.PaymentStatuses.Paid,
            OrderTypeId = SystemConstants.OrderTypes.DineIn,
            Total = 50_000m,
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };
        context.Orders.Add(order);
        context.InventoryTransactions.AddRange(
            new InventoryTransaction
            {
                StoreInventoryId = inventory.StoreInventoryId,
                Type = InventoryTransactionTypeEnum.SALES_DEDUCTION,
                StockStatus = InventoryStockStatus.NORMAL,
                Quantity = 8m,
                BeforeQty = 100m,
                AfterQty = 92m,
                UnitCost = salesCostComplete ? 2_000m : null,
                TotalCost = salesCostComplete ? 16_000m : null,
                ReferenceOrderId = order.OrderId,
                CreatedAt = DateTime.UtcNow.AddHours(-2)
            },
            new InventoryTransaction
            {
                StoreInventoryId = inventory.StoreInventoryId,
                Type = InventoryTransactionTypeEnum.SALES_RETURN,
                StockStatus = InventoryStockStatus.NORMAL,
                Quantity = 1m,
                BeforeQty = 92m,
                AfterQty = 93m,
                UnitCost = salesCostComplete ? 2_000m : null,
                TotalCost = salesCostComplete ? 2_000m : null,
                ReferenceOrderId = order.OrderId,
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            });
        var varianceTransaction = new InventoryTransaction
        {
            StoreInventoryId = inventory.StoreInventoryId,
            Type = InventoryTransactionTypeEnum.ICE_VARIANCE_OUT,
            StockStatus = InventoryStockStatus.NORMAL,
            Quantity = -1m,
            BeforeQty = 93m,
            AfterQty = 92m,
            UnitCost = 2_500m,
            TotalCost = 2_500m,
            CreatedAt = DateTime.UtcNow
        };
        context.InventoryTransactions.Add(varianceTransaction);
        context.SaveChanges();
        context.IceInventoryPostings.Add(new IceInventoryPosting
        {
            IceAllocationId = allocation.IceAllocationId,
            Revision = 2,
            PostingType = IcePostingTypes.VarianceOut,
            IdempotencyKey = "IceVariancePosting:250:2",
            InventoryTransactionId = varianceTransaction.InventoryTransactionId,
            Quantity = 1m,
            UnitCost = 2_500m,
            TotalCost = 2_500m,
            ApprovedByStaffId = ManagerStaffId,
            Reason = "Đã duyệt chênh lệch",
            CreatedAtUtc = DateTime.UtcNow
        });
        return (allocation.IceAllocationId, varianceTransaction.InventoryTransactionId);
    }

    private static void SeedStorePolicyAndShiftWithoutInventory(CafeChain.Data.AppDbContext context)
    {
        context.Stores.Add(new Store { StoreId = StoreId, Name = "No inventory", Active = true, CreatedAt = DateTime.UtcNow });
        context.Units.Add(new Unit { UnitId = UnitId, UnitCode = "G", Name = "g", Active = true });
        context.Ingredients.Add(new Ingredient { IngredientId = IngredientId, Code = "ICE_NO_STOCK", Name = "Đá", BaseUnitId = UnitId, Active = true });
        context.IcePolicies.Add(new IcePolicy
        {
            StoreId = StoreId,
            IngredientId = IngredientId,
            DisplayUnitId = UnitId,
            SuggestedDailyQuantity = 10m,
            SuggestedShiftQuantity = 5m,
            UpdatedByStaffId = ManagerStaffId,
            UpdatedAtUtc = DateTime.UtcNow,
            Active = true,
            RowVersion = [0]
        });
        context.OperationalShifts.Add(new OperationalShift
        {
            OperationalShiftId = 250,
            StoreId = StoreId,
            BusinessDate = DateTime.UtcNow.Date,
            Name = "Ca thiếu tồn",
            StartAtUtc = DateTime.UtcNow,
            EndAtUtc = DateTime.UtcNow.AddHours(8),
            Status = OperationalIceStatuses.Draft,
            CreatedByStaffId = ManagerStaffId,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = [0]
        });
    }

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { FindRepoRoot() }.Concat(parts).ToArray()));

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
            directory = directory.Parent;
        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Không tìm thấy root CafeChain.");
    }
}
