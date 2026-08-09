using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Models.Customers;
using CafeChain.Models.Enums.Inventory;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ice;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Transactions;
using CafeChain.Models.Permissions;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class OperationalIceWorkShiftSqlServerTests : IAsyncLifetime
{
    private const string Database = "CafeChain_OperationalIceLinkTests";
    private static string ConnectionString => SqlServerTestConnection.Create(Database);
    private int _storeId;
    private int _managerStaffId;
    private int _operationalShiftId;
    private int _workShiftId;

    public async Task InitializeAsync()
    {
        await using (var master = new SqlConnection(SqlServerTestConnection.MasterConnectionString()))
        {
            await master.OpenAsync();
            await using var command = master.CreateCommand();
            command.CommandText = $"IF DB_ID(N'{Database}') IS NULL CREATE DATABASE [{Database}];";
            await command.ExecuteNonQueryAsync();
        }

        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        var role = new Role
        {
            Name = RoleConstants.StoreManager,
            Active = true,
            IsStoreLevel = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Roles.Add(role);
        var store = new Store
        {
            Name = "SQL Operational Ice",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        var account = new Account
        {
            Email = "operational-ice-sql@test.local",
            PasswordHash = "x",
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Stores.Add(store);
        context.Accounts.Add(account);
        await context.SaveChangesAsync();

        context.AccountRoles.Add(new AccountRole { AccountId = account.AccountId, RoleId = role.RoleId });
        var staff = new Staff
        {
            AccountId = account.AccountId,
            StoreId = store.StoreId,
            FullName = "Quản lý SQL",
            Gender = 1,
            EmployeeStatus = 2,
            Active = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Staffs.Add(staff);
        await context.SaveChangesAsync();

        var operationalShift = new OperationalShift
        {
            StoreId = store.StoreId,
            BusinessDate = new DateTime(2026, 8, 10),
            Name = "Ca SQL",
            StartAtUtc = new DateTime(2026, 8, 10, 1, 0, 0, DateTimeKind.Utc),
            EndAtUtc = new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc),
            Status = OperationalIceStatuses.Open,
            CreatedByStaffId = staff.StaffId,
            OpenedByStaffId = staff.StaffId,
            CreatedAtUtc = DateTime.UtcNow,
            OpenedAtUtc = DateTime.UtcNow
        };
        var workShift = new WorkShift
        {
            StoreId = store.StoreId,
            UserId = account.AccountId,
            StartTimeUtc = new DateTime(2026, 8, 10, 2, 0, 0, DateTimeKind.Utc),
            BusinessDate = new DateTime(2026, 8, 10),
            StartingCash = 0m,
            ExpectedEndingCash = 0m,
            Status = WorkShiftStatuses.Open
        };
        context.OperationalShifts.Add(operationalShift);
        context.WorkShifts.Add(workShift);
        await context.SaveChangesAsync();

        _storeId = store.StoreId;
        _managerStaffId = staff.StaffId;
        _operationalShiftId = operationalShift.OperationalShiftId;
        _workShiftId = workShift.ShiftId;
    }

    public async Task DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task ConcurrentWorkShiftLink_ProducesSingleLinkAndReplaySafeResult()
    {
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var request = new LinkOperationalWorkShiftsRequest
        {
            OperationalShiftId = _operationalShiftId,
            WorkShiftIds = [_workShiftId]
        };

        var results = await Task.WhenAll(
            CreateService(firstContext).LinkWorkShiftsAsync(request, Actor()),
            CreateService(secondContext).LinkWorkShiftsAsync(request, Actor()));

        Assert.All(results, result => Assert.True(result.IsSuccess, result.Message));
        await using var verification = CreateContext();
        Assert.Equal(1, await verification.OperationalShiftWorkShifts.CountAsync());
        Assert.Equal(1, await verification.AuditLogs.CountAsync(x => x.Action == "LINK_WORKSHIFT"));
    }

    [Fact]
    public async Task ApprovePositiveVariance_PersistsPositiveAbsoluteMovementQuantity()
    {
        await using var context = CreateContext();
        var unit = new Unit
        {
            UnitCode = $"ICE_{Guid.NewGuid():N}"[..16],
            Name = "Gram kiểm thử SQL",
            Type = UnitType.KhoiLuong,
            Active = true
        };
        context.Units.Add(unit);
        await context.SaveChangesAsync();

        var ingredient = new Ingredient
        {
            Code = $"ICE_SQL_{Guid.NewGuid():N}"[..24],
            Name = "Đá kiểm thử duyệt chênh lệch",
            BaseUnitId = unit.UnitId,
            Active = true
        };
        context.Ingredients.Add(ingredient);
        await context.SaveChangesAsync();

        var inventory = new StoreInventory
        {
            StoreId = _storeId,
            IngredientId = ingredient.IngredientId,
            AvailableQty = 94m,
            ReservedQty = 4m,
            LastUpdated = DateTime.UtcNow
        };
        var policy = new IcePolicy
        {
            StoreId = _storeId,
            IngredientId = ingredient.IngredientId,
            DisplayUnitId = unit.UnitId,
            SuggestedDailyQuantity = 100m,
            SuggestedShiftQuantity = 10m,
            AllowSupplementalIssue = true,
            AllowSameDayCarryOver = true,
            RequireVarianceApproval = true,
            VarianceApprovalQuantityThreshold = 10m,
            VarianceApprovalPercentThreshold = 100m,
            Active = true,
            UpdatedByStaffId = _managerStaffId,
            UpdatedAtUtc = DateTime.UtcNow
        };
        context.StoreInventories.Add(inventory);
        context.IcePolicies.Add(policy);
        await context.SaveChangesAsync();

        var operationalShift = await context.OperationalShifts.SingleAsync(x => x.OperationalShiftId == _operationalShiftId);
        operationalShift.Status = OperationalIceStatuses.PendingApproval;
        var allocation = new IceAllocation
        {
            PublicId = Guid.NewGuid(),
            OperationalShiftId = _operationalShiftId,
            IcePolicyId = policy.IcePolicyId,
            StoreInventoryId = inventory.StoreInventoryId,
            IngredientId = ingredient.IngredientId,
            InitialIssuedQuantity = 10m,
            TheoreticalUsageQuantity = 6m,
            ActualUsageQuantity = 10m,
            VarianceQuantity = 4m,
            ReservedOutstandingQuantity = 4m,
            ReservationReference = $"ICE:SQL:{Guid.NewGuid():N}",
            Status = OperationalIceStatuses.PendingApproval,
            CloseReason = "Chênh lệch kiểm thử SQL",
            CreatedByStaffId = _managerStaffId,
            OpenedByStaffId = _managerStaffId,
            CreatedAtUtc = DateTime.UtcNow,
            OpenedAtUtc = DateTime.UtcNow,
            Revision = 1
        };
        context.IceAllocations.Add(allocation);
        await context.SaveChangesAsync();

        var result = await CreateService(context).ApproveVarianceAsync(
            new ApproveIceVarianceRequest
            {
                IceAllocationId = allocation.IceAllocationId,
                Reason = "Đã đối chiếu chênh lệch"
            },
            Actor());

        Assert.True(result.IsSuccess, result.Message);
        var movement = await context.InventoryTransactions.SingleAsync(x =>
            x.StoreInventoryId == inventory.StoreInventoryId
            && x.Type == InventoryTransactionTypeEnum.ICE_VARIANCE_OUT);
        Assert.Equal(4m, movement.Quantity);
        Assert.Equal(94m, movement.BeforeQty);
        Assert.Equal(90m, movement.AfterQty);
        Assert.Equal(90m, await context.StoreInventories
            .Where(x => x.StoreInventoryId == inventory.StoreInventoryId)
            .Select(x => x.AvailableQty)
            .SingleAsync());
    }

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    private static OperationalIceService CreateService(AppDbContext context)
    {
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.CanAccessStoreAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);
        return new OperationalIceService(context, scope.Object);
    }

    private AdminActorContext Actor() => new()
    {
        StaffId = _managerStaffId,
        StoreId = _storeId,
        RoleNames = [RoleConstants.StoreManager]
    };
}
