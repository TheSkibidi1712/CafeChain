using CafeChain.Application.Constants;
using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.Exceptions;
using CafeChain.Application.Interfaces.Inventories;
using CafeChain.Application.Services.Admin.Suppliers;
using CafeChain.Application.Services.Inventories;
using CafeChain.Data;
using CafeChain.Infrastrusture.Repositories.Admin.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CafeChain.Tests;

public sealed class SupplierTaxCodeDuplicateContractTests : IntegrationTestBase
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void TaxCode_Blank_NormalizesToNull(string? input, string? expected) =>
        Assert.Equal(expected, SupplierTaxCodeNormalizer.Normalize(input));

    [Theory]
    [InlineData("0312345679", "0312345679")]
    [InlineData("0312 345 679", "0312345679")]
    [InlineData("0312345679001", "0312345679-001")]
    [InlineData("0312 345 679 001", "0312345679-001")]
    [InlineData("0312345679-001", "0312345679-001")]
    [InlineData("0312 345 679-001", "0312345679-001")]
    [InlineData("0312345679‑001", "0312345679-001")]
    public void TaxCode_ValidFriendlyInput_NormalizesToCanonical(string input, string expected) =>
        Assert.Equal(expected, SupplierTaxCodeNormalizer.Normalize(input));

    [Theory]
    [InlineData("031234567")]
    [InlineData("03123456790")]
    [InlineData("031234567A")]
    [InlineData("0312345679_001")]
    public void TaxCode_InvalidInput_Rejected(string input)
    {
        var error = Assert.Throws<SupplierDomainException>(() => SupplierTaxCodeNormalizer.Normalize(input));
        Assert.Equal(SupplierIdentityConstants.TaxCodeInvalid, error.Code);
    }

    [Fact]
    public void TaxCode_LeadingZero_Preserved() =>
        Assert.StartsWith("0", SupplierTaxCodeNormalizer.Normalize("0312345679"));

    [Fact]
    public async Task Supplier_Create_DuplicateTaxCodeRejected()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);
        await service.CreateAsync(NewSupplier("Tax owner", "0911111001", "0312345679"), 91);

        var error = await Assert.ThrowsAsync<SupplierDomainException>(() =>
            service.CreateAsync(NewSupplier("Other supplier", "0911111002", "0312 345 679"), 91));

        Assert.Equal(SupplierIdentityConstants.TaxCodeDuplicate, error.Code);
        Assert.Equal(1, await context.Suppliers.CountAsync(x => x.TaxCode == "0312345679"));
    }

    [Fact]
    public async Task Supplier_Create_DuplicateInactiveTaxCodeRejected()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);
        var ownerId = await service.CreateAsync(NewSupplier("Inactive owner", "0911111011", "0312345680"), 91);
        var owner = await context.Suppliers.FindAsync(ownerId);
        owner!.Active = false;
        await context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<SupplierDomainException>(() =>
            service.CreateAsync(NewSupplier("Replacement", "0911111012", "0312345680"), 91));

        Assert.Equal(SupplierIdentityConstants.TaxCodeDuplicate, error.Code);
    }

    [Fact]
    public async Task Supplier_Edit_OwnTaxCodeAllowed_AndOtherSupplierRejected()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);
        var firstId = await service.CreateAsync(NewSupplier("Edit owner", "0911111021", "0312345681"), 91);
        var secondId = await service.CreateAsync(NewSupplier("Edit other", "0911111022", "0312345682"), 91);

        var first = await context.Suppliers.AsNoTracking().SingleAsync(x => x.SupplierId == firstId);
        await service.UpdateAsync(Update(first, "0312345681"), 91);

        var second = await context.Suppliers.AsNoTracking().SingleAsync(x => x.SupplierId == secondId);
        var error = await Assert.ThrowsAsync<SupplierDomainException>(() =>
            service.UpdateAsync(Update(second, "0312345681"), 91));
        Assert.Equal(SupplierIdentityConstants.TaxCodeDuplicate, error.Code);
    }

    [Fact]
    public async Task Supplier_Edit_RequiresRowVersion()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);
        var id = await service.CreateAsync(NewSupplier("Version owner", "0911111031", null), 91);
        var entity = await context.Suppliers.AsNoTracking().SingleAsync(x => x.SupplierId == id);
        var request = Update(entity, null);
        request.RowVersion = "";

        var error = await Assert.ThrowsAsync<SupplierDomainException>(() => service.UpdateAsync(request, 91));
        Assert.Equal(SupplierIdentityConstants.StaleVersion, error.Code);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("hotline")]
    [InlineData("contactPhone")]
    [InlineData("address")]
    [InlineData("email")]
    public async Task Supplier_DeterministicIdentitySignal_ReturnsWarning(string signal)
    {
        await using var context = CreateDbContext();
        var existing = await context.Suppliers
            .Include(x => x.Phones)
            .Include(x => x.Contacts)
            .OrderBy(x => x.SupplierId)
            .FirstAsync();
        var request = NewSupplier($"Unique {Guid.NewGuid():N}", "0987654101", null);
        request.Address = $"Address {Guid.NewGuid():N}";
        request.PrimaryContactPhone = "0987654102";
        request.PrimaryContactEmail = $"{Guid.NewGuid():N}@example.test";

        switch (signal)
        {
            case "name": request.Name = existing.Name!; break;
            case "hotline": request.PrimaryPhone = existing.Phones.First(x => x.IsPrimary).PhoneNumber!; break;
            case "contactPhone": request.PrimaryContactPhone = existing.Contacts.First().PhoneNumber; break;
            case "address": request.Address = existing.Address; break;
            case "email":
                existing.Contacts.First().Email = "duplicate@example.test";
                await context.SaveChangesAsync();
                request.PrimaryContactEmail = "DUPLICATE@example.test";
                break;
        }

        var before = await context.Suppliers.CountAsync();
        var error = await Assert.ThrowsAsync<SupplierDomainException>(() => CreateService(context).CreateAsync(request, 91));
        Assert.Equal(SupplierIdentityConstants.PossibleDuplicate, error.Code);
        Assert.IsType<AdminSupplierDuplicateWarningDTO>(error.DataPayload);
        Assert.Equal(before, await context.Suppliers.CountAsync());
    }

    [Fact]
    public async Task Supplier_SoftWarning_ValidConfirmRequiresReason_ThenCreatesAndAudits()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);
        var existing = await context.Suppliers.OrderBy(x => x.SupplierId).FirstAsync();
        var request = NewSupplier(existing.Name!, "0911111041", null);
        request.Address = "A distinct address";

        var warningError = await Assert.ThrowsAsync<SupplierDomainException>(() => service.CreateAsync(request, 91));
        var warning = Assert.IsType<AdminSupplierDuplicateWarningDTO>(warningError.DataPayload);
        request.DuplicateWarningId = warning.WarningId;

        Assert.False(await service.IsDuplicateWarningValidAsync(request, 91));

        var reasonError = await Assert.ThrowsAsync<SupplierDomainException>(() => service.CreateAsync(request, 91));
        Assert.Equal(SupplierIdentityConstants.OverrideReasonRequired, reasonError.Code);

        request.DuplicateOverrideReason = "Hai pháp nhân khác nhau nhưng tên thương mại giống nhau";
        Assert.True(await service.IsDuplicateWarningValidAsync(request, 91));
        Assert.Equal(SupplierIdentityConstants.WarningPending,
            (await context.SupplierDuplicateWarnings.SingleAsync(x => x.PublicId == warning.WarningId)).Status);
        var createdId = await service.CreateAsync(request, 91);

        Assert.True(createdId > 0);
        Assert.Equal(SupplierIdentityConstants.WarningUsed,
            (await context.SupplierDuplicateWarnings.SingleAsync(x => x.PublicId == warning.WarningId)).Status);
        Assert.True(await context.AuditLogs.AnyAsync(x =>
            x.RecordId == createdId && x.Action == "SUPPLIER_DUPLICATE_OVERRIDE" && x.UserId == 91));
    }

    [Fact]
    public async Task Supplier_SoftWarning_PayloadChangedOrFingerprintChangedRejected()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);
        var existing = await context.Suppliers.OrderBy(x => x.SupplierId).FirstAsync();
        var request = NewSupplier(existing.Name!, "0911111051", null);
        var warningError = await Assert.ThrowsAsync<SupplierDomainException>(() => service.CreateAsync(request, 91));
        request.DuplicateWarningId = Assert.IsType<AdminSupplierDuplicateWarningDTO>(warningError.DataPayload).WarningId;
        request.DuplicateOverrideReason = "Đã kiểm tra";
        request.Address = "Payload changed after warning";

        var error = await Assert.ThrowsAsync<SupplierDomainException>(() => service.CreateAsync(request, 91));
        Assert.Equal(SupplierIdentityConstants.WarningStale, error.Code);
    }

    [Fact]
    public async Task Supplier_SoftWarning_ExpiredOrAlreadyUsedRejected()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);
        var existing = await context.Suppliers.OrderBy(x => x.SupplierId).FirstAsync();
        var request = NewSupplier(existing.Name!, "0911111052", null);
        var warningError = await Assert.ThrowsAsync<SupplierDomainException>(() => service.CreateAsync(request, 91));
        var warningId = Assert.IsType<AdminSupplierDuplicateWarningDTO>(warningError.DataPayload).WarningId;
        request.DuplicateWarningId = warningId;
        request.DuplicateOverrideReason = "Đã kiểm tra";

        var warning = await context.SupplierDuplicateWarnings.SingleAsync(x => x.PublicId == warningId);
        warning.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1);
        await context.SaveChangesAsync();
        var expired = await Assert.ThrowsAsync<SupplierDomainException>(() => service.CreateAsync(request, 91));
        Assert.Equal(SupplierIdentityConstants.WarningInvalid, expired.Code);

        warning.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5);
        await context.SaveChangesAsync();
        await service.CreateAsync(request, 91);
        var reused = await Assert.ThrowsAsync<SupplierDomainException>(() => service.CreateAsync(request, 91));
        Assert.Equal(SupplierIdentityConstants.WarningInvalid, reused.Code);
    }

    [Fact]
    public async Task Supplier_Edit_CanClearTaxCode_AndWritesAudit()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);
        var id = await service.CreateAsync(NewSupplier("Clear tax", "0911111053", "0312345693"), 91);
        var supplier = await context.Suppliers.AsNoTracking().SingleAsync(x => x.SupplierId == id);

        await service.UpdateAsync(Update(supplier, "   "), 91);

        Assert.Null((await context.Suppliers.AsNoTracking().SingleAsync(x => x.SupplierId == id)).TaxCode);
        Assert.True(await context.AuditLogs.AnyAsync(x =>
            x.RecordId == id && x.Action == "SUPPLIER_TAX_CODE_UPDATED" && x.UserId == 91));
    }

    [Fact]
    public async Task Supplier_SoftWarning_ServerRecheckDetectsNewHardDuplicate()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);
        var existing = await context.Suppliers.OrderBy(x => x.SupplierId).FirstAsync();
        var request = NewSupplier(existing.Name!, "0911111061", "0312345690");
        var warningError = await Assert.ThrowsAsync<SupplierDomainException>(() => service.CreateAsync(request, 91));
        request.DuplicateWarningId = Assert.IsType<AdminSupplierDuplicateWarningDTO>(warningError.DataPayload).WarningId;
        request.DuplicateOverrideReason = "Đã kiểm tra";

        context.Suppliers.Add(new CafeChain.Models.Inventories.Suppliers.Supplier
        {
            Code = "NCC-HARD-RACE",
            Name = "Hard race owner",
            TaxCode = "0312345690",
            Active = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<SupplierDomainException>(() => service.CreateAsync(request, 91));
        Assert.Equal(SupplierIdentityConstants.TaxCodeDuplicate, error.Code);
    }

    [Fact]
    public async Task Supplier_Search_ByCanonicalTaxCode()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);
        await service.CreateAsync(NewSupplier("Search tax", "0911111071", "0312345691"), 91);

        var results = await service.GetAllAsync("0312345691", null);
        Assert.Contains(results, x => x.TaxCode == "0312345691");
    }

    [Fact]
    public async Task AIImport_SupplierPreview_ReusesReadOnlyDuplicatePolicyWithoutCreatingWarningToken()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context);
        var existing = NewSupplier("Preview policy owner", "0911111081", "0312345692");
        await service.CreateAsync(existing, 91);

        var incoming = NewSupplier("Different supplier", "0911111082", "0312345693");
        incoming.Address = existing.Address;
        var matches = await service.FindDuplicateMatchesAsync(incoming);
        var batch = await service.FindDuplicateMatchesBatchAsync(new[] { incoming });

        var match = Assert.Single(matches);
        Assert.Contains("Địa chỉ", match.MatchedSignals);
        Assert.Equal(matches.Select(x => x.SupplierId), Assert.Single(batch).Select(x => x.SupplierId));
        Assert.Empty(await context.SupplierDuplicateWarnings.ToListAsync());
    }

    private static AdminSupplierService CreateService(AppDbContext context)
    {
        var physical = new PhysicalUnitConversionService(
            context, NullLogger<PhysicalUnitConversionService>.Instance);
        IIngredientSupplierPackageValidator validator =
            new IngredientSupplierPackageValidator(context, physical);
        return new AdminSupplierService(new AdminSupplierRepository(context), context, validator);
    }

    private static AdminSupplierCreateDTO NewSupplier(string name, string phone, string? taxCode) => new()
    {
        Name = name,
        TaxCode = taxCode,
        Address = $"Address {name}",
        PrimaryPhone = phone,
        PrimaryContactName = $"Contact {name}",
        PrimaryContactPhone = phone
    };

    private static AdminSupplierUpdateDTO Update(
        CafeChain.Models.Inventories.Suppliers.Supplier supplier,
        string? taxCode) => new()
    {
        SupplierId = supplier.SupplierId,
        Name = supplier.Name!,
        TaxCode = taxCode,
        Address = supplier.Address,
        Note = supplier.Note,
        Active = supplier.Active,
        RowVersion = Convert.ToBase64String(supplier.RowVersion)
    };
}
