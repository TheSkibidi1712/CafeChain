using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Stock;

namespace CafeChain.Infrastructure.Interfaces.Admin.Procurement;

public sealed record ReorderUnitRow(
    int UnitId,
    string UnitCode,
    UnitType Type);

public interface IReorderSuggestionConfirmationRepository
{
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    Task AcquireIngredientLockAsync(
        int storeId,
        int ingredientId,
        CancellationToken cancellationToken = default);
    Task<RestockRequest?> GetActiveRequestAsync(
        int storeId,
        int ingredientId,
        CancellationToken cancellationToken = default);
    Task<ReorderUnitRow?> GetIngredientBaseUnitAsync(
        int ingredientId,
        CancellationToken cancellationToken = default);
    Task<ReorderUnitRow?> GetCanonicalProcurementUnitAsync(
        UnitType type,
        CancellationToken cancellationToken = default);
    void AddRequest(RestockRequest request);
    void AddTransition(RestockRequestTransition transition);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    void ClearTracking();
}
