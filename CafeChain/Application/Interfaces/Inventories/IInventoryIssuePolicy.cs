using CafeChain.Application.DTOs.Inventories;

namespace CafeChain.Application.Interfaces.Inventories;

public interface IInventoryIssuePolicy
{
    Task<InventoryIssueDecision> EvaluateAsync(
        InventoryIssueRequest request,
        CancellationToken cancellationToken = default);
}
