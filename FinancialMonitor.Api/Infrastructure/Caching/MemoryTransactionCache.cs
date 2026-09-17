using FinancialMonitor.Api.Application.Abstractions;
using FinancialMonitor.Api.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace FinancialMonitor.Api.Infrastructure.Caching;

public sealed class MemoryTransactionCache(IMemoryCache cache) : ITransactionCache
{
    private const string CacheKey = "transactions:all";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);
    private readonly SemaphoreSlim cacheLock = new(1, 1);

    public async Task<IReadOnlyCollection<Transaction>?> GetAsync(CancellationToken cancellationToken = default)
    {
        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            return cache.TryGetValue(CacheKey, out IReadOnlyCollection<Transaction>? value) ? value : null;
        }
        finally
        {
            cacheLock.Release();
        }
    }

    public async Task SetAsync(IReadOnlyCollection<Transaction> transactions, CancellationToken cancellationToken = default)
    {
        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            cache.Set(CacheKey, transactions, CacheLifetime);
        }
        finally
        {
            cacheLock.Release();
        }
    }

    public async Task UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(CacheKey, out IReadOnlyCollection<Transaction>? transactions) &&
                transactions is not null)
            {
                var updatedTransactions = transactions
                    .Select(item => item.TransactionId == transaction.TransactionId ? transaction : item)
                    .ToArray();
                cache.Set(CacheKey, updatedTransactions, CacheLifetime);
            }
        }
        finally
        {
            cacheLock.Release();
        }
    }

    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(CacheKey, out IReadOnlyCollection<Transaction>? transactions) &&
                transactions is not null)
            {
                cache.Set(CacheKey, transactions.Append(transaction).ToArray(), CacheLifetime);
            }
        }
        finally
        {
            cacheLock.Release();
        }
    }

    public async Task RemoveAsync(CancellationToken cancellationToken = default)
    {
        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            cache.Remove(CacheKey);
        }
        finally
        {
            cacheLock.Release();
        }
    }
}