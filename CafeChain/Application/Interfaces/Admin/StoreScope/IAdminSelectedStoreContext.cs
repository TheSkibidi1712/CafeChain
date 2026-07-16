namespace CafeChain.Application.Interfaces.Admin.StoreScope
{
    public interface IAdminSelectedStoreContext
    {
        int? GetSelectedStoreId();
        void SetSelectedStoreId(int storeId);
        void ClearSelectedStoreId();
    }
}
