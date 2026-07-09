namespace CafeChain.Application.Constants
{
    public static class OtpConstants
    {
        public const int CodeLength = 6;
        public const int TtlMinutes = 5;
        public const int MaxFailedAttempts = 5;
        public const int ResendCooldownSeconds = 60;
        public const int MaxResendCount = 3;

        public static class Statuses
        {
            public const string Pending = "Pending";
            public const string Approved = "Approved";
            public const string Used = "Used";
            public const string Expired = "Expired";
            public const string Locked = "Locked";
            public const string Cancelled = "Cancelled";
        }

        public static class ActionTypes
        {
            public const string CashDifference = "CASH_DIFFERENCE";
        }

        public static class TargetTypes
        {
            public const string Shifts = "shifts";
        }
    }
}
