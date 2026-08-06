using System.Security.Claims;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Actor;
using CafeChain.Application.DTOs.Admin.Permissions;
using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.Interfaces.Admin.Actor;
using CafeChain.Application.Interfaces.Admin.Permissions;
using CafeChain.Application.Interfaces.Admin.Suppliers;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Interfaces.Security;
using CafeChain.Application.Results;
using CafeChain.Application.Exceptions;
using CafeChain.Application.Services.Admin.Suppliers;
using CafeChain.Application.Services.Inventories;
using CafeChain.Areas.Admin.Controllers;
using CafeChain.Data;
using CafeChain.Infrastrusture.Repositories.Admin.Suppliers;
using CafeChain.Models.Inventories.Suppliers;
using CafeChain.Models.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CafeChain.Tests;

public sealed class SupplierLifecycleVisibilityIssue324Tests : IntegrationTestBase
{
    [Fact]
    public async Task CreatedSupplierWithoutStoreLink_AppearsOnMasterListAndDetail()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);
        var id = await service.CreateAsync(NewSupplier("NCC lifecycle", "0909000324", "0319000324"), 91);

        var page = await service.GetPagedAsync("NCC lifecycle", null, 1, 20);
        var detail = await service.GetByIdAsync(id);

        var item = Assert.Single(page.Items);
        Assert.Equal(id, item.SupplierId);
        Assert.Equal(0, item.ActiveStoreCount);
        Assert.NotNull(detail);
        Assert.Equal(id, detail!.SupplierId);
    }

    [Fact]
    public async Task SupplierMasterController_DoesNotUseStoreCoverageAsListOrDetailVisibility()
    {
        var service = new Mock<IAdminSupplierService>(MockBehavior.Strict);
        service.Setup(x => x.GetPagedAsync(null, null, 1, 20, null))
            .ReturnsAsync(new AdminSupplierIndexPageDTO());
        service.Setup(x => x.GetByIdAsync(324, null))
            .ReturnsAsync(new AdminSupplierDetailDTO
            {
                SupplierId = 324,
                Code = "NCC00324",
                Name = "NCC master"
            });

        var actor = new Mock<IAdminActorContextAccessor>();
        actor.Setup(x => x.Get(It.IsAny<ClaimsPrincipal>())).Returns(new AdminActorContext
        {
            AccountId = 91,
            StaffId = 91,
            StoreId = 1
        });
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.GetAllowedStoresAsync(91)).ReturnsAsync(new List<Store>
        {
            new() { StoreId = 1, Name = "Store 1", Active = true }
        });
        var controller = new AdminSupplierController(
            service.Object,
            actor.Object,
            scope.Object,
            NullLogger<AdminSupplierController>.Instance);
        AttachUser(controller);

        Assert.IsType<ViewResult>(await controller.Index(null, null, 1, 20));
        var detailResult = Assert.IsType<JsonResult>(await controller.GetById(324));

        Assert.NotNull(detailResult.Value);
        service.Verify(x => x.GetPagedAsync(null, null, 1, 20, null), Times.Once);
        service.Verify(x => x.GetByIdAsync(324, null), Times.Once);
    }

    [Fact]
    public async Task CreatedSupplier_IsSearchableByPrimaryContactEmail()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);
        var id = await service.CreateAsync(NewSupplier(
            "NCC email search",
            "0909000325",
            "0319000325",
            "supplier.324@cafechain.test"), 91);

        var page = await service.GetPagedAsync("supplier.324@cafechain.test", null, 1, 20);

        Assert.Contains(page.Items, x => x.SupplierId == id);
    }

    [Fact]
    public async Task DuplicateSupplier_ReturnsExistingSupplierIdentityThatLoadsDetail()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);
        var existingId = await service.CreateAsync(
            NewSupplier("NCC tax owner", "0909000326", "0319000326"), 91);

        var error = await Assert.ThrowsAsync<SupplierDomainException>(() => service.CreateAsync(
            NewSupplier("NCC tax duplicate", "0909000327", "0319000326"), 91));
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(error.DataPayload));

        Assert.Equal(SupplierIdentityConstants.TaxCodeDuplicate, error.Code);
        Assert.Equal(
            existingId,
            payload.RootElement.GetProperty("existingSupplier").GetProperty("SupplierId").GetInt32());
        Assert.NotNull(await service.GetByIdAsync(existingId));
    }

    [Fact]
    public async Task SupplierStoreCoverageMutation_RejectsStoreOutsideActorScope()
    {
        var service = new Mock<IAdminSupplierService>(MockBehavior.Strict);
        var actor = new Mock<IAdminActorContextAccessor>();
        actor.Setup(x => x.Get(It.IsAny<ClaimsPrincipal>())).Returns(new AdminActorContext
        {
            AccountId = 91,
            StaffId = 91,
            StoreId = 1
        });
        var scope = new Mock<IScopeAuthorizationService>();
        scope.Setup(x => x.GetAllowedStoresAsync(91)).ReturnsAsync(new List<Store>
        {
            new() { StoreId = 1, Name = "Store 1", Active = true }
        });
        var controller = new AdminSupplierController(
            service.Object,
            actor.Object,
            scope.Object,
            NullLogger<AdminSupplierController>.Instance);
        AttachUser(controller);

        var result = Assert.IsType<JsonResult>(await controller.SaveSupplierStore(new AdminSupplierStoreSaveDTO
        {
            SupplierId = 324,
            StoreId = 2,
            Active = true
        }));

        Assert.Equal(StatusCodes.Status403Forbidden, controller.Response.StatusCode);
        Assert.NotNull(result.Value);
        service.Verify(x => x.SaveSupplierStoreAsync(It.IsAny<AdminSupplierStoreSaveDTO>()), Times.Never);
    }

    [Fact]
    public async Task SupplierVisibilityRepair_DryRunAndSafeRepairFlagDuplicatesWithoutMutation()
    {
        await using var context = CreateDbContext();
        var first = NewEntity(321, "NCC00321", "Nhà cung cấp trùng", "0909000328", "dup@cafechain.test");
        var second = NewEntity(322, "NCC00322", "Nha cung cap trung", "0909000328", "DUP@cafechain.test");
        context.Suppliers.AddRange(first, second);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var service = new SupplierVisibilityRepairService(context);

        var before = await context.Suppliers.AsNoTracking().CountAsync();
        var dryRun = await service.DryRunAsync();
        var firstRepair = await service.RepairSafeAsync();
        var secondRepair = await service.RepairSafeAsync();

        Assert.True(dryRun.LegacyHiddenCount >= 2);
        Assert.All(
            dryRun.Findings.Where(x => x.SupplierId is 321 or 322),
            finding => Assert.True(finding.RequiresManualReview));
        Assert.Equal(2, dryRun.Findings.Count(x => x.SupplierId is 321 or 322));
        Assert.Equal(0, firstRepair.SafeChangesApplied);
        Assert.Equal(0, secondRepair.SafeChangesApplied);
        Assert.Equal(before, await context.Suppliers.AsNoTracking().CountAsync());
        Assert.Equal(
            firstRepair.Findings.Select(x => (x.SupplierId, x.Resolution)),
            secondRepair.Findings.Select(x => (x.SupplierId, x.Resolution)));
    }

    [Fact]
    public void SupplierCreateUi_OpensReturnedSupplierIdentityWithoutReloadingFilteredList()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "CafeChain",
            "wwwroot",
            "js",
            "Admin",
            "Supplier",
            "supplier.js"));
        var submitStart = script.IndexOf("async function submitCreate", StringComparison.Ordinal);
        var submitEnd = script.IndexOf(
            "$('#createSupplierForm')?.addEventListener",
            submitStart,
            StringComparison.Ordinal);
        var submitCreate = script[submitStart..submitEnd];

        Assert.Contains("const result = await api('/Create'", submitCreate, StringComparison.Ordinal);
        Assert.Contains("openSupplierId", submitCreate, StringComparison.Ordinal);
        Assert.Contains("target.searchParams.set('created', '1')", submitCreate, StringComparison.Ordinal);
        Assert.Contains("window.location.assign", submitCreate, StringComparison.Ordinal);
        Assert.DoesNotContain("window.location.reload()", submitCreate, StringComparison.Ordinal);
        Assert.Contains("Đã tạo nhà cung cấp ${state.detail.name}.", script, StringComparison.Ordinal);
        Assert.Contains("window.history.replaceState", script, StringComparison.Ordinal);
    }

    private static AdminSupplierService CreateService(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(
            context,
            NullLogger<PhysicalUnitConversionService>.Instance);
        IIngredientSupplierPackageValidator validator =
            new IngredientSupplierPackageValidator(context, physical);
        return new AdminSupplierService(new AdminSupplierRepository(context), context, validator);
    }

    private static AdminSupplierCreateDTO NewSupplier(
        string name,
        string phone,
        string taxCode,
        string? email = null) => new()
    {
        Name = name,
        TaxCode = taxCode,
        Address = $"Address {name}",
        PrimaryPhone = phone,
        PrimaryContactName = $"Contact {name}",
        PrimaryContactPhone = phone,
        PrimaryContactEmail = email
    };

    private static Supplier NewEntity(
        int id,
        string code,
        string name,
        string phone,
        string email)
    {
        var now = DateTime.UtcNow;
        return new Supplier
        {
            SupplierId = id,
            Code = code,
            Name = name,
            Active = true,
            CreatedAt = now,
            UpdatedAt = now,
            Phones = new List<SupplierPhone>
            {
                new() { PhoneNumber = phone, IsPrimary = true }
            },
            Contacts = new List<SupplierContact>
            {
                new()
                {
                    Name = $"Liên hệ {name}",
                    PhoneNumber = phone,
                    Email = email,
                    IsPrimary = true
                }
            }
        };
    }

    private static void AttachUser(Controller controller)
    {
        var permissionService = new Mock<IAdminPermissionService>();
        permissionService
            .Setup(x => x.HasPermissionAsync(91, It.IsAny<string>(), It.IsAny<int?>()))
            .ReturnsAsync((int accountId, string permissionCode, int? _) =>
                ServiceResult<PermissionDecisionDto>.Success(new PermissionDecisionDto
                {
                    AccountId = accountId,
                    PermissionCode = permissionCode,
                    Allowed = true
                }));
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "91"),
            new Claim("StaffId", "91"),
            new Claim("StoreId", "1")
        }, "Test"));
        var httpContext = new DefaultHttpContext
        {
            User = user,
            RequestServices = new ServiceCollection()
                .AddSingleton(permissionService.Object)
                .BuildServiceProvider()
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFile = "")
    {
        var testProject = Directory.GetParent(Path.GetDirectoryName(sourceFile)!)!;
        return testProject.FullName;
    }
}
