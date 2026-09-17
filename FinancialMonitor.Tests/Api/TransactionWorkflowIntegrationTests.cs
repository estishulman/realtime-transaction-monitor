using System.Net;
using System.Net.Http.Json;
using FinancialMonitor.Api.Domain.Entities;
using FinancialMonitor.Api.Presentation.Dtos;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinancialMonitor.Tests.Api;

public sealed class TransactionWorkflowIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public TransactionWorkflowIntegrationTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task IngestAndProcessTransactions_GetReturnsFinalStatuses()
    {
        var completed = new { amount = 1500m, currency = "USD" };
        var failed = new { amount = 10001m, currency = "USD" };

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/transactions", completed),
            client.PostAsJsonAsync("/api/transactions", failed));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));

        var completedResponse = await responses[0].Content.ReadFromJsonAsync<TransactionResponse>();
        var failedResponse = await responses[1].Content.ReadFromJsonAsync<TransactionResponse>();
        Assert.NotNull(completedResponse);
        Assert.NotNull(failedResponse);
        var finalStatuses = await WaitForFinalStatusesAsync(
            completedResponse!.TransactionId,
            failedResponse!.TransactionId);

        Assert.Equal(TransactionStatusDto.Completed, finalStatuses[completedResponse.TransactionId]);
        Assert.Equal(TransactionStatusDto.Failed, finalStatuses[failedResponse.TransactionId]);
    }

    private async Task<Dictionary<string, TransactionStatusDto>> WaitForFinalStatusesAsync(
        string completedId,
        string failedId)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var transactions = await client.GetFromJsonAsync<TransactionResponse[]>("/api/transactions") ?? [];
            var matching = transactions
                .Where(transaction => transaction.TransactionId == completedId || transaction.TransactionId == failedId)
                .ToDictionary(transaction => transaction.TransactionId, transaction => transaction.Status);

            if (matching.TryGetValue(completedId, out var completedStatus) &&
                matching.TryGetValue(failedId, out var failedStatus) &&
                completedStatus != TransactionStatusDto.Pending &&
                failedStatus != TransactionStatusDto.Pending)
            {
                return matching;
            }

            await Task.Delay(25);
        }

        throw new Xunit.Sdk.XunitException("Transactions did not reach final statuses within the timeout.");
    }

}