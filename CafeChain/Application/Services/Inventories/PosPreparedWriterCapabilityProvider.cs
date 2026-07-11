using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Issue #121 — POS_PREPARED_WRITER is Ready when the PreparedItem POS consumption path is implemented.
    /// Does not change Store WriterMode.
    /// </summary>
    public sealed class PosPreparedWriterCapabilityProvider : IInventoryWriterCapabilityProvider
    {
        public const string ContractVersion = "121.1";

        public InventoryWriterCapabilityStatus GetStatus()
            => new(
                InventoryWriterCapabilityIds.PosPreparedWriter,
                ContractVersion,
                Ready: true);
    }
}
