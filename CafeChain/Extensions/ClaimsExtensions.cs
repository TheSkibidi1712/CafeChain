using System.Security.Claims;

namespace CafeChain.Extensions
{
    /// <summary>
    /// Extension methods cho ClaimsPrincipal — trích xuất StoreId và StaffId từ JWT/Cookie claims.
    /// Claim types: "StoreId", "StaffId" (consistent với AccountService login flow).
    /// </summary>
    public static class ClaimsExtensions
    {
        /// <summary>
        /// Trích xuất StoreId từ Claims. Throw nếu không tìm thấy hoặc invalid.
        /// </summary>
        public static int GetStoreId(this ClaimsPrincipal user)
        {
            var claim = user.FindFirst("StoreId")?.Value;
            if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out int storeId) || storeId <= 0)
            {
                throw new UnauthorizedAccessException("Missing or invalid 'StoreId' claim in token.");
            }
            return storeId;
        }

        /// <summary>
        /// Trích xuất StaffId từ Claims. Throw nếu không tìm thấy hoặc invalid.
        /// </summary>
        public static int GetStaffId(this ClaimsPrincipal user)
        {
            var claim = user.FindFirst("StaffId")?.Value;
            if (string.IsNullOrEmpty(claim) || !int.TryParse(claim, out int staffId) || staffId <= 0)
            {
                throw new UnauthorizedAccessException("Missing or invalid 'StaffId' claim in token.");
            }
            return staffId;
        }

        /// <summary>
        /// Try-get StoreId — trả 0 nếu không tìm thấy (backward compat cho cookie auth).
        /// </summary>
        public static int GetStoreIdOrDefault(this ClaimsPrincipal user)
        {
            var claim = user.FindFirst("StoreId")?.Value;
            return int.TryParse(claim, out int storeId) ? storeId : 0;
        }

        /// <summary>
        /// Try-get StaffId — trả 0 nếu không tìm thấy.
        /// </summary>
        public static int GetStaffIdOrDefault(this ClaimsPrincipal user)
        {
            var claim = user.FindFirst("StaffId")?.Value;
            return int.TryParse(claim, out int staffId) ? staffId : 0;
        }
    }
}
