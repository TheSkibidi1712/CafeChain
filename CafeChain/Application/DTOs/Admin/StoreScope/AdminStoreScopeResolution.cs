namespace CafeChain.Application.DTOs.Admin.StoreScope
{
    public static class AdminStoreScopeErrorCodes
    {
        public const string StoreNotFound = "STORE_NOT_FOUND";
        public const string StoreScopeForbidden = "STORE_SCOPE_FORBIDDEN";
        public const string StoreScopeNotConfigured = "STORE_SCOPE_NOT_CONFIGURED";
        public const string SelectedStoreNoLongerAccessible = "SELECTED_STORE_NO_LONGER_ACCESSIBLE";
    }

    public enum AdminStoreScopeResolutionStatus
    {
        Resolved,
        StoreNotFound,
        RequestedStoreForbidden,
        NoAccessibleStore
    }

    public enum AdminStoreScopeResolutionSource
    {
        ExplicitRequest,
        SelectedSessionStore,
        StaffStore,
        FirstAccessibleStore
    }

    public sealed class AdminStoreOptionDto
    {
        public int StoreId { get; init; }
        public string StoreName { get; init; } = string.Empty;
    }

    public sealed class AdminStoreScopeResolution
    {
        public AdminStoreScopeResolutionStatus Status { get; init; }
        public AdminStoreScopeResolutionSource? Source { get; init; }
        public int? StoreId { get; init; }
        public string? ErrorCode { get; init; }
        public string? WarningCode { get; init; }
        public string? Message { get; init; }
        public IReadOnlyList<AdminStoreOptionDto> AccessibleStores { get; init; }
            = Array.Empty<AdminStoreOptionDto>();

        public bool IsResolved => Status == AdminStoreScopeResolutionStatus.Resolved
                                  && StoreId.GetValueOrDefault() > 0;
    }
}
