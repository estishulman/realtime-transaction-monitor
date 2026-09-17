using System.Text.Json.Serialization;

namespace FinancialMonitor.Api.Presentation.Dtos;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransactionStatusDto
{
    Pending,
    Completed,
    Failed
}