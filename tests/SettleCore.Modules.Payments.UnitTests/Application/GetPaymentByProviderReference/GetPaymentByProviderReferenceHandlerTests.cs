using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.GetPaymentByProviderReference;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.UnitTests.Application.GetPaymentByProviderReference;

public sealed class GetPaymentByProviderReferenceHandlerTests
{
    [Fact]
    public async Task HandleAsyncReturnsMatchingPayment()
    {
        var payment = Payment.Create(
            100.00m,
            "SGD");

        payment.AttachProviderReference(
            ProviderPaymentReference.Create(
                "stripe",
                "pi_3ABC123"));

        var repository =
            new StubPaymentRepository(payment);

        var handler =
            new GetPaymentByProviderReferenceHandler(repository);

        var result = await handler.HandleAsync(
            new GetPaymentByProviderReferenceQuery(
                "stripe",
                "pi_3ABC123"));

        var found =
            Assert.IsType<GetPaymentByProviderReferenceResult>(result);

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
    public async Task HandleAsyncReturnsNullWhenNoPaymentMatches()
    {
        var payment = Payment.Create(
            100.00m,
            "SGD");

        payment.AttachProviderReference(
            ProviderPaymentReference.Create(
                "stripe",
                "pi_existing"));

        var repository =
            new StubPaymentRepository(payment);

        var handler =
            new GetPaymentByProviderReferenceHandler(repository);

        var result = await handler.HandleAsync(
            new GetPaymentByProviderReferenceQuery(
                "stripe",
                "pi_missing"));

        Assert.Null(result);
    }
}
