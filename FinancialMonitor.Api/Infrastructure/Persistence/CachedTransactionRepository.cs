using FinancialMonitor.Api.Application.Abstractions;
using FinancialMonitor.Api.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialMonitor.Api.Infrastructure.Persistence;

public sealed class CachedTransactionRepository(
    [FromKeyedServices("inner")] ITransactionRepository repository,
    ITransactionCache cache,
    ILogger<CachedTransactionRepository> logger) : ITransactionRepository
{
    public async Task<Transaction> AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        var result = await repository.AddAsync(transaction, cancellationToken);
        await cache.AddAsync(result, cancellationToken);
        logger.LogInformation("Transaction cache updated after adding {TransactionId}.", result.TransactionId);
        return result;
    }

    public async Task<Transaction> UpdateAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        var result = await repository.UpdateAsync(transaction, cancellationToken);
        await cache.UpdateAsync(result, cancellationToken);
        logger.LogInformation("Transaction cache updated after updating {TransactionId}.", result.TransactionId);
        return result;
    }

    public async Task<IReadOnlyCollection<Transaction>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var cached = await cache.GetAsync(cancellationToken);
        if (cached is not null)
        {
            logger.LogInformation("Transactions loaded from cache. Count: {Count}.", cached.Count);
            return cached;
        }

        var transactions = await repository.GetAllAsync(cancellationToken);
        await cache.SetAsync(transactions, cancellationToken);
        logger.LogInformation("Transactions loaded from database and stored in cache. Count: {Count}.", transactions.Count);
        return transactions;
    }
}