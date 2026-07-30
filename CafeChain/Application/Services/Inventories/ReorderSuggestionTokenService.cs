using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CafeChain.Application.DTOs.Admin.Procurement;
using CafeChain.Application.Interfaces.Inventories;
using Microsoft.AspNetCore.DataProtection;

namespace CafeChain.Application.Services.Inventories;

public sealed class ReorderSuggestionTokenService : IReorderSuggestionTokenService
{
    private const string Purpose = "CafeChain.ReorderSuggestion.Confirmation.v1";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDataProtector _protector;
    private readonly TimeProvider _clock;

    public ReorderSuggestionTokenService(
        IDataProtectionProvider dataProtectionProvider,
        TimeProvider clock)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _clock = clock;
    }

    public string Issue(
        int staffId,
        int storeId,
        int ingredientId,
        int analysisWindowDays,
        string calculationVersion,
        string decisionFingerprint)
    {
        if (staffId <= 0 || storeId <= 0 || ingredientId <= 0)
            throw new ArgumentOutOfRangeException(nameof(staffId));
        if (analysisWindowDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(analysisWindowDays));
        if (string.IsNullOrWhiteSpace(calculationVersion)
            || string.IsNullOrWhiteSpace(decisionFingerprint))
            throw new ArgumentException("Calculation version and fingerprint are required.");

        var now = _clock.GetUtcNow().UtcDateTime;
        var payload = new ReorderSuggestionTokenPayload
        {
            IssuedToStaffId = staffId,
            StoreId = storeId,
            IngredientId = ingredientId,
            AnalysisWindowDays = analysisWindowDays,
            IssuedAtUtc = now,
            ExpiresAtUtc = now.Add(Lifetime),
            CalculationVersion = calculationVersion.Trim(),
            DecisionFingerprint = decisionFingerprint.Trim()
        };
        return _protector.Protect(JsonSerializer.Serialize(payload, JsonOptions));
    }

    public ReorderSuggestionTokenReadResult Read(
        string token,
        int expectedStaffId,
        int expectedStoreId,
        int expectedIngredientId)
    {
        if (string.IsNullOrWhiteSpace(token)
            || token.Length > 4096
            || expectedStaffId <= 0
            || expectedStoreId <= 0
            || expectedIngredientId <= 0)
        {
            return Invalid();
        }

        try
        {
            var json = _protector.Unprotect(token.Trim());
            var payload = JsonSerializer.Deserialize<ReorderSuggestionTokenPayload>(json, JsonOptions);
            if (payload == null
                || payload.IssuedToStaffId != expectedStaffId
                || payload.StoreId != expectedStoreId
                || payload.IngredientId != expectedIngredientId
                || payload.AnalysisWindowDays <= 0
                || string.IsNullOrWhiteSpace(payload.CalculationVersion)
                || string.IsNullOrWhiteSpace(payload.DecisionFingerprint))
            {
                return Invalid();
            }

            if (_clock.GetUtcNow().UtcDateTime > payload.ExpiresAtUtc)
            {
                return new ReorderSuggestionTokenReadResult(
                    false,
                    true,
                    payload,
                    ReorderSuggestionConfirmationErrorCodes.SuggestionExpired);
            }

            return new ReorderSuggestionTokenReadResult(true, false, payload);
        }
        catch (CryptographicException)
        {
            return Invalid();
        }
        catch (JsonException)
        {
            return Invalid();
        }
    }

    public string ComputeDecisionFingerprint(ReorderSuggestionDecisionFingerprintDto decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var canonical = new
        {
            decision.StoreId,
            decision.IngredientId,
            decision.CalculationVersion,
            decision.OnHandQuantity,
            decision.ReservedQuantity,
            decision.AvailableStock,
            decision.MinimumStock,
            decision.AverageDailyConsumption,
            decision.LeadTimeDays,
            decision.ReorderPoint,
            decision.IncomingQuantity,
            decision.RawDemand,
            decision.ProcurementCoveredQuantity,
            decision.RemainingDemand,
            decision.SuggestedPackageQuantity,
            decision.FinalSuggestedQuantity,
            decision.IngredientSupplierId,
            decision.SupplierId,
            decision.SupplierName,
            decision.PackageUnitId,
            decision.PackageBaseQuantity,
            decision.MinimumOrderPackageCount,
            decision.SupplierPrice,
            decision.PriceEffectiveAtUtc,
            decision.EstimatedCost,
            decision.SuggestionStatus,
            decision.ActiveRestockRequestId,
            ReasonCodes = decision.ReasonCodes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray()
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(canonical, JsonOptions);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static ReorderSuggestionTokenReadResult Invalid() =>
        new(
            false,
            false,
            null,
            ReorderSuggestionConfirmationErrorCodes.SuggestionChanged);
}
