using CafeChain.Application.DTOs.Admin.Procurement;

namespace CafeChain.Application.Interfaces.Inventories;

public sealed record ReorderSuggestionTokenReadResult(
    bool IsValid,
    bool IsExpired,
    ReorderSuggestionTokenPayload? Payload,
    string? ErrorCode = null);

public interface IReorderSuggestionTokenService
{
    string Issue(
        int staffId,
        int storeId,
        int ingredientId,
        int analysisWindowDays,
        string calculationVersion,
        string decisionFingerprint);

    ReorderSuggestionTokenReadResult Read(
        string token,
        int expectedStaffId,
        int expectedStoreId,
        int expectedIngredientId);

    string ComputeDecisionFingerprint(ReorderSuggestionDecisionFingerprintDto decision);
}
