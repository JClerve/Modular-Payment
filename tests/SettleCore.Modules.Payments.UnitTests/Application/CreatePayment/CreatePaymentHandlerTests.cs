using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.CreatePayment;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.UnitTests.Application.CreatePayment;

public sealed class CreatePaymentHandlerTests
{
    [Fact]
    public async Task HandleCreatesAndPersistsPayment()
    {
        var repository = new RecordingPaymentRepository();
        var handler = new CreatePaymentHandler(repository);

        var result = await handler.HandleAsync(
            new CreatePaymentCommand(125.50m, "sgd"));

        var persistedPayment =
            Assert.IsType<Payment>(repository.AddedPayment);

        Assert.Equal(
            persistedPayment.Id.Value,
            result.PaymentId);

        Assert.Equal(
            125.50m,
            result.Amount);

        Assert.Equal(
            "SGD",
            result.Currency);

        Assert.Equal(
            "Pending",
            result.Status);

        Assert.Equal(
            result.PaymentId,
            persistedPayment.Id.Value);

        Assert.Equal(
            result.Amount,
            persistedPayment.Amount);

        Assert.Equal(
            result.Currency,
            persistedPayment.Currency);
    }

    private sealed class RecordingPaymentRepository
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
            throw new NotSupportedException();
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
            throw new NotSupportedException();
        }
    }
}
