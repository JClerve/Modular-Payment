using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.GetPaymentByProviderReference;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.IntegrationTests;

public sealed class GetPaymentByProviderReferenceEndpointTests
{
    [Fact]
    public async Task GetPaymentByProviderReferenceReturnsMatchingPayment()
    {
        var payment = Payment.Create(
            100.00m,
            "SGD");

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
            "/payments/by-provider-reference" +
            "?provider=stripe" +
            "&reference=pi_3ABC123");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var result =
            await response.Content
                .ReadFromJsonAsync<GetPaymentByProviderReferenceResult>();

        var found =
            Assert.IsType<GetPaymentByProviderReferenceResult>(
                result);

        Assert.Equal(
            payment.Id.Value,
            found.PaymentId);

        Assert.Equal(
            "stripe",
            found.Provider);

        Assert.Equal(
            "pi_3ABC123",
            found.ProviderReference);
    }

    private sealed class PaymentsApiFactory(
        Payment payment)
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
        Payment payment)
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
            throw new NotSupportedException();
        }

        public Task<Payment?> GetByProviderReferenceAsync(
            ProviderPaymentReference providerReference,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Payment?>(
                payment.ProviderReference == providerReference
                    ? payment
                    : null);
        }

        public Task UpdateAsync(
            Payment payment,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    [Fact]
    public async Task GetPaymentByProviderReferenceReturnsNotFoundWhenNoPaymentMatches()
    {
        var payment = Payment.Create(
            100.00m,
            "SGD");

        payment.AttachProviderReference(
            ProviderPaymentReference.Create(
                "stripe",
                "pi_existing"));

        using var factory =
            new PaymentsApiFactory(payment);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.GetAsync(
            "/payments/by-provider-reference" +
            "?provider=stripe" +
            "&reference=pi_missing");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task GetPaymentByProviderReferenceReturnsBadRequestForBlankProvider()
    {
        var payment = Payment.Create(
            100.00m,
            "SGD");

        payment.AttachProviderReference(
            ProviderPaymentReference.Create(
                "stripe",
                "pi_existing"));

        using var factory =
            new PaymentsApiFactory(payment);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.GetAsync(
            "/payments/by-provider-reference" +
            "?provider=%20" +
            "&reference=pi_existing");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task GetPaymentByProviderReferenceReturnsBadRequestForBlankReference()
    {
        var payment = Payment.Create(
            100.00m,
            "SGD");

        payment.AttachProviderReference(
            ProviderPaymentReference.Create(
                "stripe",
                "pi_existing"));

        using var factory =
            new PaymentsApiFactory(payment);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.GetAsync(
            "/payments/by-provider-reference" +
            "?provider=stripe" +
            "&reference=%20");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }
}
