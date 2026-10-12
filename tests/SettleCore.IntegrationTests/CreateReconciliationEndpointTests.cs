using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SettleCore.Modules.Reconciliation.Application;
using SettleCore.Modules.Reconciliation.Application.CreateReconciliation;
using SettleCore.Modules.Reconciliation.Application.GetReconciliationById;
using SettleCore.Modules.Reconciliation.Domain;

namespace SettleCore.IntegrationTests;

public sealed class CreateReconciliationEndpointTests
{
    [Fact]
    public async Task PostReconciliationsCreatesAndPersistsComparison()
    {
        using var factory = new ReconciliationApiFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.PostAsJsonAsync(
            "/reconciliations",
            new
            {
                expectedAmount = 100.00m,
                expectedCurrency = "sgd",
                actualAmount = 90.00m,
                actualCurrency = "sgd"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = Assert.IsType<CreateReconciliationResult>(
            await response.Content.ReadFromJsonAsync<CreateReconciliationResult>());

        Assert.NotEqual(Guid.Empty, created.ReconciliationId);
        Assert.Equal(ReconciliationStatus.AmountMismatch, created.Status);
        Assert.Equal(100.00m, created.ExpectedAmount);
        Assert.Equal(90.00m, created.ActualAmount);
        Assert.Equal("SGD", created.ExpectedCurrency);
        Assert.Equal("SGD", created.ActualCurrency);
        Assert.Equal(
            $"/reconciliations/{created.ReconciliationId}",
            response.Headers.Location?.OriginalString);

        var persisted = Assert.IsType<ReconciliationRecord>(
            factory.Repository.AddedRecord);
        Assert.Equal(created.ReconciliationId, persisted.Id);
    }

    [Theory]
    [InlineData("", "SGD")]
    [InlineData("SG", "SGD")]
    [InlineData("SGD", "123")]
    public async Task PostReconciliationsWithInvalidCurrencyReturnsBadRequest(
        string expectedCurrency,
        string actualCurrency)
    {
        using var factory = new ReconciliationApiFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.PostAsJsonAsync(
            "/reconciliations",
            new
            {
                expectedAmount = 100.00m,
                expectedCurrency,
                actualAmount = 100.00m,
                actualCurrency
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(factory.Repository.AddedRecord);
    }

    [Theory]
    [InlineData(100, "sgd", 100, "SGD", ReconciliationStatus.Matched)]
    [InlineData(100, "sgd", 90, "SGD", ReconciliationStatus.AmountMismatch)]
    [InlineData(100, "sgd", 100, "usd", ReconciliationStatus.CurrencyMismatch)]
    [InlineData(100, "sgd", 90, "usd", ReconciliationStatus.Mismatch)]
    public async Task PostReconciliationsReturnsComparisonStatus(
        int expectedAmount,
        string expectedCurrency,
        int actualAmount,
        string actualCurrency,
        ReconciliationStatus expectedStatus)
    {
        using var factory = new ReconciliationApiFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.PostAsJsonAsync(
            "/reconciliations",
            new
            {
                expectedAmount,
                expectedCurrency,
                actualAmount,
                actualCurrency
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = Assert.IsType<CreateReconciliationResult>(
            await response.Content.ReadFromJsonAsync<CreateReconciliationResult>());
        Assert.Equal(expectedStatus, created.Status);
        Assert.Equal("SGD", created.ExpectedCurrency);
        Assert.Equal(actualCurrency.ToUpperInvariant(), created.ActualCurrency);
        Assert.Equal(expectedStatus, factory.Repository.AddedRecord?.Status);
    }

    [Fact]
    public async Task GetReconciliationReturnsCreatedRecord()
    {
        using var factory = new ReconciliationApiFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var createResponse = await client.PostAsJsonAsync(
            "/reconciliations",
            new
            {
                expectedAmount = 100.00m,
                expectedCurrency = "SGD",
                actualAmount = 90.00m,
                actualCurrency = "SGD"
            });
        var created = Assert.IsType<CreateReconciliationResult>(
            await createResponse.Content.ReadFromJsonAsync<CreateReconciliationResult>());

        var getResponse = await client.GetAsync(
            $"/reconciliations/{created.ReconciliationId}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = Assert.IsType<GetReconciliationByIdResult>(
            await getResponse.Content.ReadFromJsonAsync<GetReconciliationByIdResult>());
        Assert.Equal(created.ReconciliationId, fetched.ReconciliationId);
        Assert.Equal(created.Status, fetched.Status);
        Assert.Equal(created.ExpectedAmount, fetched.ExpectedAmount);
        Assert.Equal(created.ActualAmount, fetched.ActualAmount);
    }

    [Fact]
    public async Task GetMissingReconciliationReturnsNotFound()
    {
        using var factory = new ReconciliationApiFactory();
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.GetAsync(
            $"/reconciliations/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class ReconciliationApiFactory : WebApplicationFactory<Program>
    {
        public RecordingReconciliationRepository Repository { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IReconciliationRepository>();
                services.AddSingleton<IReconciliationRepository>(Repository);
            });
        }
    }

    public sealed class RecordingReconciliationRepository : IReconciliationRepository
    {
        public ReconciliationRecord? AddedRecord { get; private set; }

        public Task AddAsync(
            ReconciliationRecord record,
            CancellationToken cancellationToken = default)
        {
            AddedRecord = record;
            return Task.CompletedTask;
        }

        public Task<ReconciliationRecord?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                AddedRecord?.Id == id ? AddedRecord : null);
        }
    }
}
