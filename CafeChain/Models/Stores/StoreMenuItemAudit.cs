namespace CafeChain.Models.Stores
{
    public class StoreMenuItemAudit
    {
        public long StoreMenuItemAuditId { get; set; }
        public int StoreMenuItemId { get; set; }
        public int StoreId { get; set; }
        public int DrinkSizeId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? OldDataJson { get; set; }
        public string NewDataJson { get; set; } = string.Empty;
        public int ActorStaffId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }

        public virtual StoreMenuItem StoreMenuItem { get; set; } = null!;
        public virtual CafeChain.Models.Staffs.Staff ActorStaff { get; set; } = null!;
    }
}
