using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Models.Customers;
using CafeChain.Models.Inventories.Ice;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class OperationalIcePolicyHardeningTests : IntegrationTestBase
{
    private const int StoreId = 9741;
    private const int GramUnitId = 1;
    private const int KilogramUnitId = 2;
    private const int MillilitreUnitId = 3;
    private const int IceIngredientId = 7;
    private const int WrongIngredientId = 9746;
    private const int NoInventoryIngredientId = 9747;
    private const int ManagerStaffId = 9748;
    private const int ShiftLeadStaffId = 9749;

    [Fact]
    public async Task PolicySetup_ReturnsOnlyEligibleIngredientAndGramKilogramUnits_WithUsableStock()
    {
        using var context = CreateDbContext();
        SeedCatalog(context, includePolicy: true);
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetPolicySetupAsync(StoreId);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.Data.IsValid);
        Assert.Equal(["kg", "g"], result.Data.Units.Select(x => x.Code));
        Assert.Collection(result.Data.Ingredients,
            option =>
            {
                Assert.Equal(IceIngredientId, option.Id);
                Assert.Equal("ING00007", option.Code);
            });
        Assert.NotNull(result.Data.Inventory);
        Assert.Equal(125m, result.Data.Inventory!.PhysicalQuantity);
        Assert.Equal(25m, result.Data.Inventory.ReservedQuantity);
        Assert.Equal(100m, result.Data.Inventory.AvailableQuantity);
    }

    [Fact]
    public async Task SavePolicy_RejectsIngredientOutsideWhitelist_AndUnitOutsideGramKilogram()
    {
        using var context = CreateDbContext();
        SeedCatalog(context);
        await context.SaveChangesAsync();
        var service = CreateService(context, SuccessfulConversion());

        var wrongIngredient = await service.SavePolicyAsync(
            ValidPolicyRequest(ingredientId: WrongIngredientId), ManagerActor());
        var wrongUnit = await service.SavePolicyAsync(
            ValidPolicyRequest(displayUnitId: MillilitreUnitId), ManagerActor());

        Assert.False(wrongIngredient.IsSuccess);
        Assert.Equal(OperationalIceErrorCodes.InvalidRequest, wrongIngredient.ErrorCode);
        Assert.False(wrongUnit.IsSuccess);
        Assert.Equal(OperationalIceErrorCodes.InvalidRequest, wrongUnit.ErrorCode);
        Assert.Empty(context.IcePolicies);
    }

    [Theory]
    [InlineData(0, 10, 0, 0, true, "Định mức ngày")]
    [InlineData(10, 11, 0, 0, true, "không được vượt")]
    [InlineData(10, 5, -1, 0, true, "không được âm")]
    [InlineData(10, 5, 0, 101, true, "từ 0 đến 100")]
    [InlineData(10, 5, 0, 0, false, "bắt buộc duyệt")]
    public async Task SavePolicy_RejectsUnsafeQuantities(
        decimal daily,
        decimal shift,
        decimal quantityThreshold,
        decimal percentThreshold,
        bool requireApproval,
        string expectedMessage)
    {
        using var context = CreateDbContext();
        SeedCatalog(context);
        await context.SaveChangesAsync();
        var service = CreateService(context, SuccessfulConversion());

        var result = await service.SavePolicyAsync(ValidPolicyRequest(
            daily: daily,
            shift: shift,
            quantityThreshold: quantityThreshold,
            percentThreshold: percentThreshold,
            requireApproval: requireApproval), ManagerActor());

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationalIceErrorCodes.InvalidRequest, result.ErrorCode);
        Assert.Contains(expectedMessage, result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(context.IcePolicies);
    }

    [Fact]
    public async Task CreateShift_RejectsMissingPolicy_AndMissingShiftLead()
    {
        using var context = CreateDbContext();
        SeedCatalog(context);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var request = ValidShiftRequest(shiftLeadId: null);

        var missingPolicy = await service.CreateShiftAsync(request, ManagerActor());
        context.IcePolicies.Add(ValidPolicy());
        await context.SaveChangesAsync();
        var missingLead = await service.CreateShiftAsync(request, ManagerActor());

        Assert.False(missingPolicy.IsSuccess);
        Assert.Equal(OperationalIceErrorCodes.InvalidState, missingPolicy.ErrorCode);
        Assert.False(missingLead.IsSuccess);
        Assert.Equal(OperationalIceErrorCodes.InvalidRequest, missingLead.ErrorCode);
        Assert.Contains("ca trưởng", missingLead.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateShift_RequiresActiveSameStoreSupervisorOrStoreManager()
    {
        using var context = CreateDbContext();
        SeedCatalog(context, includePolicy: true);
        SeedStaff(context, ShiftLeadStaffId, "Nhân viên bán hàng", RoleConstants.SalesStaff, active: true);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var rejected = await service.CreateShiftAsync(
            ValidShiftRequest(ShiftLeadStaffId), ManagerActor());

        Assert.False(rejected.IsSuccess);
        Assert.Equal(OperationalIceErrorCodes.InvalidRequest, rejected.ErrorCode);
        Assert.Contains("Ca trưởng hoặc Cửa hàng trưởng", rejected.Message);
    }

    [Fact]
    public async Task CreateShift_WithValidPolicyAndShiftLead_Succeeds()
    {
        using var context = CreateDbContext();
        SeedCatalog(context, includePolicy: true);
        SeedStaff(context, ShiftLeadStaffId, "Ca trưởng", RoleConstants.ShiftSupervisor, active: true);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.CreateShiftAsync(
            ValidShiftRequest(ShiftLeadStaffId), ManagerActor());

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(ShiftLeadStaffId, result.Data.ShiftLeadId);
        Assert.Equal(OperationalIceStatuses.Draft, result.Data.Status);
    }

    [Fact]
    public async Task OpenAllocation_RejectsShiftWithoutLead_BeforeCreatingReservation()
    {
        using var context = CreateDbContext();
        SeedCatalog(context, includePolicy: true);
        var shift = SeedDraftShift(context, shiftLeadId: null);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        var result = await service.OpenAllocationAsync(new OpenIceAllocationRequest
        {
            OperationalShiftId = shift.OperationalShiftId,
            InitialIssuedQuantity = 10m
        }, ManagerActor());

        Assert.False(result.IsSuccess);
        Assert.Equal(OperationalIceErrorCodes.InvalidState, result.ErrorCode);
        Assert.Contains("chưa có ca trưởng", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(context.IceAllocations);
        Assert.Equal(25m, await context.StoreInventories
            .Where(x => x.StoreId == StoreId && x.IngredientId == IceIngredientId)
            .Select(x => x.ReservedQty)
            .SingleAsync());
    }

    [Fact]
    public void OperationalIceUi_ExposesValidationStockAndResponsiveGuards()
    {
        var root = FindRepoRoot();
        var index = File.ReadAllText(Path.Combine(root, "CafeChain", "Areas", "Admin", "Views", "AdminOperationalIce", "Index.cshtml"));
        var details = File.ReadAllText(Path.Combine(root, "CafeChain", "Areas", "Admin", "Views", "AdminOperationalIce", "Details.cshtml"));
        var report = File.ReadAllText(Path.Combine(root, "CafeChain", "Areas", "Admin", "Views", "AdminOperationalIce", "Report.cshtml"));
        var controller = File.ReadAllText(Path.Combine(root, "CafeChain", "Areas", "Admin", "Controllers", "AdminOperationalIceController.cs"));
        var reportService = File.ReadAllText(Path.Combine(root, "CafeChain", "Application", "Services", "Inventories", "OperationalIceReportService.cs"));
        var css = File.ReadAllText(Path.Combine(root, "CafeChain", "wwwroot", "css", "Admin", "OperationalIce", "operational-ice.css"));

        Assert.Contains("Tồn vật lý", index);
        Assert.Contains("Đang giữ chỗ", index);
        Assert.Contains("Tồn khả dụng", index);
        Assert.Contains("Model.HasValidPolicy && row.HasShiftLead", index);
        Assert.Contains("Định mức mỗi ca không được vượt định mức ngày", index);
        Assert.Contains("dd/MM/yyyy HH:mm", index);
        Assert.Contains("data-ice-date-display", index);
        Assert.Contains("data-ice-datetime-display", index);
        Assert.Contains("InputNumber(Model.Policy.SuggestedDailyQuantity)", index);
        Assert.Contains("InputNumber(Model.Policy?.VarianceApprovalPercentThreshold ?? 0)", index);
        Assert.Contains("DisplayQuantity(Model.Inventory.PhysicalQuantity", index);
        Assert.Contains("value.ToString(\"0.##\", viCulture)", index);
        Assert.Contains("DisplayQuantity(Model.AvailableQuantity)", details);
        Assert.Contains("value.ToString(\"0.##\", viCulture)", report);
        Assert.DoesNotContain("ToString(\"N2\")", index);
        Assert.DoesNotContain("ToString(\"N2\")", details);
        Assert.DoesNotContain("{value:N3}", report);
        Assert.Contains("DisplayUnitSymbol(policy.DisplayUnit.UnitCode)", controller);
        Assert.Contains("DisplayUnitSymbol(allocation.IcePolicy.DisplayUnit.UnitCode)", controller);
        Assert.Contains("NormalizeUnitCode", reportService);
        Assert.DoesNotContain("type=\"date\"", index);
        Assert.DoesNotContain("type=\"datetime-local\"", index);
        Assert.DoesNotContain("href=\"#createShift\"", index);
        Assert.Contains("Tồn vật lý", details);
        Assert.Contains(".ice-policy-group", css);
        Assert.Contains(".ice-btn:disabled", css);
        Assert.Contains("@media (max-width: 600px)", css);
    }

    private static OperationalIceService CreateService(
        CafeChain.Data.AppDbContext context,
        IUnitConversionService? conversion = null)
    {
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), StoreId)).ReturnsAsync(true);
        return new OperationalIceService(context, scope.Object, unitConversionService: conversion);
    }

    private static IUnitConversionService SuccessfulConversion()
    {
        var conversion = new Mock<IUnitConversionService>();
        conversion.Setup(x => x.ConvertAsync(It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int?>()))
            .ReturnsAsync((int _, decimal quantity, int _, int? _) => ServiceResult<decimal>.Success(quantity));
        return conversion.Object;
    }

    private static AdminActorContext ManagerActor() => new()
    {
        StaffId = ManagerStaffId,
        StoreId = StoreId,
        RoleNames = [RoleConstants.StoreManager]
    };

    private static SaveIcePolicyRequest ValidPolicyRequest(
        int ingredientId = IceIngredientId,
        int displayUnitId = KilogramUnitId,
        decimal daily = 100m,
        decimal shift = 25m,
        decimal quantityThreshold = 5m,
        decimal percentThreshold = 10m,
        bool requireApproval = true) => new()
    {
        StoreId = StoreId,
        IngredientId = ingredientId,
        DisplayUnitId = displayUnitId,
        SuggestedDailyQuantity = daily,
        SuggestedShiftQuantity = shift,
        AllowSupplementalIssue = true,
        AllowSameDayCarryOver = true,
        RequireVarianceApproval = requireApproval,
        VarianceApprovalQuantityThreshold = quantityThreshold,
        VarianceApprovalPercentThreshold = percentThreshold
    };

    private static CreateOperationalShiftRequest ValidShiftRequest(int? shiftLeadId = ShiftLeadStaffId)
    {
        var businessDate = DateTime.Now.Date;
        var start = DateTime.SpecifyKind(businessDate.AddHours(6), DateTimeKind.Local).ToUniversalTime();
        return new CreateOperationalShiftRequest
        {
            StoreId = StoreId,
            BusinessDate = businessDate,
            Name = "Ca sáng",
            StartAtUtc = start,
            EndAtUtc = start.AddHours(8),
            ShiftLeadId = shiftLeadId
        };
    }

    private static void SeedCatalog(CafeChain.Data.AppDbContext context, bool includePolicy = false)
    {
        context.Stores.Add(new Store
        {
            StoreId = StoreId,
            Name = "Cửa hàng test đá",
            Active = true,
            CreatedAt = DateTime.UtcNow
        });
        context.Ingredients.AddRange(
            new Ingredient { IngredientId = WrongIngredientId, Code = "CHOCOLATE", Name = "Bột chocolate", BaseUnitId = GramUnitId, Active = true },
            new Ingredient { IngredientId = NoInventoryIngredientId, Code = "ICE_NO_STOCK", Name = "Đá chưa có tồn", BaseUnitId = GramUnitId, Active = true });
        context.StoreInventories.AddRange(
            new StoreInventory
            {
                StoreId = StoreId,
                IngredientId = IceIngredientId,
                AvailableQty = 125m,
                ReservedQty = 25m,
                LastUpdated = DateTime.UtcNow,
                RowVersion = [0]
            },
            new StoreInventory
            {
                StoreId = StoreId,
                IngredientId = WrongIngredientId,
                AvailableQty = 50m,
                ReservedQty = 0m,
                LastUpdated = DateTime.UtcNow,
                RowVersion = [0]
            });
        if (includePolicy)
            context.IcePolicies.Add(ValidPolicy());
    }

    private static IcePolicy ValidPolicy() => new()
    {
        StoreId = StoreId,
        IngredientId = IceIngredientId,
        DisplayUnitId = KilogramUnitId,
        SuggestedDailyQuantity = 100m,
        SuggestedShiftQuantity = 25m,
        AllowSupplementalIssue = true,
        AllowSameDayCarryOver = true,
        RequireVarianceApproval = true,
        VarianceApprovalQuantityThreshold = 5m,
        VarianceApprovalPercentThreshold = 10m,
        Active = true,
        UpdatedByStaffId = ManagerStaffId,
        UpdatedAtUtc = DateTime.UtcNow,
        RowVersion = [0]
    };

    private static OperationalShift SeedDraftShift(CafeChain.Data.AppDbContext context, int? shiftLeadId)
    {
        var shift = new OperationalShift
        {
            StoreId = StoreId,
            BusinessDate = DateTime.UtcNow.Date,
            Name = "Ca chưa phân công",
            StartAtUtc = DateTime.UtcNow,
            EndAtUtc = DateTime.UtcNow.AddHours(8),
            ShiftLeadId = shiftLeadId,
            Status = OperationalIceStatuses.Draft,
            CreatedByStaffId = ManagerStaffId,
            CreatedAtUtc = DateTime.UtcNow,
            RowVersion = [0]
        };
        context.OperationalShifts.Add(shift);
        return shift;
    }

    private static void SeedStaff(
        CafeChain.Data.AppDbContext context,
        int staffId,
        string fullName,
        string roleName,
        bool active)
    {
        var accountId = staffId;
        var roleId = context.Roles
            .Where(x => x.Name == roleName)
            .Select(x => x.RoleId)
            .Single();
        context.Accounts.Add(new Account
        {
            AccountId = accountId,
            Email = $"ice-{staffId}@test.local",
            PasswordHash = "test",
            Active = active,
            CreatedAt = DateTime.UtcNow
        });
        context.AccountRoles.Add(new AccountRole { AccountId = accountId, RoleId = roleId });
        context.Staffs.Add(new Staff
        {
            StaffId = staffId,
            AccountId = accountId,
            StoreId = StoreId,
            FullName = fullName,
            Active = active,
            EmployeeStatus = 2,
            CreatedAt = DateTime.UtcNow
        });
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Không tìm thấy root CafeChain.");
    }
}
