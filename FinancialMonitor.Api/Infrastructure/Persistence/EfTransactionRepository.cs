using FinancialMonitor.Api.Application.Abstractions;
using FinancialMonitor.Api.Domain.Entities;
using FinancialMonitor.Api.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace FinancialMonitor.Api.Infrastructure.Persistence;

public sealed class EfTransactionRepository(
    IDbContextFactory<FinancialMonitorDbContext> contextFactory,
    IConfiguration configuration) : ITransactionRepository
{
    private readonly int recentCount = configuration.GetValue("Transactions:RecentCount", 200);

    public async Task<Transaction> AddAsync(
        Transaction transaction,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync(cancellationToken);
        return transaction;
    }

    public async Task<Transaction> UpdateAsync(
        Transaction transaction,
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var updated = transaction with { Version = transaction.Version + 1 };
        context.Transactions.Attach(updated);
        context.Entry(updated).Property(item => item.Version).OriginalValue = transaction.Version;
        context.Entry(updated).State = EntityState.Modified;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new TransactionConcurrencyException(transaction.TransactionId, exception);
        }

        return updated;
    }

    public async Task<IReadOnlyCollection<Transaction>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Transactions
            .AsNoTracking()
            .OrderByDescending(transaction => transaction.Timestamp)
            .Take(recentCount)
            .ToArrayAsync(cancellationToken);
    }
}