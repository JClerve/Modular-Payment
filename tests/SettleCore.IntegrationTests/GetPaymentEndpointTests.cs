using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.GetPaymentById;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.IntegrationTests;

public sealed class GetPaymentEndpointTests
{
    [Fact]
    public async Task GetPaymentReturnsPaymentWhenItExists()
    {
        var payment = Payment.Create(
            75.25m,
            "sgd");

        using var factory =
            new PaymentsApiFactory(payment);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.GetAsync(
            $"/payments/{payment.Id.Value}");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var result =
            await response.Content
                .ReadFromJsonAsync<GetPaymentByIdResult>();

        var foundPayment =
            Assert.IsType<GetPaymentByIdResult>(result);

        Assert.Equal(
            payment.Id.Value,
            foundPayment.PaymentId);

        Assert.Equal(
            75.25m,
            foundPayment.Amount);

        Assert.Equal(
            "SGD",
            foundPayment.Currency);

        Assert.Equal(
            "Pending",
            foundPayment.Status);

        Assert.Null(
            foundPayment.Provider);

        Assert.Null(
            foundPayment.ProviderReference);
    }

    [Fact]
    public async Task GetPaymentReturnsProviderReferenceWhenAttached()
    {
        var payment = Payment.Create(
            75.25m,
            "sgd");

        payment.AttachProviderReference(
            ProviderPaymentReference.Create(
                "stripe",
                "pi_3ABC123"));

        using var factory =
            new PaymentsApiFactory(payment);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.GetAsync(
            $"/payments/{payment.Id.Value}");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var result =
            await response.Content
                .ReadFromJsonAsync<GetPaymentByIdResult>();

        var foundPayment =
            Assert.IsType<GetPaymentByIdResult>(result);

        Assert.Equal(
            payment.Id.Value,
            foundPayment.PaymentId);

        Assert.Equal(
            "stripe",
            foundPayment.Provider);

        Assert.Equal(
            "pi_3ABC123",
            foundPayment.ProviderReference);
    }

    [Fact]
    public async Task GetPaymentReturnsNotFoundWhenPaymentDoesNotExist()
    {
        using var factory =
            new PaymentsApiFactory(null);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.GetAsync(
            $"/payments/{Guid.NewGuid()}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task GetPaymentWithEmptyIdReturnsBadRequest()
    {
        using var factory =
            new PaymentsApiFactory(null);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.GetAsync(
            $"/payments/{Guid.Empty}");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    private sealed class PaymentsApiFactory(
        Payment? payment)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPaymentRepository>();

                services.AddSingleton<IPaymentRepository>(
                    new StubPaymentRepository(payment));
            });
        }
    }

    private sealed class StubPaymentRepository(
        Payment? payment)
        : IPaymentRepository
    {
        public Task AddAsync(
            Payment payment,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<Payment?> GetByIdAsync(
            PaymentId id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(payment);
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
