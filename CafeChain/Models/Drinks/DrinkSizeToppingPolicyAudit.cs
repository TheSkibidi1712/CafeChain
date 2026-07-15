namespace CafeChain.Models.Drinks
{
    public class DrinkSizeToppingPolicyAudit
    {
        public int DrinkSizeToppingPolicyAuditId { get; set; }
        public int DrinkSizeToppingPolicyId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? OldDataJson { get; set; }
        public string NewDataJson { get; set; } = string.Empty;
        public int ActorStaffId { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public virtual DrinkSizeToppingPolicy Policy { get; set; } = null!;
    }
}
