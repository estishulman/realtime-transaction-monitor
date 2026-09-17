using FinancialMonitor.Api.Domain.Entities;
using FinancialMonitor.Api.Domain.Exceptions;
using FinancialMonitor.Api.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FinancialMonitor.Tests.Storage;

public sealed class EfTransactionRepositoryTests : IDisposable
{
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancialMonitorDbContext> contextOptions;

    public EfTransactionRepositoryTests()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        contextOptions = new DbContextOptionsBuilder<FinancialMonitorDbContext>()
            .UseSqlite(connection)
            .Options;

        using var context = new FinancialMonitorDbContext(contextOptions);
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task AddAsync_NewTransaction_StartsAtVersionZero()
    {
        var repository = CreateSut();

        var added = await repository.AddAsync(CreateTransaction());

        Assert.Equal(0, added.Version);
    }

    [Fact]
    public async Task UpdateAsync_ValidVersion_IncrementsVersionAndPersistsChanges()
    {
        var repository = CreateSut();
        var added = await repository.AddAsync(CreateTransaction());

        var updated = await repository.UpdateAsync(added with { Status = TransactionStatus.Completed });

        Assert.Equal(added.Version + 1, updated.Version);
        Assert.Equal(TransactionStatus.Completed, updated.Status);

        var stored = (await repository.GetAllAsync()).Single();
        Assert.Equal(updated.Version, stored.Version);
        Assert.Equal(TransactionStatus.Completed, stored.Status);
    }

    [Fact]
    public async Task UpdateAsync_BasedOnStaleVersion_ThrowsTransactionConcurrencyException()
    {
        var repository = CreateSut();
        var original = await repository.AddAsync(CreateTransaction());
        await repository.UpdateAsync(original with { Status = TransactionStatus.Completed });

        await Assert.ThrowsAsync<TransactionConcurrencyException>(
            () => repository.UpdateAsync(original with { Status = TransactionStatus.Failed }));
    }

    [Fact]
    public async Task UpdateAsync_BasedOnStaleVersion_DoesNotOverwriteTheWinningUpdate()
    {
        var repository = CreateSut();
        var original = await repository.AddAsync(CreateTransaction());
        await repository.UpdateAsync(original with { Status = TransactionStatus.Completed });

        await Assert.ThrowsAsync<TransactionConcurrencyException>(
            () => repository.UpdateAsync(original with { Status = TransactionStatus.Failed }));

        var stored = (await repository.GetAllAsync()).Single();
        Assert.Equal(TransactionStatus.Completed, stored.Status);
    }

    private EfTransactionRepository CreateSut() =>
        new(new TestDbContextFactory(contextOptions), new ConfigurationBuilder().Build());

    private static Transaction CreateTransaction() => new(
        Guid.NewGuid().ToString(),
        100.50m,
        "USD",
        TransactionStatus.Pending,
        DateTime.UtcNow);

    public void Dispose() => connection.Dispose();

    private sealed class TestDbContextFactory(DbContextOptions<FinancialMonitorDbContext> options)
        : IDbContextFactory<FinancialMonitorDbContext>
    {
        public FinancialMonitorDbContext CreateDbContext() => new(options);
    }
}
