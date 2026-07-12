using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;

namespace CafeChain.Application.Services.Admin.Production
{
    /// <summary>
    /// Issue #120 — PRODUCTION_PREPARED_WRITER is Ready when this provider is registered in DI
    /// after the execution service is implemented. Does not change Store WriterMode.
    /// </summary>
    public sealed class ProductionPreparedWriterCapabilityProvider : IInventoryWriterCapabilityProvider
    {
        public const string ContractVersion = "120.1";

        public InventoryWriterCapabilityStatus GetStatus()
            => new(
                InventoryWriterCapabilityIds.ProductionPreparedWriter,
                ContractVersion,
                Ready: true);
    }
}
