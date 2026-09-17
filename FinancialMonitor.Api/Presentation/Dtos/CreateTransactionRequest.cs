namespace FinancialMonitor.Api.Presentation.Dtos;

public sealed record CreateTransactionRequest(
    decimal Amount,
    string Currency);