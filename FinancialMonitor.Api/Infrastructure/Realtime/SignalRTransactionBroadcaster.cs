using FinancialMonitor.Api.Application.Abstractions;
using FinancialMonitor.Api.Domain.Entities;
using FinancialMonitor.Api.Presentation.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace FinancialMonitor.Api.Infrastructure.Realtime;

public sealed class SignalRTransactionBroadcaster(IHubContext<TransactionHub> hubContext) : ITransactionBroadcaster
{
    public Task BroadcastAsync(Transaction transaction, CancellationToken cancellationToken = default) =>
        hubContext.Clients.All.SendAsync("ReceiveTransaction", transaction, cancellationToken);
}
