namespace CafeChain.Models.Enums.Inventory
{
    /// <summary>Production run lifecycle (#119 intent, #120 stock apply).</summary>
    public enum ProductionRunStatus
    {
        /// <summary>Durable intent accepted; stock not applied.</summary>
        Confirmed = 1,

        /// <summary>Stock + ledger applied atomically; immutable.</summary>
        Completed = 2,

        /// <summary>V2 Restock-driven plan; no inventory mutation.</summary>
        Planned = 10,
        Released = 11,
        InProgress = 12,
        AwaitingAcceptance = 13,
        AwaitingVarianceApproval = 14,
        Cancelled = 15
    }
}
