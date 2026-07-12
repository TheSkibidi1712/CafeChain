using System.Collections.Generic;

namespace CafeChain.Application.DTOs.Admin.Actor
{
    /// <summary>
    /// Read-only claims snapshot. Does not grant business permission —
    /// application services remain the authorization authority.
    /// </summary>
    public sealed class AdminActorContext
    {
        public int StaffId { get; init; }
        public int StoreId { get; init; }
        public int? StoreIdOrNull => StoreId > 0 ? StoreId : null;
        public IReadOnlyList<string> RoleNames { get; init; } = System.Array.Empty<string>();
    }
}
