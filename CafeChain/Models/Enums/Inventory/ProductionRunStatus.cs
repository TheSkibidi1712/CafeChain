namespace CafeChain.Models.Enums.Inventory
{
    /// <summary>Production run lifecycle (#119 intent, #120 stock apply).</summary>
    public enum ProductionRunStatus
    {
        /// <summary>Durable intent accepted; stock not applied.</summary>
        Confirmed = 1,

        /// <summary>Stock + ledger applied atomically; immutable.</summary>
        Completed = 2
    }
}
