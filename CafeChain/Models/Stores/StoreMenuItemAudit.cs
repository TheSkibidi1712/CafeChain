namespace CafeChain.Models.Stores
{
    public class StoreMenuItemAudit
    {
        public long StoreMenuItemAuditId { get; set; }
        public int StoreMenuItemId { get; set; }
        public int StoreId { get; set; }
        public int DrinkSizeId { get; set; }
        public string Action { get; set; } = string.Empty;
        public bool OldIsEnabled { get; set; }
        public bool NewIsEnabled { get; set; }
        public decimal? OldPriceOverride { get; set; }
        public decimal? NewPriceOverride { get; set; }
        public DateTime? OldEffectiveFromUtc { get; set; }
        public DateTime? NewEffectiveFromUtc { get; set; }
        public DateTime? OldEffectiveToUtc { get; set; }
        public DateTime? NewEffectiveToUtc { get; set; }
        public long CatalogVersionBefore { get; set; }
        public long CatalogVersionAfter { get; set; }
        public byte[] ItemRowVersionBefore { get; set; } = Array.Empty<byte>();
        public byte[] ItemRowVersionAfter { get; set; } = Array.Empty<byte>();
        public string? OldDataJson { get; set; }
        public string NewDataJson { get; set; } = string.Empty;
        public int ActorStaffId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }

        public virtual StoreMenuItem StoreMenuItem { get; set; } = null!;
        public virtual CafeChain.Models.Staffs.Staff ActorStaff { get; set; } = null!;
    }
}
