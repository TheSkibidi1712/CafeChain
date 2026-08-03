namespace CafeChain.Application.Constants
{
    public static class OtpConstants
    {
        /// <summary>Six-character alphanumeric OTP (no ambiguous O/0/I/1).</summary>
        public const int CodeLength = 6;

        /// <summary>Allowed OTP alphabet — uppercase + digits, excludes O,0,I,1.</summary>
        public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public const int TtlMinutes = 5;
        public const int MaxFailedAttempts = 3;
        public const int ResendCooldownSeconds = 60;
        public const int MaxResendCount = 3;
        public const int RateLimitWindowMinutes = 15;
        public const int MaxChallengesPerStaffWindow = 5;
        public const int MaxChallengesPerTerminalWindow = 10;
        public const int MaxChallengesPerIpWindow = 20;
        public const int MaxChallengesPerDeviceWindow = 10;
        public const int MaxFailedAttemptsPerIpWindow = 20;
        public const int MaxFailedAttemptsPerDeviceWindow = 10;

        /// <summary>Minutes after scheduled start when open is considered late.</summary>
        public const int LateOpenThresholdMinutes = 30;

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
            public const string CloseShiftException = "CLOSE_SHIFT_EXCEPTION";
            public const string OpenShiftLate = "OPEN_SHIFT_LATE";
            public const string OpenShiftOutsideSchedule = "OPEN_SHIFT_OUTSIDE_SCHEDULE";
            public const string RegisterTerminal = "REGISTER_POS_TERMINAL";
            public const string ReconcileWorkShift = "RECONCILE_WORKSHIFT";
        }

        public static class TargetTypes
        {
            public const string Shifts = "shifts";
        }

        public static class ErrorCodes
        {
            public const string NoEligibleApprover = "NO_ELIGIBLE_APPROVER";
            public const string PayloadMismatch = "OTP_CHALLENGE_PAYLOAD_MISMATCH";
            public const string ApproverNoLongerEligible = "OTP_APPROVER_NO_LONGER_ELIGIBLE";
            public const string EmailFailed = "OTP_EMAIL_FAILED";
            public const string Required = "OTP_REQUIRED";
            public const string FeatureNotAvailable = "FEATURE_NOT_AVAILABLE";
            public const string RequiresOnline = "SUPERVISOR_APPROVAL_REQUIRES_ONLINE";
            public const string LateOpeningRequiresOtp = "LATE_OPENING_REQUIRES_OTP";
            public const string RateLimited = "OTP_RATE_LIMITED";
            /// <summary>SMTP mode is on but Email:Password / Email__Password is not configured.</summary>
            public const string EmailSmtpPasswordNotConfigured = "EMAIL_SMTP_PASSWORD_NOT_CONFIGURED";
        }

        public static class Thresholds
        {
            /// <summary>Absolute VND amount above which OTP is required.</summary>
            public const decimal AbsoluteAmountVnd = 50_000m;
            /// <summary>Percentage of expected cash above which OTP is required (2%).</summary>
            public const decimal PercentageOfExpected = 0.02m;
        }
    }
}
