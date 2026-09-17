using FinancialMonitor.Api.Domain.Entities;
using FinancialMonitor.Api.Infrastructure.Caching;

namespace FinancialMonitor.Tests.Storage;

public sealed class RedisTransactionCacheTests
{
    [Fact]
    public void IsStale_NoExistingEntry_ReturnsFalse()
    {
        var incoming = CreateTransaction(version: 0);

        Assert.False(RedisTransactionCache.IsStale(existing: null, incoming));
    }

    [Fact]
    public void IsStale_IncomingVersionIsNewer_ReturnsFalse()
    {
        var existing = CreateTransaction(version: 1);
        var incoming = CreateTransaction(version: 2);

        Assert.False(RedisTransactionCache.IsStale(existing, incoming));
    }

    [Fact]
    public void IsStale_IncomingVersionIsOlder_ReturnsTrue()
    {
        var existing = CreateTransaction(version: 2);
        var incoming = CreateTransaction(version: 1);

        Assert.True(RedisTransactionCache.IsStale(existing, incoming));
    }

    [Fact]
    public void IsStale_SameVersion_ReturnsFalse()
    {
        var existing = CreateTransaction(version: 1);
        var incoming = CreateTransaction(version: 1);

        Assert.False(RedisTransactionCache.IsStale(existing, incoming));
    }

    private static Transaction CreateTransaction(int version) => new(
        Guid.NewGuid().ToString(),
        100.50m,
        "USD",
        TransactionStatus.Pending,
        DateTime.UtcNow)
    {
        Version = version
    };
}
