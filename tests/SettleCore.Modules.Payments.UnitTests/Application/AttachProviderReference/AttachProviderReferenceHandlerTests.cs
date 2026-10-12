using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.AttachProviderReference;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.UnitTests.Application.AttachProviderReference;

public sealed class AttachProviderReferenceHandlerTests
{
    [Fact]
    public async Task HandleAsyncAttachesProviderReferenceAndPersistsPayment()
    {
        var payment = Payment.Create(
            100.00m,
            "SGD");

        var repository =
            new RecordingPaymentRepository(payment);

        var handler =
            new AttachProviderReferenceHandler(repository, repository);

        var result = await handler.HandleAsync(
            new AttachProviderReferenceCommand(
                payment.Id.Value,
                "stripe",
                "pi_3ABC123"));

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
    public async Task HandleAsyncReturnsNullWhenPaymentDoesNotExist()
    {
        var repository =
            new RecordingPaymentRepository(payment: null);

        var handler =
            new AttachProviderReferenceHandler(repository, repository);

        var result = await handler.HandleAsync(
            new AttachProviderReferenceCommand(
                Guid.NewGuid(),
                "stripe",
                "pi_missing"));

        Assert.Null(result);
        Assert.Null(repository.UpdatedPayment);
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

        public Task<Payment?> GetByProviderReferenceAsync(
            ProviderPaymentReference providerReference,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateAsync(
            Payment payment,
            CancellationToken cancellationToken = default)
        {
            UpdatedPayment = payment;

            return Task.CompletedTask;
        }
    }
}
