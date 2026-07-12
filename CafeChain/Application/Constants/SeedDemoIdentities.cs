namespace CafeChain.Application.Constants
{
    /// <summary>
    /// Stable demo seed identities for EnsureCreated / guardrail tests only.
    /// Do not use in production authorization logic.
    /// Issue #94 / #130 — ShiftSupervisor (Ca trưởng) fixed AccountId 12.
    /// </summary>
    public static class SeedDemoIdentities
    {
        public const int ShiftSupervisorAccountId = 12;
        public const int ShiftSupervisorStaffId = 12;
        public const int ShiftSupervisorRoleId = 8;
        public const int ShiftSupervisorStoreId = 1;
        public const string ShiftSupervisorEmail = "shiftsupervisor@cafechain.vn";
    }
}
