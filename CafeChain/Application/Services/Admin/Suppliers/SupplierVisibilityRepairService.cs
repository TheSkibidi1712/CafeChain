using System.Globalization;
using System.Text;
using CafeChain.Application.DTOs.Admin.Suppliers;
using CafeChain.Application.Interfaces.Admin.Suppliers;
using CafeChain.Data;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Application.Services.Admin.Suppliers;

public sealed class SupplierVisibilityRepairService : ISupplierVisibilityRepairService
{
    private readonly AppDbContext _context;

    public SupplierVisibilityRepairService(AppDbContext context)
    {
        _context = context;
    }

    public Task<SupplierVisibilityRepairReportDTO> DryRunAsync() => AnalyzeAsync(dryRun: true);

    public Task<SupplierVisibilityRepairReportDTO> RepairSafeAsync() => AnalyzeAsync(dryRun: false);

    private async Task<SupplierVisibilityRepairReportDTO> AnalyzeAsync(bool dryRun)
    {
        var suppliers = await _context.Suppliers
            .AsNoTracking()
            .Select(x => new SupplierSnapshot(
                x.SupplierId,
                x.Code,
                x.Name,
                x.Active,
                x.SupplierStores.Any(link => link.Active),
                x.Phones.Where(phone => phone.IsPrimary)
                    .Select(phone => phone.PhoneNumber)
                    .FirstOrDefault(),
                x.Contacts.Where(contact => contact.IsPrimary)
                    .Select(contact => contact.Email)
                    .FirstOrDefault()))
            .ToListAsync();

        var supplierIdsWithDownstreamReferences = (await _context.IngredientSuppliers
                .AsNoTracking()
                .Select(x => x.SupplierId)
                .Concat(_context.PurchaseOrders.AsNoTracking().Select(x => x.SupplierId))
                .Concat(_context.BranchReceipts.AsNoTracking()
                    .Where(x => x.SupplierId.HasValue)
                    .Select(x => x.SupplierId!.Value))
                .Distinct()
                .ToListAsync())
            .ToHashSet();

        var duplicateGroups = suppliers
            .GroupBy(BuildIdentityFingerprint, StringComparer.Ordinal)
            .Where(group => group.Key.Length > 0 && group.Count() > 1)
            .SelectMany(group => group.Select(item => new
            {
                item.SupplierId,
                OtherIds = group
                    .Where(other => other.SupplierId != item.SupplierId)
                    .Select(other => other.SupplierId)
                    .OrderBy(id => id)
                    .ToArray()
            }))
            .ToDictionary(x => x.SupplierId, x => (IReadOnlyList<int>)x.OtherIds);

        var findings = suppliers
            .Where(x => !x.HasActiveStoreCoverage || duplicateGroups.ContainsKey(x.SupplierId))
            .OrderBy(x => x.SupplierId)
            .Select(x =>
            {
                var possibleDuplicates = duplicateGroups.GetValueOrDefault(x.SupplierId)
                                         ?? Array.Empty<int>();
                var requiresManualReview = possibleDuplicates.Count > 0;
                return new SupplierVisibilityFindingDTO
                {
                    SupplierId = x.SupplierId,
                    Code = x.Code,
                    Name = x.Name,
                    Active = x.Active,
                    HasActiveStoreCoverage = x.HasActiveStoreCoverage,
                    HasDownstreamReferences = supplierIdsWithDownstreamReferences.Contains(x.SupplierId),
                    RequiresManualReview = requiresManualReview,
                    PossibleDuplicateSupplierIds = possibleDuplicates,
                    Resolution = requiresManualReview
                        ? "Có dấu hiệu trùng thông tin nhận diện; cần Owner xem xét, không tự động gộp hoặc xóa."
                        : "Không cần sửa dữ liệu. Nhà cung cấp là dữ liệu master toàn chuỗi và có thể chưa cấu hình phạm vi phục vụ."
                };
            })
            .ToList();

        // Query alignment repairs visibility. No safe row mutation is needed or allowed here.
        return new SupplierVisibilityRepairReportDTO
        {
            DryRun = dryRun,
            SupplierCount = suppliers.Count,
            LegacyHiddenCount = suppliers.Count(x => !x.HasActiveStoreCoverage),
            SafeChangesApplied = 0,
            Findings = findings
        };
    }

    private static string BuildIdentityFingerprint(SupplierSnapshot supplier)
    {
        var name = NormalizeText(supplier.Name);
        var phone = NormalizePhone(supplier.PrimaryPhone);
        var email = supplier.PrimaryEmail?.Trim().ToLowerInvariant() ?? "";
        if (name.Length == 0 || phone.Length == 0 && email.Length == 0) return "";
        return $"{name}|{phone}|{email}";
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(character)) result.Append(char.ToUpperInvariant(character));
        }
        return result.ToString();
    }

    private static string NormalizePhone(string? value) =>
        new((value ?? "").Where(char.IsDigit).ToArray());

    private sealed record SupplierSnapshot(
        int SupplierId,
        string Code,
        string Name,
        bool Active,
        bool HasActiveStoreCoverage,
        string? PrimaryPhone,
        string? PrimaryEmail);
}
