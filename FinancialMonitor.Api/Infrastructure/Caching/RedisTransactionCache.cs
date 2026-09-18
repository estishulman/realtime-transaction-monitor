using System.Text.Json;
using FinancialMonitor.Api.Application.Abstractions;
using FinancialMonitor.Api.Domain.Entities;
using StackExchange.Redis;

namespace FinancialMonitor.Api.Infrastructure.Caching;

public sealed class RedisTransactionCache(IConnectionMultiplexer redis) : ITransactionCache
{
    private const string TransactionsHashKey = "FinancialMonitor:transactions";
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);

    // Atomic compare-and-set: a read-then-write from C# has a race window where another
    // pod's write can land between the read and the write, silently overwriting it. Doing
    // the read, comparison, and write inside one Lua script makes Redis execute all three
    // as a single atomic step, closing that window.
    private static readonly LuaScript UpsertIfNewerScript = LuaScript.Prepare(
        $$"""
        local existing = redis.call('HGET', @key, @field)
        if existing then
            local ok, decoded = pcall(cjson.decode, existing)
            if ok and decoded.{{nameof(Transaction.Version)}} ~= nil and tonumber(decoded.{{nameof(Transaction.Version)}}) >= tonumber(@version) then
                return 0
            end
        end
        redis.call('HSET', @key, @field, @value)
        redis.call('HEXPIRE', @key, @ttlSeconds, 'FIELDS', 1, @field)
        return 1
        """);

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

        await UpsertIfNewerScript.EvaluateAsync(
            Database,
            new
            {
                key = (RedisKey)TransactionsHashKey,
                field = transaction.TransactionId,
                value = JsonSerializer.Serialize(transaction),
                version = transaction.Version,
                ttlSeconds = (int)CacheLifetime.TotalSeconds
            });
    }
}
