using CafeChain.Application.DTOs.Inventories;
using CafeChain.Application.Interfaces.Inventories;

namespace CafeChain.Application.Services.Inventories
{
    /// <summary>
    /// Issue #122 — ALERT_RESTOCK_PREPARED_IDENTITY is Ready when alert/restock
    /// transitional PreparedItem identity is implemented. Does not change Store WriterMode.
    /// Code-contract readiness only — deploy still requires coordinated migration apply.
    /// </summary>
    public sealed class AlertRestockPreparedIdentityCapabilityProvider : IInventoryWriterCapabilityProvider
    {
        public const string ContractVersion = "122.1";

        public InventoryWriterCapabilityStatus GetStatus()
            => new(
                InventoryWriterCapabilityIds.AlertRestockPreparedIdentity,
                ContractVersion,
                Ready: true);
    }
}
