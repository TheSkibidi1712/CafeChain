using System.Data;
using CafeChain.Application.Constants;
using CafeChain.Data;
using CafeChain.Infrastructure.Interfaces.Admin.Procurement;
using CafeChain.Models.Enums.Unit;
using CafeChain.Models.Inventories.Stock;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CafeChain.Infrastructure.Repositories.Admin.Procurement;

public sealed class ReorderSuggestionConfirmationRepository
    : IReorderSuggestionConfirmationRepository
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

    public ReorderSuggestionConfirmationRepository(AppDbContext context) =>
        _context = context;

    public async Task BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
            throw new InvalidOperationException("A confirmation transaction is already active.");
        _transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
    }

    public async Task CommitTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
            return;
        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
        _context.ChangeTracker.Clear();
    }

    public async Task AcquireIngredientLockAsync(
        int storeId,
        int ingredientId,
        CancellationToken cancellationToken = default)
    {
        if (_context.Database.ProviderName?.Contains(
                "SqlServer",
                StringComparison.OrdinalIgnoreCase) != true)
            return;
        if (_transaction == null)
            throw new InvalidOperationException("Transaction-scoped lock requires an active transaction.");

        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = _transaction.GetDbTransaction();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 10000;
            SELECT @result;
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@resource";
        parameter.Value = $"CafeChain:Reorder:{storeId}:{ingredientId}";
        command.Parameters.Add(parameter);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value == null || Convert.ToInt32(value) < 0)
            throw new TimeoutException("Không thể khóa nhu cầu nhập hàng để xác nhận.");
    }

    public Task<RestockRequest?> GetActiveRequestAsync(
        int storeId,
        int ingredientId,
        CancellationToken cancellationToken = default) =>
        _context.RestockRequests
            .Include(x => x.ProcurementUnit)
            .Where(x => x.StoreId == storeId
                && x.IngredientId == ingredientId
                && RestockRequestStatuses.ActiveValues.Contains(x.Status))
            .OrderBy(x => x.RestockRequestId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ReorderUnitRow?> GetIngredientBaseUnitAsync(
        int ingredientId,
        CancellationToken cancellationToken = default) =>
        _context.Ingredients.AsNoTracking()
            .Where(x => x.IngredientId == ingredientId && x.Active)
            .Select(x => new ReorderUnitRow(
                x.BaseUnit.UnitId,
                x.BaseUnit.UnitCode,
                x.BaseUnit.Type))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ReorderUnitRow?> GetCanonicalProcurementUnitAsync(
        UnitType type,
        CancellationToken cancellationToken = default)
    {
        var code = type switch
        {
            UnitType.KhoiLuong => ProcurementUnitCodes.Kilogram,
            UnitType.TheTich => ProcurementUnitCodes.Liter,
            UnitType.Dem => ProcurementUnitCodes.Piece,
            _ => string.Empty
        };
        return _context.Units.AsNoTracking()
            .Where(x => x.Active && x.Type == type && x.UnitCode == code)
            .Select(x => new ReorderUnitRow(x.UnitId, x.UnitCode, x.Type))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public void AddRequest(RestockRequest request) =>
        _context.RestockRequests.Add(request);

    public void AddTransition(RestockRequestTransition transition) =>
        _context.RestockRequestTransitions.Add(transition);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);

    public void ClearTracking() => _context.ChangeTracker.Clear();
}
