using System.Net;
using System.Net.Http.Json;
using FinancialMonitor.Api.Domain.Entities;
using FinancialMonitor.Api.Presentation.Dtos;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinancialMonitor.Tests.Api;

public sealed class TransactionsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;

    public TransactionsEndpointTests(WebApplicationFactory<Program> factory)
    {
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostTransactions_ValidTransaction_ReturnsCreated()
    {
        var request = new { amount = 1500.50m, currency = "USD" };

        var response = await client.PostAsJsonAsync("/api/transactions", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var saved = await response.Content.ReadFromJsonAsync<TransactionResponse>();
        Assert.NotNull(saved);
        Assert.NotEqual(Guid.Empty.ToString(), saved!.TransactionId);
        Assert.Equal(request.amount, saved.Amount);
        Assert.Equal(request.currency, saved.Currency);
        Assert.Equal(TransactionStatus.Completed, (TransactionStatus)saved.Status);
        Assert.Equal($"/api/transactions/{saved.TransactionId}", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task PostTransactions_JsonStringStatus_ReturnsCreated()
    {
        using var content = JsonContent.Create(new
        {
            amount = 1500.50m,
            currency = "USD"
        });

        var response = await client.PostAsync("/api/transactions", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task PostTransactions_InvalidTransaction_ReturnsBadRequest()
    {
        var transaction = new { amount = -1m, currency = "" };

        var response = await client.PostAsJsonAsync("/api/transactions", transaction);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTransactions_ReturnsStoredTransactions()
    {
        var postResponse = await client.PostAsJsonAsync(
            "/api/transactions",
            new { amount = 200m, currency = "EUR" });
        var created = await postResponse.Content.ReadFromJsonAsync<TransactionResponse>();
        Assert.NotNull(created);

        var response = await client.GetAsync("/api/transactions");
        var stored = await response.Content.ReadFromJsonAsync<TransactionResponse[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(stored!, saved => saved.TransactionId == created!.TransactionId);
    }

    [Theory]
    [InlineData(1500, TransactionStatus.Completed)]
    [InlineData(10001, TransactionStatus.Failed)]
    public async Task PostThenGet_ReturnsProcessedFinalStatus(
        decimal amount,
        TransactionStatus expectedStatus)
    {
        var transaction = new Transaction(
            Guid.NewGuid().ToString(),
            amount,
            "USD",
            TransactionStatus.Pending,
            DateTime.UtcNow);

        var postResponse = await client.PostAsJsonAsync(
            "/api/transactions",
            new { amount = transaction.Amount, currency = transaction.Currency });
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var created = await postResponse.Content.ReadFromJsonAsync<TransactionResponse>();
        Assert.NotNull(created);
        TransactionResponse? stored = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var transactions = await client.GetFromJsonAsync<TransactionResponse[]>("/api/transactions");
            stored = transactions?.SingleOrDefault(item => item.TransactionId == created!.TransactionId);
            if (stored is not null && (TransactionStatus)stored.Status == expectedStatus) break;
            await Task.Delay(25);
        }

        Assert.NotNull(stored);
        Assert.Equal(expectedStatus, (TransactionStatus)stored!.Status);
    }
}