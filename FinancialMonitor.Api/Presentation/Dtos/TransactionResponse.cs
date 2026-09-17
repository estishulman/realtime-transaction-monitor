namespace FinancialMonitor.Api.Presentation.Dtos;

public sealed record TransactionResponse(
    string TransactionId,
    decimal Amount,
    string Currency,
    TransactionStatusDto Status,
    DateTime Timestamp);