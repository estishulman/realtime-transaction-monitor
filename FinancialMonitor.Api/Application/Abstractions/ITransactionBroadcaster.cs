using FinancialMonitor.Api.Domain.Entities;

namespace FinancialMonitor.Api.Application.Abstractions;

public interface ITransactionBroadcaster
{
    Task BroadcastAsync(Transaction transaction, CancellationToken cancellationToken = default);
}
