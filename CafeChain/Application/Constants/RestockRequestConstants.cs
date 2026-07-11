namespace CafeChain.Application.Constants
{
    /// <summary>Issue #100 — RestockRequest status codes.</summary>
    public static class RestockRequestStatuses
    {
        public const string Submitted = "SUBMITTED";
        /// <summary>Future warehouse processing.</summary>
        public const string Processing = "PROCESSING";
        /// <summary>Future warehouse completion.</summary>
        public const string Completed = "COMPLETED";
        /// <summary>Future warehouse reject.</summary>
        public const string Rejected = "REJECTED";
        /// <summary>Future cancel.</summary>
        public const string Cancelled = "CANCELLED";
    }

    /// <summary>Issue #100 — RestockRequest priority codes.</summary>
    public static class RestockRequestPriorities
    {
        public const string Normal = "NORMAL";
        public const string High = "HIGH";
        public const string Urgent = "URGENT";
    }
}
