namespace CafeChain.Models.Enums.Inventory
{
    public enum InventoryWriterMode
    {
        LegacyRecipe = 0,
        PreparedItem = 1,
        Blocked = 2
    }

    public enum BtpIdentityState
    {
        Legacy = 0,
        Canonical = 1,
        Superseded = 2
    }

    public enum InventoryQuantitySemanticsStatus
    {
        Unknown = 0,
        BaseUnitConfirmed = 1,
        LegacyBatch = 2,
        Incompatible = 3
    }

    public enum QuantitySemanticsEvidenceType
    {
        ManualReview = 0,
        SystemCanonicalCreation = 1
    }
}
