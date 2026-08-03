using CafeChain.Data;
using CafeChain.Infrastrusture.Interfaces.Systems;
using CafeChain.Models.Systems;
using Microsoft.EntityFrameworkCore;

namespace CafeChain.Infrastrusture.Repositories.Systems
{
    public class RequestDeduplicationRepository : IRequestDeduplicationRepository
    {
        private readonly AppDbContext _context;

        public RequestDeduplicationRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RequestDeduplication?> GetAsync(
            string requestKey,
            string actionName,
            int staffId)
        {
            return await GetAsync(requestKey, actionName, staffId, 0);
        }

        public async Task<RequestDeduplication?> GetAsync(
            string requestKey,
            string actionName,
            int staffId,
            int storeId)
        {
            return await _context.RequestDeduplications
                .FirstOrDefaultAsync(x =>
                    x.RequestKey == requestKey
                    && x.ActionName == actionName
                    && x.StaffId == staffId
                    && x.StoreId == storeId);
        }

        public async Task AddAsync(RequestDeduplication request)
        {
            await _context.RequestDeduplications.AddAsync(request);
        }

        public void Update(RequestDeduplication request)
        {
            _context.RequestDeduplications.Update(request);
        }

        public void Detach(RequestDeduplication request)
        {
            _context.Entry(request).State = EntityState.Detached;
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
