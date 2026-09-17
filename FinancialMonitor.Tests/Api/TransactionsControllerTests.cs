using FinancialMonitor.Api.Application.Transactions;
using FinancialMonitor.Api.Domain.Entities;
using FinancialMonitor.Api.Presentation.Controllers;
using FinancialMonitor.Api.Presentation.Dtos;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FinancialMonitor.Tests.Api;

public sealed class TransactionsControllerTests
{
    [Fact]
    public async Task Create_ValidTransaction_ReturnsCreatedWithServiceResult()
    {
        var transaction = new Transaction(
            Guid.NewGuid().ToString(),
            100,
            "USD",
            TransactionStatus.Completed,
            DateTime.UtcNow);
        var transactionService = new Mock<ITransactionService>();
        transactionService
            .Setup(service => service.CreateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransactionOperationResult.Success(transaction));
        var controller = new TransactionsController(transactionService.Object);

        var request = new CreateTransactionRequest(transaction.Amount, transaction.Currency);
        var result = await controller.Create(request, CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result.Result);
        var response = Assert.IsType<TransactionResponse>(created.Value);
        Assert.NotEqual(Guid.Empty.ToString(), response.TransactionId);
        Assert.Equal(transaction.Amount, response.Amount);
        Assert.Equal(transaction.Currency, response.Currency);
        Assert.Equal(TransactionStatus.Completed, (TransactionStatus)response.Status);
        Assert.Equal(DateTimeKind.Utc, response.Timestamp.Kind);
    }

    [Fact]
    public async Task Create_InvalidTransaction_ReturnsBadRequest()
    {
        var transactionService = new Mock<ITransactionService>();
        transactionService
            .Setup(service => service.CreateAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransactionOperationResult.Invalid("Transaction is invalid."));
        var controller = new TransactionsController(transactionService.Object);

        var request = new CreateTransactionRequest(-1, "USD");
        var result = await controller.Create(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Transaction is invalid.", badRequest.Value);
    }
}