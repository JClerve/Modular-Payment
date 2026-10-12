using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.AttachProviderReference;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.IntegrationTests;

public sealed class AttachProviderReferenceEndpointTests
{
    [Fact]
    public async Task PostProviderReferenceAttachesReferenceToPayment()
    {
        var payment = Payment.Create(
            100.00m,
            "SGD");

        var repository =
            new RecordingPaymentRepository(payment);

        using var factory =
            new PaymentsApiFactory(repository);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.PostAsJsonAsync(
            $"/payments/{payment.Id.Value}/provider-reference",
            new
            {
                provider = "stripe",
                reference = "pi_3ABC123"
            });

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var result =
            await response.Content
                .ReadFromJsonAsync<AttachProviderReferenceResult>();

        var attached =
            Assert.IsType<AttachProviderReferenceResult>(result);

        Assert.Equal(
            payment.Id.Value,
            attached.PaymentId);

        Assert.Equal(
            "stripe",
            attached.Provider);

        Assert.Equal(
            "pi_3ABC123",
            attached.Reference);

        var providerReference =
            Assert.IsType<ProviderPaymentReference>(
                repository.UpdatedPayment?.ProviderReference);

        Assert.Equal(
            "stripe",
            providerReference.Provider);

        Assert.Equal(
            "pi_3ABC123",
            providerReference.Reference);
    }

    [Fact]
    public async Task PostProviderReferenceReturnsNotFoundWhenPaymentDoesNotExist()
    {
        var repository =
            new RecordingPaymentRepository(payment: null);

        using var factory =
            new PaymentsApiFactory(repository);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.PostAsJsonAsync(
            $"/payments/{Guid.NewGuid()}/provider-reference",
            new
            {
                provider = "stripe",
                reference = "pi_missing"
            });

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        Assert.Null(repository.UpdatedPayment);
    }

    private sealed class PaymentsApiFactory(
        IPaymentRepository repository)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPaymentRepository>();

                services.AddSingleton(repository);
                services.RemoveAll<IPaymentProviderReferencePersistence>();
                services.AddSingleton<IPaymentProviderReferencePersistence>((IPaymentProviderReferencePersistence)repository);
            });
        }
    }

    private sealed class RecordingPaymentRepository(
        Payment? payment)
        : IPaymentRepository, IPaymentProviderReferencePersistence
    {
        public Payment? UpdatedPayment { get; private set; }

        public Task<bool> AttachAsync(PaymentId id, ProviderPaymentReference reference, CancellationToken cancellationToken = default)
        {
            if (payment is null || payment.Id != id) return Task.FromResult(false);
            payment.AttachProviderReference(reference);
            UpdatedPayment = payment;
            return Task.FromResult(true);
        }

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
            return Task.FromResult<Payment?>(
                payment is not null &&
                id == payment.Id
                    ? payment
                    : null);
        }

        public Task UpdateAsync(
            Payment payment,
            CancellationToken cancellationToken = default)
        {
            UpdatedPayment = payment;

            return Task.CompletedTask;
        }

        public Task<Payment?> GetByProviderReferenceAsync(
            ProviderPaymentReference providerReference,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    [Fact]
    public async Task PostProviderReferenceReturnsBadRequestForBlankProvider()
    {
        var payment = Payment.Create(
            100.00m,
            "SGD");

        var repository =
            new RecordingPaymentRepository(payment);

        using var factory =
            new PaymentsApiFactory(repository);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.PostAsJsonAsync(
            $"/payments/{payment.Id.Value}/provider-reference",
            new
            {
                provider = " ",
                reference = "pi_3ABC123"
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Null(repository.UpdatedPayment);
    }

    [Fact]
    public async Task PostProviderReferenceReturnsBadRequestForBlankReference()
    {
        var payment = Payment.Create(
            100.00m,
            "SGD");

        var repository =
            new RecordingPaymentRepository(payment);

        using var factory =
            new PaymentsApiFactory(repository);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.PostAsJsonAsync(
            $"/payments/{payment.Id.Value}/provider-reference",
            new
            {
                provider = "stripe",
                reference = " "
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Null(repository.UpdatedPayment);
    }
}
