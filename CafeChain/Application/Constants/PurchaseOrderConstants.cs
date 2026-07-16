namespace CafeChain.Application.Constants
{
    public static class PurchaseOrderStatuses
    {
        public const string Draft = "DRAFT";
        public const string Approved = "APPROVED";
        public const string MarkedAsSent = "MARKED_AS_SENT";
        public const string PartiallyReceived = "PARTIALLY_RECEIVED";
        public const string Completed = "COMPLETED";
        public const string Cancelled = "CANCELLED";

        public static readonly string[] IncomingValues =
        {
            Approved,
            MarkedAsSent,
            PartiallyReceived
        };
    }
}
