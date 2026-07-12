namespace CafeChain.Models.Enums.Inventory
{
    public enum InventoryConsolidationRunType
    {
        AuditNoOp = 1,
        Consolidation = 2
    }

    public enum InventoryConsolidationRunStatus
    {
        Draft = 1,
        DryRunReady = 2,
        Blocked = 3,
        Executing = 4,
        Completed = 5,
        Failed = 6
    }

    public enum InventoryConsolidationLineRole
    {
        Source = 1,
        Target = 2
    }
}
