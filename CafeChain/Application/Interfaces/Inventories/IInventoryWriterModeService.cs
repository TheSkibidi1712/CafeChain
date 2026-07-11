using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Results;

namespace CafeChain.Application.Interfaces.Inventories
{
    public interface IInventoryWriterModeService
    {
        Task<ServiceResult<InventoryWriterModeSnapshot>> AcquireSnapshotAsync(int storeId);
        bool IsSnapshotValidForCurrentTransaction(InventoryWriterModeSnapshot snapshot, int storeId);
        ServiceResult EnsureLegacyBtpWriteAllowed(InventoryWriterModeSnapshot snapshot, int storeId);
        Task<ServiceResult<InventoryWriterModeStatusDto>> GetStatusAsync(int storeId);
        Task<InventoryWriterReadinessReport> EvaluateReadinessAsync(int storeId);
        Task<InventoryWriterModeTransitionResult> TransitionAsync(InventoryWriterModeTransitionRequest request);
    }

    public interface IInventoryWriterCapabilityProvider
    {
        InventoryWriterCapabilityStatus GetStatus();
    }
}
