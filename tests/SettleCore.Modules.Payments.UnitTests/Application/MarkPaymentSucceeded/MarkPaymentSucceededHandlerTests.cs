using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.UnitTests.Application.MarkPaymentSucceeded;

public sealed class MarkPaymentSucceededHandlerTests
{
    [Fact]
    public async Task HandleMarksPaymentSucceededAndPersistsUpdate()
    {
        var payment =
            Payment.Create(150.00m, "SGD");

        var repository =
            new RecordingPaymentRepository(payment);

        var handler =
            new MarkPaymentSucceededHandler(repository);

        var result = await handler.HandleAsync(
            new MarkPaymentSucceededCommand(
                payment.Id.Value));

        var updatedPayment =
            Assert.IsType<Payment>(
                repository.UpdatedPayment);

        var succeededPayment =
            Assert.IsType<MarkPaymentSucceededResult>(
                result);

        Assert.Equal(
            PaymentStatus.Succeeded,
            updatedPayment.Status);

        Assert.Equal(
            payment.Id.Value,
            succeededPayment.PaymentId);

        Assert.Equal(
            "Succeeded",
            succeededPayment.Status);
    }

    [Fact]
    public async Task HandleReturnsNullWhenPaymentDoesNotExist()
    {
        var repository =
            new RecordingPaymentRepository(null);

        var handler =
            new MarkPaymentSucceededHandler(repository);

        var result = await handler.HandleAsync(
            new MarkPaymentSucceededCommand(
                Guid.NewGuid()));

        Assert.Null(result);
        Assert.Null(repository.UpdatedPayment);
    }

    [Fact]
    public async Task ProviderLinkedPaymentCannotUseStatusOnlyManualCompletion()
    {
        var payment = Payment.Create(150m, "SGD");
        payment.AttachProviderReference(ProviderPaymentReference.Create("stripe", "pi_test"));
        var repository = new RecordingPaymentRepository(payment);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new MarkPaymentSucceededHandler(repository)
            .HandleAsync(new(payment.Id.Value)));
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Null(repository.UpdatedPayment);
    }

    private sealed class RecordingPaymentRepository(
        Payment? payment)
        : IPaymentRepository
    {
        public Payment? UpdatedPayment { get; private set; }

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
