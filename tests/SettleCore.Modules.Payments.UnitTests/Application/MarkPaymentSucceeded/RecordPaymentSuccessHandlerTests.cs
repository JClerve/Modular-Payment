using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.UnitTests.Application.MarkPaymentSucceeded;

public sealed class RecordPaymentSuccessHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidInputSavesSucceededPaymentAndExactIntentTogether(bool alreadySucceeded)
    {
        var payment = Payment.Create(150m, "SGD");
        if (alreadySucceeded)
        {
            payment.MarkSucceeded();
        }
        var input = Input();
        var persistence = new RecordingPersistence();
        var handler = new RecordPaymentSuccessHandler(new RecordingPaymentRepository(payment), persistence);
        var result = await handler.HandleAsync(new RecordPaymentSuccessCommand(payment.Id.Value, input));
        Assert.NotNull(result);
        Assert.Equal(payment.Id.Value, result.PaymentId);
        Assert.Equal("Succeeded", result.Status);
        Assert.Same(payment, persistence.Payment);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        var intent = Assert.IsType<PaymentLedgerPostingIntent>(persistence.Intent);
        Assert.Equal(payment.Id, intent.PaymentId);
        Assert.Equal(input.TransactionId, intent.TransactionId);
        Assert.Equal(input.LedgerId, intent.LedgerId);
        Assert.Equal(input.ProcessorReceivableAccountId, intent.ProcessorReceivableAccountId);
        Assert.Equal(input.MerchantPayableAccountId, intent.MerchantPayableAccountId);
        Assert.Equal(input.PlatformRevenueAccountId, intent.PlatformRevenueAccountId);
        Assert.Equal("SGD", intent.Currency);
        Assert.Equal(15_000, intent.GrossAmountMinorUnits);
        Assert.Equal(input.FeeAmountMinorUnits, intent.FeeAmountMinorUnits);
        Assert.Equal(PaymentLedgerPostingIntentStatus.Pending, intent.Status);
        Assert.Equal(1, persistence.Calls);
    }

    [Fact]
    public async Task InvalidInputDoesNotChangeStatusOrWrite()
    {
        var payment = Payment.Create(150m, "SGD");
        var persistence = new RecordingPersistence();
        var handler = new RecordPaymentSuccessHandler(new RecordingPaymentRepository(payment), persistence);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => handler.HandleAsync(
            new RecordPaymentSuccessCommand(payment.Id.Value, Input() with { FeeAmountMinorUnits = 0 })));
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(0, persistence.Calls);
    }

    [Fact]
    public async Task MissingPaymentReturnsNullWithoutWriting()
    {
        var persistence = new RecordingPersistence();
        var handler = new RecordPaymentSuccessHandler(new RecordingPaymentRepository(null), persistence);
        Assert.Null(await handler.HandleAsync(new RecordPaymentSuccessCommand(Guid.NewGuid(), Input())));
        Assert.Equal(0, persistence.Calls);
    }

    [Fact]
    public async Task ProviderLinkedPaymentCannotBeCompletedManually()
    {
        var payment = Payment.Create(150m, "SGD");
        payment.AttachProviderReference(ProviderPaymentReference.Create("stripe", "pi_test"));
        var persistence = new RecordingPersistence();
        var handler = new RecordPaymentSuccessHandler(new RecordingPaymentRepository(payment), persistence);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new(payment.Id.Value, Input())));
        Assert.Equal(PaymentStatus.Pending, payment.Status);
        Assert.Equal(0, persistence.Calls);
    }

    private static PaymentLedgerPostingInput Input() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 300);

    private sealed class RecordingPersistence : IPaymentSuccessPersistence
    {
        public Payment? Payment { get; private set; }
        public PaymentLedgerPostingIntent? Intent { get; private set; }
        public int Calls { get; private set; }

        public Task SaveAsync(Payment payment, PaymentLedgerPostingIntent intent,
            CancellationToken cancellationToken = default)
        {
            Payment = payment;
            Intent = intent;
            Calls++;
            return Task.CompletedTask;
        }
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
            throw new NotSupportedException("Status must be saved through the atomic boundary.");
        }
    }
}
