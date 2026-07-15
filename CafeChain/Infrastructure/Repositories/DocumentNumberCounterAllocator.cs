using System.Data;
using CafeChain.Data;
using CafeChain.Models.Systems;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CafeChain.Infrastrusture.Repositories;

internal static class DocumentNumberCounterAllocator
{
    public static async Task<int> NextAsync(AppDbContext context, string counterKey, DateTime date)
    {
        IDbContextTransaction? localTransaction = null;
        if (context.Database.CurrentTransaction == null)
            localTransaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            var dateKey = date.Year * 10000 + date.Month * 100 + date.Day;
            DocumentNumberCounter? counter;
            if (context.Database.IsSqlServer())
            {
                counter = await context.DocumentNumberCounters.FromSqlInterpolated(
                    $"SELECT * FROM DocumentNumberCounters WITH (UPDLOCK, HOLDLOCK, ROWLOCK) WHERE CounterKey = {counterKey} AND DateKey = {dateKey}")
                    .SingleOrDefaultAsync();
            }
            else
            {
                counter = await context.DocumentNumberCounters
                    .SingleOrDefaultAsync(x => x.CounterKey == counterKey && x.DateKey == dateKey);
            }

            if (counter == null)
            {
                counter = new DocumentNumberCounter
                {
                    CounterKey = counterKey,
                    DateKey = dateKey,
                    LastValue = 1,
                    UpdatedAt = DateTime.UtcNow
                };
                await context.DocumentNumberCounters.AddAsync(counter);
            }
            else
            {
                counter.LastValue++;
                counter.UpdatedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync();
            if (localTransaction != null)
                await localTransaction.CommitAsync();
            return counter.LastValue;
        }
        catch
        {
            if (localTransaction != null)
                await localTransaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (localTransaction != null)
                await localTransaction.DisposeAsync();
        }
    }
}
