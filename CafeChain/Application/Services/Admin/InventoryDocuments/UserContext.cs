using CafeChain.Application.DTOs.Admin.InventoryDocuments;
using CafeChain.Application.Interfaces.Admin.InventoryDocuments;
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
    }
}
