using FinancialMonitor.Api.Domain.Entities;
using FinancialMonitor.Api.Application.Abstractions;

namespace FinancialMonitor.Api.Application.Transactions;

public sealed class TransactionService(
    ITransactionRepository repository,
    ITransactionProcessor processor,
    ITransactionBroadcaster broadcaster) : ITransactionService
{
    public async Task<TransactionOperationResult> CreateAsync(
        Transaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transaction.TransactionId) ||
            !Guid.TryParse(transaction.TransactionId, out _) ||
            string.IsNullOrWhiteSpace(transaction.Currency) ||
            transaction.Amount < 0 ||
            !Enum.IsDefined(transaction.Status) ||
            transaction.Timestamp.Kind != DateTimeKind.Utc)
        {
            return TransactionOperationResult.Invalid("Transaction is invalid.");
        }

        var savedTransaction = await repository.AddAsync(transaction, cancellationToken);
        await broadcaster.BroadcastAsync(savedTransaction, cancellationToken);

        var processedTransaction = await processor.ProcessAsync(savedTransaction, cancellationToken);
        var updatedTransaction = await repository.UpdateAsync(processedTransaction, cancellationToken);
        await broadcaster.BroadcastAsync(updatedTransaction, cancellationToken);

        return TransactionOperationResult.Success(updatedTransaction);
    }

    public Task<IReadOnlyCollection<Transaction>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        repository.GetAllAsync(cancellationToken);
}