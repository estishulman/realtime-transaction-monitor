using System.Text.Json;
using FinancialMonitor.Api.Application.Abstractions;
using FinancialMonitor.Api.Domain.Entities;
using StackExchange.Redis;

namespace FinancialMonitor.Api.Infrastructure.Caching;

public sealed class RedisTransactionCache(IConnectionMultiplexer redis) : ITransactionCache
{
    private const string TransactionsHashKey = "FinancialMonitor:transactions";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);

    private IDatabase Database => redis.GetDatabase();

    public async Task<IReadOnlyCollection<Transaction>?> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = await Database.HashGetAllAsync(TransactionsHashKey);
        if (entries.Length == 0)
        {
            return null;
        }

        return entries
            .Select(entry => JsonSerializer.Deserialize<Transaction>(entry.Value.ToString()))
            .OfType<Transaction>()
            .ToArray();
    }

    public async Task SetAsync(
        IReadOnlyCollection<Transaction> transactions,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var transaction in transactions)
        {
            await UpsertIfNewerAsync(transaction, cancellationToken);
        }
    }

    public Task UpdateAsync(
        Transaction transaction,
        CancellationToken cancellationToken = default) => UpsertIfNewerAsync(transaction, cancellationToken);

    public Task AddAsync(
        Transaction transaction,
        CancellationToken cancellationToken = default) => UpsertIfNewerAsync(transaction, cancellationToken);

    public async Task RemoveAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Database.KeyDeleteAsync(TransactionsHashKey);
    }

    private async Task UpsertIfNewerAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var existingEntry = await Database.HashGetAsync(TransactionsHashKey, transaction.TransactionId);
        var existingTransaction = existingEntry.HasValue
            ? JsonSerializer.Deserialize<Transaction>(existingEntry!)
            : null;

        if (IsStale(existingTransaction, transaction))
        {
            return;
        }

        var cacheUpdate = Database.CreateTransaction();
        _ = cacheUpdate.HashSetAsync(
            TransactionsHashKey,
            transaction.TransactionId,
            JsonSerializer.Serialize(transaction));
        _ = cacheUpdate.HashFieldExpireAsync(
            TransactionsHashKey,
            [transaction.TransactionId],
            CacheLifetime);

        await cacheUpdate.ExecuteAsync();
    }

    public static bool IsStale(Transaction? existing, Transaction incoming) =>
        existing is not null && existing.Version > incoming.Version;
}
