namespace CafeChain.Models.Inventories.Auditing
{
    public class AuditLog
    {
        public int AuditLogId { get; set; }

        public string TableName { get; set; }
        public int RecordId { get; set; }

        public string Action { get; set; }

        public string? OldData { get; set; }
        public string? NewData { get; set; }

        public int UserId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
