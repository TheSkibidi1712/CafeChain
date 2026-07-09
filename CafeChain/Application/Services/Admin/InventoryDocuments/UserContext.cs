using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
using System.Security.Claims;

namespace CafeChain.Application.Services.Admin.InventoryDocuments
{
    public class UserContext : IUserContext
    {
        private readonly IHttpContextAccessor _http;

        public UserContext(IHttpContextAccessor http)
        {
            _http = http;
        }

        public int StaffId
        {
            get
            {
                var claim = _http.HttpContext?.User?.FindFirst("StaffId");

                return claim != null ? int.Parse(claim.Value) : 0;
            }
        }

        public string StaffName
        {
            get
            {
                var user = _http.HttpContext?.User;
                var name = user?.FindFirst(ClaimTypes.Name)?.Value
                    ?? user?.FindFirst("StaffName")?.Value;

                return string.IsNullOrWhiteSpace(name)
                    ? "Không xác định"
                    : name.Trim();
            }
        }
    }
}
