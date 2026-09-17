using FinancialMonitor.Api.Application.Transactions;
using FinancialMonitor.Api.Domain.Entities;
using FinancialMonitor.Api.Presentation.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace FinancialMonitor.Api.Presentation.Controllers;

[ApiController]
[Route("api/transactions")]
public sealed class TransactionsController(ITransactionService transactionService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TransactionResponse>> Create(
        [FromBody] CreateTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var transaction = request.ToDomain();
        var result = await transactionService.CreateAsync(transaction, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(result.Error);
        }

        var savedTransaction = result.Transaction!;
        return Created(
            $"/api/transactions/{savedTransaction.TransactionId}",
            TransactionDtoMapping.FromDomain(savedTransaction));
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<TransactionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<TransactionResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var transactions = await transactionService.GetAllAsync(cancellationToken);
        return Ok(transactions.Select(TransactionDtoMapping.FromDomain).ToArray());
    }
}

file static class TransactionDtoMapping
{
    public static Transaction ToDomain(this CreateTransactionRequest request) => new(
        Guid.NewGuid().ToString(),
        request.Amount,
        request.Currency,
        TransactionStatus.Pending,
        DateTime.UtcNow);

    public static TransactionResponse FromDomain(Transaction transaction) => new(
        transaction.TransactionId,
        transaction.Amount,
        transaction.Currency,
        (TransactionStatusDto)transaction.Status,
        transaction.Timestamp);
}