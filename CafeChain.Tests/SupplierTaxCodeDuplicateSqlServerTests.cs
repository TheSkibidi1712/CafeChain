using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.Exceptions;
using CafeChain.Application.Services.Admin.Suppliers;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Infrastrusture.Repositories.Admin.Suppliers;
using CafeChain.Models.Inventories.Suppliers;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests;

[Trait("Category", "SqlServerIntegration")]
public sealed class SupplierTaxCodeDuplicateSqlServerTests : IAsyncLifetime
{
    private const string Database = "CafeChain_SupplierTaxCodeTests";
    private static string ConnectionString => SqlServerTestConnection.Create(Database);

    public async Task InitializeAsync()
    {
        try
        {
            await using var master = new SqlConnection(SqlServerTestConnection.MasterConnectionString());
            await master.OpenAsync();
            await using var command = master.CreateCommand();
            command.CommandText = $"IF DB_ID(N'{Database}') IS NULL CREATE DATABASE [{Database}];";
            await command.ExecuteNonQueryAsync();

            await using var context = CreateContext();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"BLOCKED_ON_SQL_SERVER: Supplier TaxCode database unavailable. Database={Database}. {ex.Message}", ex);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SqlServer_ConcurrentSameTaxCode_OneWinner_AndStableError()
    {
        const string taxCode = "0319999001";
        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();

        var results = await Task.WhenAll(
            TryCreateAsync(CreateService(firstContext), NewSupplier("Concurrent tax A", "0971000001", taxCode)),
            TryCreateAsync(CreateService(secondContext), NewSupplier("Concurrent tax B", "0971000002", taxCode)));

        Assert.Single(results.Where(x => x.Success));
        Assert.Single(results.Where(x => x.ErrorCode == SupplierIdentityConstants.TaxCodeDuplicate));
        await using var verify = CreateContext();
        Assert.Equal(1, await verify.Suppliers.CountAsync(x => x.TaxCode == taxCode));
    }

    [Fact]
    public async Task SqlServer_InactiveSupplierStillOwnsTaxCode()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var ownerId = await service.CreateAsync(NewSupplier("SQL inactive owner", "0971000011", "0319999002"), 1);
        var owner = await context.Suppliers.FindAsync(ownerId);
        owner!.Active = false;
        await context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<SupplierDomainException>(() =>
            service.CreateAsync(NewSupplier("SQL replacement", "0971000012", "0319999002"), 1));
        Assert.Equal(SupplierIdentityConstants.TaxCodeDuplicate, error.Code);
    }

    [Fact]
    public async Task SqlServer_EditRace_DoesNotDuplicateTaxCode()
    {
        int firstId;
        int secondId;
        await using (var seed = CreateContext())
        {
            var service = CreateService(seed);
            firstId = await service.CreateAsync(NewSupplier("SQL edit A", "0971000021", null), 1);
            secondId = await service.CreateAsync(NewSupplier("SQL edit B", "0971000022", null), 1);
        }

        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var first = await firstContext.Suppliers.AsNoTracking().SingleAsync(x => x.SupplierId == firstId);
        var second = await secondContext.Suppliers.AsNoTracking().SingleAsync(x => x.SupplierId == secondId);

        var results = await Task.WhenAll(
            TryUpdateAsync(CreateService(firstContext), Update(first, "0319999003")),
            TryUpdateAsync(CreateService(secondContext), Update(second, "0319999003")));

        Assert.Single(results.Where(x => x.Success));
        Assert.Single(results.Where(x => x.ErrorCode == SupplierIdentityConstants.TaxCodeDuplicate));
    }

    [Fact]
    public async Task SqlServer_SoftOverrideAndHardDuplicateRace_Blocked()
    {
        Guid warningId;
        var request = NewSupplier("Nhà cung cấp A", "0971000031", "0319999004");
        await using (var warningContext = CreateContext())
        {
            var error = await Assert.ThrowsAsync<SupplierDomainException>(() =>
                CreateService(warningContext).CreateAsync(request, 1));
            warningId = Assert.IsType<AdminSupplierDuplicateWarningDTO>(error.DataPayload).WarningId;
        }

        await using (var competingContext = CreateContext())
        {
            competingContext.Suppliers.Add(new Supplier
            {
                Code = "NCC-SQL-RACE",
                Name = "Competing owner",
                TaxCode = "0319999004",
                Active = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await competingContext.SaveChangesAsync();
        }

        request.DuplicateWarningId = warningId;
        request.DuplicateOverrideReason = "Đã kiểm tra";
        await using var confirmContext = CreateContext();
        var duplicate = await Assert.ThrowsAsync<SupplierDomainException>(() =>
            CreateService(confirmContext).CreateAsync(request, 1));
        Assert.Equal(SupplierIdentityConstants.TaxCodeDuplicate, duplicate.Code);
    }

    [Fact]
    public void SqlServer_ModelHasFilteredUniqueTaxCodeIndex()
    {
        using var context = CreateContext();
        var index = context.Model.FindEntityType(typeof(Supplier))!.GetIndexes()
            .Single(x => x.GetDatabaseName() == "UX_Suppliers_TaxCode");
        Assert.True(index.IsUnique);
        Assert.Equal("[TaxCode] IS NOT NULL", index.GetFilter());
    }

    private static async Task<(bool Success, string? ErrorCode)> TryCreateAsync(
        AdminSupplierService service,
        AdminSupplierCreateDTO request)
    {
        try
        {
            await service.CreateAsync(request, 1);
            return (true, null);
        }
        catch (SupplierDomainException ex)
        {
            return (false, ex.Code);
        }
    }

    private static async Task<(bool Success, string? ErrorCode)> TryUpdateAsync(
        AdminSupplierService service,
        AdminSupplierUpdateDTO request)
    {
        try
        {
            await service.UpdateAsync(request, 1);
            return (true, null);
        }
        catch (SupplierDomainException ex)
        {
            return (false, ex.Code);
        }
    }

    private static AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);

    private static AdminSupplierService CreateService(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(
            context, NullLogger<PhysicalUnitConversionService>.Instance);
        return new AdminSupplierService(
            new AdminSupplierRepository(context),
            context,
            new IngredientSupplierPackageValidator(context, physical));
    }

    private static AdminSupplierCreateDTO NewSupplier(string name, string phone, string? taxCode) => new()
    {
        Name = name,
        TaxCode = taxCode,
        Address = $"SQL address {name}",
        PrimaryPhone = phone,
        PrimaryContactName = $"Contact {name}",
        PrimaryContactPhone = phone
    };

    private static AdminSupplierUpdateDTO Update(Supplier supplier, string taxCode) => new()
    {
        SupplierId = supplier.SupplierId,
        Name = supplier.Name!,
        TaxCode = taxCode,
        Address = supplier.Address,
        Active = supplier.Active,
        RowVersion = Convert.ToBase64String(supplier.RowVersion)
    };
}
