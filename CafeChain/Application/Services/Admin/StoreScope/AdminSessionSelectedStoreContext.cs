using CafeChain.Application.Interfaces.Admin.StoreScope;

namespace CafeChain.Application.Services.Admin.StoreScope
{
    public sealed class AdminSessionSelectedStoreContext : IAdminSelectedStoreContext
    {
        internal const string SessionKey = "Admin.SelectedStoreId";

        private readonly IHttpContextAccessor _httpContextAccessor;

        public AdminSessionSelectedStoreContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int? GetSelectedStoreId()
        {
            var value = _httpContextAccessor.HttpContext?.Session.GetInt32(SessionKey);
            return value.GetValueOrDefault() > 0 ? value : null;
        }

        public void SetSelectedStoreId(int storeId)
        {
            if (storeId > 0)
                _httpContextAccessor.HttpContext?.Session.SetInt32(SessionKey, storeId);
        }

        public void ClearSelectedStoreId()
        {
            _httpContextAccessor.HttpContext?.Session.Remove(SessionKey);
        }
    }
}
