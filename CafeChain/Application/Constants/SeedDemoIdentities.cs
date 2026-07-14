namespace CafeChain.Application.Constants
{
    /// <summary>
    /// Stable demo seed identities for EnsureCreated / guardrail tests only.
    /// Do not use in production authorization logic.
    /// Issue #94 / #130 / follow-up — ShiftSupervisor (Ca trưởng) fixed AccountId/StaffId = 15
    /// (matches historical InitialCreate account seed; avoid id 12 clash with renumber migrations).
    /// </summary>
    public static class SeedDemoIdentities
    {
        public const int ShiftSupervisorAccountId = 15;
        public const int ShiftSupervisorStaffId = 15;
        public const int ShiftSupervisorRoleId = 8;
        public const int ShiftSupervisorStoreId = 1;
        public const string ShiftSupervisorEmail = "shiftsupervisor@cafechain.vn";
    }
}
