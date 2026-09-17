namespace FinancialMonitor.Api.Domain.Exceptions;

public sealed class TransactionConcurrencyException(string transactionId, Exception innerException)
    : Exception($"Transaction '{transactionId}' was concurrently modified by another writer.", innerException)
{
    public string TransactionId { get; } = transactionId;
}
