using CafeChain.Models.Systems;

namespace CafeChain.Infrastrusture.Interfaces.Systems
{
    public interface IRequestDeduplicationRepository
    {
        Task<RequestDeduplication?> GetAsync(
            string requestKey,
            string actionName,
            int staffId);

        Task<RequestDeduplication?> GetAsync(
            string requestKey,
            string actionName,
            int staffId,
            int storeId);

        Task AddAsync(RequestDeduplication request);

        void Update(RequestDeduplication request);

        void Detach(RequestDeduplication request);

        Task SaveChangesAsync();
    }
}
