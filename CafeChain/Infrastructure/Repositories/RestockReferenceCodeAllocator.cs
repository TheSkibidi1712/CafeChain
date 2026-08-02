using CafeChain.Data;

namespace CafeChain.Infrastrusture.Repositories;

internal static class RestockReferenceCodeAllocator
{
    private const string CounterPrefix = "RESTOCK";

    public static async Task<string> NextAsync(
        AppDbContext context,
        int storeId,
        DateTime createdAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(storeId);

        var sequence = await DocumentNumberCounterAllocator.NextAsync(
            context,
            $"{CounterPrefix}:S{storeId}",
            createdAtUtc);

        return $"RR-S{storeId:D4}-{createdAtUtc:yyyyMMdd}-{sequence:D5}";
    }
}
