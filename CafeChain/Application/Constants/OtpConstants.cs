namespace CafeChain.Application.Constants
{
    public static class OtpConstants
    {
        /// <summary>Six-character alphanumeric OTP (no ambiguous O/0/I/1).</summary>
        public const int CodeLength = 6;

        /// <summary>Allowed OTP alphabet — uppercase + digits, excludes O,0,I,1.</summary>
        public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public const int TtlMinutes = 5;
        public const int MaxFailedAttempts = 5;
        public const int ResendCooldownSeconds = 60;
        public const int MaxResendCount = 3;

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
        }

        /// <summary>
        /// Phase 3 (#140): legacy PIN bypass/management messages (disabled, not deleted until #143).
        /// </summary>
        public static class PinDisabledMessages
        {
            public const string SupervisorPinAuth =
                "Xác thực PIN supervisor không còn được hỗ trợ. Các thao tác nhạy cảm dùng OTP phê duyệt (online) hoặc đã bị gỡ.";

            public const string UpdatePin =
                "Đặt/đổi PIN supervisor không còn được hỗ trợ. Mã PIN cố định đã bị loại khỏi luồng phê duyệt.";

            public const string GenericApprovalBool =
                "Phê duyệt supervisor generic (success bool) không còn được hỗ trợ.";
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
