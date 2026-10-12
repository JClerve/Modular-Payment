using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.CreatePayment;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.IntegrationTests;

public sealed class CreatePaymentEndpointTests
{
    [Fact]
    public async Task PostPaymentsCreatesPayment()
    {
        using var factory = new PaymentsApiFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.PostAsJsonAsync(
            "/payments",
            new
            {
                amount = 125.50m,
                currency = "sgd"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var result =
            await response.Content
                .ReadFromJsonAsync<CreatePaymentResult>();

        var createdPayment =
            Assert.IsType<CreatePaymentResult>(result);

        Assert.NotEqual(
            Guid.Empty,
            createdPayment.PaymentId);

        Assert.Equal(
            125.50m,
            createdPayment.Amount);

        Assert.Equal(
            "SGD",
            createdPayment.Currency);

        Assert.Equal(
            "Pending",
            createdPayment.Status);

        Assert.Equal(
            $"/payments/{createdPayment.PaymentId}",
            response.Headers.Location?.OriginalString);

        var persistedPayment =
            Assert.IsType<Payment>(
                factory.Repository.AddedPayment);

        Assert.Equal(
            createdPayment.PaymentId,
            persistedPayment.Id.Value);
    }

    [Fact]
    public async Task PostPaymentsWithNonPositiveAmountReturnsBadRequest()
    {
        using var factory = new PaymentsApiFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.PostAsJsonAsync(
            "/payments",
            new
            {
                amount = 0m,
                currency = "SGD"
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Null(factory.Repository.AddedPayment);
    }

    [Theory]
    [InlineData("")]
    [InlineData("SG")]
    [InlineData("123")]
    public async Task PostPaymentsWithInvalidCurrencyReturnsBadRequest(
        string currency)
    {
        using var factory = new PaymentsApiFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.PostAsJsonAsync(
            "/payments",
            new
            {
                amount = 100m,
                currency
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Null(factory.Repository.AddedPayment);
    }

    private sealed class PaymentsApiFactory
        : WebApplicationFactory<Program>
    {
        public RecordingPaymentRepository Repository { get; } =
            new();

        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPaymentRepository>();

                services.AddSingleton<IPaymentRepository>(
                    Repository);
            });
        }
    }

    public sealed class RecordingPaymentRepository
        : IPaymentRepository
    {
        public Payment? AddedPayment { get; private set; }

        public Task AddAsync(
            Payment payment,
            CancellationToken cancellationToken = default)
        {
            AddedPayment = payment;

            return Task.CompletedTask;
        }

        public Task<Payment?> GetByIdAsync(
            PaymentId id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Payment?>(null);
        }

        public Task UpdateAsync(
            Payment payment,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Payment?> GetByProviderReferenceAsync(
            ProviderPaymentReference providerReference,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
