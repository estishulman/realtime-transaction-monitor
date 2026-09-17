using FinancialMonitor.Api.Application.Abstractions;
using FinancialMonitor.Api.Application.Transactions;
using FinancialMonitor.Api.Domain.Entities;
using Moq;

namespace FinancialMonitor.Tests.Application;

public sealed class TransactionServiceTests
{
    [Fact]
    public async Task CreateAsync_InvalidTransaction_ReturnsValidationFailure()
    {
        var repository = new Mock<ITransactionRepository>();
        var processor = new Mock<ITransactionProcessor>();
        var broadcaster = new Mock<ITransactionBroadcaster>();
        var service = new TransactionService(repository.Object, processor.Object, broadcaster.Object);
        var transaction = new Transaction(
            "not-a-guid",
            -1,
            "",
            TransactionStatus.Pending,
            DateTime.Now);

        var result = await service.CreateAsync(transaction);

        Assert.False(result.Succeeded);
        Assert.Equal("Transaction is invalid.", result.Error);
        repository.Verify(item => item.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()), Times.Never);
        broadcaster.Verify(
            item => item.BroadcastAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ValidTransaction_PersistsProcessesAndBroadcastsTwice()
    {
        var transaction = CreateTransaction();
        var processedTransaction = transaction with { Status = TransactionStatus.Failed };
        var updatedTransaction = processedTransaction with { Version = transaction.Version + 1 };

        var repository = new Mock<ITransactionRepository>();
        repository.Setup(item => item.AddAsync(transaction, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        repository.Setup(item => item.UpdateAsync(processedTransaction, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedTransaction);

        var processor = new Mock<ITransactionProcessor>();
        processor.Setup(item => item.ProcessAsync(transaction, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processedTransaction);

        var broadcastOrder = new List<Transaction>();
        var broadcaster = new Mock<ITransactionBroadcaster>();
        broadcaster
            .Setup(item => item.BroadcastAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .Callback<Transaction, CancellationToken>((broadcast, _) => broadcastOrder.Add(broadcast))
            .Returns(Task.CompletedTask);

        var service = new TransactionService(repository.Object, processor.Object, broadcaster.Object);

        var result = await service.CreateAsync(transaction);

        Assert.True(result.Succeeded);
        Assert.Equal(updatedTransaction, result.Transaction);
        Assert.Equal([transaction, updatedTransaction], broadcastOrder);
        repository.Verify(item => item.AddAsync(transaction, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(item => item.UpdateAsync(processedTransaction, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Transaction CreateTransaction() => new(
        Guid.NewGuid().ToString(),
        100,
        "USD",
        TransactionStatus.Pending,
        DateTime.UtcNow);
}
