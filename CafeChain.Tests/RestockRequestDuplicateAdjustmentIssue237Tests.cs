using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.RestockRequests;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Results;
using CafeChain.Application.Services.Inventories;
using CafeChain.Application.Services.Security;
using CafeChain.Models.Customers;
using CafeChain.Models.Drinks;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Ingredients;
using CafeChain.Models.Inventories.Stock;
using CafeChain.Models.Permissions;
using CafeChain.Models.Staffs;
using CafeChain.Models.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;
using Xunit;

namespace CafeChain.Tests;

public sealed class RestockRequestDuplicateAdjustmentIssue237Tests : IntegrationTestBase
{
    private const int StoreA = 23701;
    private const int StoreB = 23702;
    private const int IngredientId = 23701;
    private const int BaseUnitId = 1;
    private const int ProcurementUnitId = 2;
    private const int ManagerA = 23701;
    private const int ManagerB = 23702;
    private const int WarehouseStaff = 23703;
    private const int SalesStaff = 23704;

    [Fact]
    public async Task CreateManual_NoActiveRequest_CreatesExactlyOne()
    {
        using var context = CreateDbContext();
        await SeedAsync(context);

        var result = await CreateService(context).CreateManualAsync(Request(StoreA, "NEW-1"), ManagerA);

        Assert.True(result.IsSuccess, result.Message);
        Assert.Equal(1, await context.RestockRequests.CountAsync());
        var created = await context.RestockRequests.SingleAsync();
        Assert.Equal(10m, created.RequestedProcurementQuantity);
        Assert.Equal(10_000m, created.RequestedQuantity);
        Assert.Matches("^RR-S23701-[0-9]{8}-[0-9]{5}$", created.ReferenceCode);
        Assert.Equal(created.ReferenceCode, result.Data!.ReferenceCode);
        Assert.NotEqual("NEW-1", created.ReferenceCode);
    }

    [Fact]
    public async Task RetryCreate_DoesNotCreateSecondRestock()
    {
        using var context = CreateDbContext();
        await SeedAsync(context);
        var service = CreateService(context);
        var request = Request(StoreA, "same-create-request");

        var first = await service.CreateManualAsync(request, ManagerA);
        var replay = await service.CreateManualAsync(request, ManagerA);

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(replay.IsSuccess, replay.Message);
        Assert.True(replay.Data!.AlreadyExisted);
        Assert.Equal(first.Data!.RestockRequestId, replay.Data.RestockRequestId);
        Assert.Equal(first.Data.ReferenceCode, replay.Data.ReferenceCode);
        Assert.Equal(1, await context.RestockRequests.CountAsync());
    }

    [Fact]
    public void ReferenceCode_IsUniqueImmutableAndNotClientControlled()
    {
        using var context = CreateDbContext();
        var entity = context.Model.FindEntityType(typeof(RestockRequest))!;
        var referenceProperty = entity.FindProperty(nameof(RestockRequest.ReferenceCode))!;
        var referenceIndex = entity.GetIndexes()
            .Single(x => x.GetDatabaseName() == "UX_RestockRequests_ReferenceCode");
        var setter = typeof(RestockRequest)
            .GetProperty(nameof(RestockRequest.ReferenceCode))!
            .SetMethod;

        Assert.True(referenceIndex.IsUnique);
        Assert.Equal(PropertySaveBehavior.Throw, referenceProperty.GetAfterSaveBehavior());
        Assert.True(setter == null || !setter.IsPublic);
        Assert.Null(typeof(CreateProcurementDemandRequest).GetProperty("ReferenceCode"));
    }

    [Theory]
    [InlineData(RestockRequestStatuses.Draft)]
    [InlineData(RestockRequestStatuses.Submitted)]
    [InlineData(RestockRequestStatuses.Processing)]
    [InlineData(RestockRequestStatuses.PartiallyReceived)]
    public async Task CreateManual_ActiveStoreIngredient_ReturnsTypedConflict(string status)
    {
        using var context = CreateDbContext();
        await SeedAsync(context);
        var service = CreateService(context);
        var first = await service.CreateManualAsync(Request(StoreA, "ACTIVE-1"), ManagerA);
        var existing = await context.RestockRequests.SingleAsync();
        existing.Status = status;
        await context.SaveChangesAsync();

        var duplicate = await service.CreateManualAsync(Request(StoreA, "ACTIVE-2"), ManagerA);

        Assert.False(duplicate.IsSuccess);
        Assert.Equal(RestockRequestErrorCodes.ActiveRequestExists, duplicate.ErrorCode);
        Assert.Equal(first.Data!.RestockRequestId, duplicate.Data!.ExistingActiveRequest!.RestockRequestId);
        Assert.Equal(status, duplicate.Data.ExistingActiveRequest.Status);
        Assert.Equal(1, await context.RestockRequests.CountAsync());
    }

    [Theory]
    [InlineData(RestockRequestStatuses.Completed)]
    [InlineData(RestockRequestStatuses.Rejected)]
    [InlineData(RestockRequestStatuses.Cancelled)]
    public async Task CreateManual_InactiveHistory_AllowsNewRequest(string inactiveStatus)
    {
        using var context = CreateDbContext();
        await SeedAsync(context);
        var service = CreateService(context);
        Assert.True((await service.CreateManualAsync(Request(StoreA, "OLD-1"), ManagerA)).IsSuccess);
        var old = await context.RestockRequests.SingleAsync();
        old.Status = inactiveStatus;
        await context.SaveChangesAsync();

        var next = await service.CreateManualAsync(Request(StoreA, "NEW-2"), ManagerA);

        Assert.True(next.IsSuccess, next.Message);
        Assert.Equal(2, await context.RestockRequests.CountAsync());
    }

    [Fact]
    public async Task SameIngredient_DifferentStores_CanHaveSeparateActiveRequests()
    {
        using var context = CreateDbContext();
        await SeedAsync(context);
        var service = CreateService(context);

        var first = await service.CreateManualAsync(Request(StoreA, "STORE-A"), ManagerA);
        var second = await service.CreateManualAsync(Request(StoreB, "STORE-B"), ManagerB);

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(second.IsSuccess, second.Message);
        Assert.Equal(2, await context.RestockRequests.CountAsync());
        Assert.NotEqual(first.Data!.ReferenceCode, second.Data!.ReferenceCode);
    }

    [Fact]
    public async Task AddDemand_IncreasesDemand_PreservesAllocation_AndWritesAudit()
    {
        using var context = CreateDbContext();
        await SeedAsync(context);
        var service = CreateService(context);
        var created = await service.CreateManualAsync(Request(StoreA, "ADJUST-1"), ManagerA);
        var demand = await context.RestockRequests.SingleAsync();
        demand.Status = RestockRequestStatuses.Processing;
        context.RestockSourcingAllocations.Add(new RestockSourcingAllocation
        {
            RestockRequestId = demand.RestockRequestId,
            DecisionType = RestockSourcingDecisionTypes.Purchase,
            ProcurementQuantity = 4m,
            ProcurementUnitId = ProcurementUnitId,
            Status = RestockSourcingAllocationStatuses.PendingPurchaseAdvice,
            CreatedByStaffId = WarehouseStaff,
            CreatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();
        var rowVersion = Convert.ToBase64String(demand.RowVersion);

        var result = await service.AddDemandAdjustmentAsync(new AddRestockDemandAdjustmentRequest
        {
            RestockRequestId = created.Data!.RestockRequestId,
            AdjustmentProcurementQuantity = 3m,
            ProcurementUnitId = ProcurementUnitId,
            NeedByDate = DateTime.UtcNow.AddDays(4),
            Reason = "Tăng nhu cầu cuối tuần",
            RowVersion = rowVersion,
            RequestKey = "adjust-once"
        }, ManagerA);

        Assert.True(result.IsSuccess, result.Message);
        context.ChangeTracker.Clear();
        var updated = await context.RestockRequests.Include(x => x.SourcingAllocations).SingleAsync();
        Assert.Equal(13m, updated.RequestedProcurementQuantity);
        Assert.Equal(13_000m, updated.RequestedQuantity);
        Assert.Equal(4m, updated.SourcingAllocations.Single().ProcurementQuantity);
        Assert.Equal(RestockSourcingStatuses.PartiallyAllocated, updated.SourcingStatus);
        Assert.Empty(await context.PurchaseAdvices.ToListAsync());
        Assert.Empty(await context.PurchaseOrders.ToListAsync());
        var audit = await context.RestockRequestTransitions.SingleAsync();
        Assert.Equal(RestockRequestStatuses.Processing, audit.PreviousStatus);
        Assert.Equal(RestockRequestStatuses.Processing, audit.NewStatus);
        Assert.Equal(10m, audit.QuantityBefore);
        Assert.Equal(13m, audit.QuantityAfter);
        Assert.Equal("Tăng nhu cầu cuối tuần", audit.Reason);
        Assert.StartsWith(RestockRequestAuditKeys.DemandAdjustmentPrefix, audit.RequestKey);
    }

    [Fact]
    public async Task AddDemand_SameRequestKey_IsIdempotent()
    {
        using var context = CreateDbContext();
        await SeedAsync(context);
        var service = CreateService(context);
        var created = await service.CreateManualAsync(Request(StoreA, "REPLAY-1"), ManagerA);
        var demand = await context.RestockRequests.SingleAsync();
        var command = Adjustment(created.Data!.RestockRequestId, demand.RowVersion, "same-key", 2m);

        var first = await service.AddDemandAdjustmentAsync(command, ManagerA);
        var replay = await service.AddDemandAdjustmentAsync(command, ManagerA);

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(replay.IsSuccess, replay.Message);
        Assert.True(replay.Data!.WasReplay);
        Assert.Equal(12m, (await context.RestockRequests.SingleAsync()).RequestedProcurementQuantity);
        Assert.Equal(1, await context.RestockRequestTransitions.CountAsync());
    }

    [Fact]
    public async Task AddDemand_ValidatesQuantityUnitReasonVersionAndScope()
    {
        using var context = CreateDbContext();
        await SeedAsync(context);
        var service = CreateService(context);
        var created = await service.CreateManualAsync(Request(StoreA, "VALIDATE-1"), ManagerA);
        var demand = await context.RestockRequests.SingleAsync();

        Assert.Equal(RestockRequestErrorCodes.DemandAdjustmentInvalid,
            (await service.AddDemandAdjustmentAsync(Adjustment(created.Data!.RestockRequestId, demand.RowVersion, "zero", 0m), ManagerA)).ErrorCode);
        var noReason = Adjustment(created.Data.RestockRequestId, demand.RowVersion, "reason", 1m);
        noReason.Reason = " ";
        Assert.Equal(RestockRequestErrorCodes.DemandAdjustmentInvalid,
            (await service.AddDemandAdjustmentAsync(noReason, ManagerA)).ErrorCode);
        var wrongUnit = Adjustment(created.Data.RestockRequestId, demand.RowVersion, "unit", 1m);
        wrongUnit.ProcurementUnitId = BaseUnitId;
        Assert.Equal(RestockRequestErrorCodes.ProcurementUnitMismatch,
            (await service.AddDemandAdjustmentAsync(wrongUnit, ManagerA)).ErrorCode);
        Assert.Equal(RestockRequestErrorCodes.ResourceChanged,
            (await service.AddDemandAdjustmentAsync(Adjustment(created.Data.RestockRequestId, new byte[] { 9, 9 }, "stale", 1m), ManagerA)).ErrorCode);
        Assert.Equal(RestockRequestErrorCodes.Unauthorized,
            (await service.AddDemandAdjustmentAsync(Adjustment(created.Data.RestockRequestId, demand.RowVersion, "sales", 1m), SalesStaff)).ErrorCode);
    }

    [Fact]
    public async Task SourcingDecision_IsOwnedByWarehouseOrBusinessOwner_NotStoreManager()
    {
        using var context = CreateDbContext();
        await SeedAsync(context);
        var service = CreateService(context);
        var created = await service.CreateManualAsync(Request(StoreA, "SOURCE-1"), ManagerA);
        var request = new SourcingDecisionRequest
        {
            RestockRequestId = created.Data!.RestockRequestId,
            DecisionType = RestockSourcingDecisionTypes.Purchase,
            ProcurementQuantity = 3m,
            ProcurementUnitId = ProcurementUnitId,
            Reason = "Mua ngoài"
        };

        var manager = await service.SetSourcingDecisionAsync(request, ManagerA);
        var warehouse = await service.SetSourcingDecisionAsync(request, WarehouseStaff);

        Assert.False(manager.IsSuccess);
        Assert.True(warehouse.IsSuccess, warehouse.Message);
    }

    [Theory]
    [InlineData(2601, "Cannot insert duplicate key row with unique index 'UX_RestockRequest_Active_Store_Ingredient'", true)]
    [InlineData(2627, "Violation of UNIQUE KEY constraint 'UX_RestockRequest_Active_Store_Ingredient'", true)]
    [InlineData(2601, "Violation of index IX_Unrelated", false)]
    [InlineData(547, "UX_RestockRequest_Active_Store_Ingredient", false)]
    public void ProviderErrorClassifier_OnlyMapsExpectedUniqueConflict(int number, string message, bool expected)
    {
        Assert.Equal(expected, RestockRequestService.IsActiveRequestUniqueConflict(number, message));
    }

    [Fact]
    public void ActiveStatusContract_MatchesFilteredUniqueIndex()
    {
        using var context = CreateDbContext();
        var entity = context.Model.FindEntityType(typeof(RestockRequest))!;
        var index = entity.GetIndexes().Single(x => x.GetDatabaseName() == "UX_RestockRequest_Active_Store_Ingredient");
        var filter = index.GetFilter()!;

        Assert.True(index.IsUnique);
        foreach (var status in RestockRequestStatuses.ActiveValues)
            Assert.Contains($"'{status}'", filter, StringComparison.Ordinal);
        Assert.DoesNotContain(RestockRequestStatuses.Completed, filter, StringComparison.Ordinal);
        Assert.DoesNotContain(RestockRequestStatuses.Rejected, filter, StringComparison.Ordinal);
        Assert.DoesNotContain(RestockRequestStatuses.Cancelled, filter, StringComparison.Ordinal);
    }

    [Fact]
    public void Views_ExposeConflictAdjustmentAuditAndSafePurchaseAdviceStates()
    {
        var root = FindRepoRoot();
        var create = File.ReadAllText(Path.Combine(root, "CafeChain", "Areas", "Admin", "Views", "AdminRestockRequests", "CreateManual.cshtml"));
        var details = File.ReadAllText(Path.Combine(root, "CafeChain", "Areas", "Admin", "Views", "AdminRestockRequests", "Details.cshtml"));
        var advice = File.ReadAllText(Path.Combine(root, "CafeChain", "Areas", "Admin", "Views", "AdminPurchaseAdvices", "Create.cshtml"));
        var css = File.ReadAllText(Path.Combine(root, "CafeChain", "wwwroot", "css", "Admin", "PurchaseAdvice", "purchase-advice.css"));

        Assert.Contains("CheckActive", create);
        Assert.Contains("Nguyên liệu đã có yêu cầu đang xử lý", create);
        Assert.Contains("Bổ sung nhu cầu", create);
        Assert.Contains("activeExists", create);
        Assert.Contains("Mã yêu cầu sẽ được hệ thống tự tạo sau khi lưu.", create);
        Assert.DoesNotContain("Mã tham chiếu</label>", create);
        Assert.Contains("@Model.ReferenceCode", details);
        Assert.Contains("asp-action=\"AddDemand\"", details);
        Assert.Contains("Đã bổ sung nhu cầu", details);
        Assert.Contains("transition.IsDemandAdjustment ? transition.Reason", details);
        Assert.Contains("Tạo từ yêu cầu bổ sung", advice);
        Assert.Contains("Chưa có yêu cầu nào sẵn sàng", advice);
        Assert.Contains("PendingPurchaseAllocationProcurementQuantity", advice);
        Assert.Contains("savePurchaseAdvice", advice);
        Assert.Contains("hasInitialValidSelection", advice);
        Assert.Contains("data-max-quantity", advice);
        Assert.Contains("valueAsNumber", advice);
        Assert.Contains("purchaseAdviceSubmitStatus", advice);
        Assert.Contains("Yêu cầu chưa hoàn tất", advice);
        Assert.DoesNotContain("save.disabled = true", advice);
        Assert.Contains("@media (max-width:600px)", css);
    }

    private static CreateProcurementDemandRequest Request(int storeId, string sourceReference) => new()
    {
        StoreId = storeId,
        IngredientId = IngredientId,
        RequestedProcurementQuantity = 10m,
        ProcurementUnitId = ProcurementUnitId,
        SourceReferenceId = sourceReference,
        NeedByDate = DateTime.UtcNow.AddDays(3),
        Priority = RestockRequestPriorities.High,
        Note = "Nhu cầu vận hành"
    };

    private static AddRestockDemandAdjustmentRequest Adjustment(int id, byte[] rowVersion, string key, decimal quantity) => new()
    {
        RestockRequestId = id,
        AdjustmentProcurementQuantity = quantity,
        ProcurementUnitId = ProcurementUnitId,
        Reason = "Bổ sung nhu cầu kiểm thử",
        RowVersion = Convert.ToBase64String(rowVersion),
        RequestKey = key
    };

    private static RestockRequestService CreateService(CafeChain.Data.AppDbContext context)
    {
        var conversion = new Mock<IUnitConversionService>();
        conversion.Setup(x => x.ConvertAsync(
                IngredientId,
                It.IsAny<decimal>(),
                ProcurementUnitId,
                It.IsAny<int?>()))
            .Returns((int _, decimal quantity, int _, int? _) =>
                Task.FromResult(ServiceResult<decimal>.Success(quantity * 1000m)));
        return new RestockRequestService(
            context,
            new ScopeAuthorizationService(context),
            new Mock<ILogger<RestockRequestService>>().Object,
            conversion.Object);
    }

    private static async Task SeedAsync(CafeChain.Data.AppDbContext context)
    {
        context.Stores.AddRange(
            new Store { StoreId = StoreA, Name = "Chi nhánh A", Address = "A", Phone = "1", Active = true, CreatedAt = DateTime.UtcNow },
            new Store { StoreId = StoreB, Name = "Chi nhánh B", Address = "B", Phone = "2", Active = true, CreatedAt = DateTime.UtcNow });
        context.Ingredients.Add(new Ingredient { IngredientId = IngredientId, Code = "ISSUE237", Name = "Cà phê kiểm thử", BaseUnitId = BaseUnitId, Active = true });
        EnsureStaff(context, ManagerA, StoreA, RoleConstants.StoreManager, "manager-a-237@test.local");
        EnsureStaff(context, ManagerB, StoreB, RoleConstants.StoreManager, "manager-b-237@test.local");
        EnsureStaff(context, WarehouseStaff, StoreA, RoleConstants.AccountantWarehouse, "warehouse-237@test.local");
        EnsureStaff(context, SalesStaff, StoreA, RoleConstants.SalesStaff, "sales-237@test.local");
        context.StaffScopes.Add(new StaffScope
        {
            StaffId = WarehouseStaff,
            ScopeTypeId = (int)CafeChain.Application.Interfaces.Security.ScopeLevel.Store,
            ScopeRefId = StoreA
        });
        await context.SaveChangesAsync();
    }

    private static void EnsureStaff(CafeChain.Data.AppDbContext context, int staffId, int storeId, string roleName, string email)
    {
        var role = context.Roles.Local.FirstOrDefault(x => x.Name == roleName)
            ?? context.Roles.Single(x => x.Name == roleName);
        var account = new Account { AccountId = 24000 + staffId, Email = email, PasswordHash = "x", Active = true, CreatedAt = DateTime.UtcNow };
        context.Accounts.Add(account);
        context.AccountRoles.Add(new AccountRole { AccountId = account.AccountId, RoleId = role.RoleId });
        context.Staffs.Add(new Staff { StaffId = staffId, AccountId = account.AccountId, StoreId = storeId, FullName = $"Staff {staffId}", Active = true, CreatedAt = DateTime.UtcNow });
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "CafeChain"))
                && Directory.Exists(Path.Combine(current.FullName, "CafeChain.Tests")))
                return current.FullName;
            current = current.Parent;
        }
        return Directory.GetCurrentDirectory();
    }
}
