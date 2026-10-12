using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.DispatchPaymentLedgerPosting;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.UnitTests.Application.DispatchPaymentLedgerPosting;

public sealed class DispatchPaymentLedgerPostingHandlerTests
{
    [Fact]
    public async Task PostsExactPayloadBeforeAcknowledgmentAndSkipsCompletedIntent()
    {
        var repository = new RecordingRepository(CreateIntent());
        var port = new RecordingPort(repository);
        var handler = new DispatchPaymentLedgerPostingHandler(repository, port);
        var intent = repository.Intent!;
        Assert.True(await handler.HandleAsync(new DispatchPaymentLedgerPostingCommand(intent.PaymentId.Value)));
        var posted = Assert.Single(port.Requests);
        Assert.Equal(new PaymentLedgerPostingRequest(intent.PaymentId.Value, intent.TransactionId,
            intent.LedgerId, intent.ProcessorReceivableAccountId, intent.MerchantPayableAccountId,
            intent.PlatformRevenueAccountId, intent.Currency, intent.GrossAmountMinorUnits,
            intent.FeeAmountMinorUnits), posted);
        Assert.Equal(PaymentLedgerPostingIntentStatus.Posted, intent.Status);
        Assert.True(await handler.HandleAsync(new DispatchPaymentLedgerPostingCommand(intent.PaymentId.Value)));
        Assert.Single(port.Requests);
        Assert.Equal(1, repository.Acknowledgments);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureRemainsPendingAndRetriesSameTransaction(bool failureAfterPosting)
    {
        var repository = new RecordingRepository(CreateIntent()) { FailAcknowledgment = failureAfterPosting };
        var port = new RecordingPort(repository) { FailPosting = !failureAfterPosting };
        var handler = new DispatchPaymentLedgerPostingHandler(repository, port);
        var command = new DispatchPaymentLedgerPostingCommand(repository.Intent!.PaymentId.Value);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));
        Assert.Equal(PaymentLedgerPostingIntentStatus.Pending, repository.Intent.Status);
        Assert.Equal(failureAfterPosting ? 1 : 0, repository.Acknowledgments);
        repository.FailAcknowledgment = false;
        port.FailPosting = false;
        Assert.True(await handler.HandleAsync(command));
        Assert.Equal(2, port.Requests.Count);
        Assert.Equal(port.Requests[0], port.Requests[1]);
        Assert.Equal(PaymentLedgerPostingIntentStatus.Posted, repository.Intent.Status);
    }

    [Fact]
    public async Task MissingIntentDoesNotPostOrAcknowledge()
    {
        var repository = new RecordingRepository(null);
        var port = new RecordingPort(repository);
        Assert.False(await new DispatchPaymentLedgerPostingHandler(repository, port)
            .HandleAsync(new DispatchPaymentLedgerPostingCommand(Guid.NewGuid())));
        Assert.Empty(port.Requests);
        Assert.Equal(0, repository.Acknowledgments);
    }

    private static PaymentLedgerPostingIntent CreateIntent() => PaymentLedgerPostingIntent.Create(
        PaymentId.New(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), "SGD", 10_000, 300);

    private sealed class RecordingRepository(PaymentLedgerPostingIntent? intent)
        : IPaymentLedgerPostingIntentRepository
    {
        public PaymentLedgerPostingIntent? Intent { get; } = intent;
        public int Acknowledgments { get; private set; }
        public bool FailAcknowledgment { get; set; }
        public Task<IReadOnlyList<PaymentLedgerPostingIntent>> GetPendingAsync(
            int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ScheduleRetryAsync(PaymentId paymentId, DateTimeOffset nextAttemptAt,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(PaymentLedgerPostingIntent intent, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<PaymentLedgerPostingIntent?> GetByPaymentIdAsync(PaymentId paymentId,
            CancellationToken cancellationToken = default) => Task.FromResult(Intent);
        public Task<bool> MarkPostedAsync(PaymentId paymentId, CancellationToken cancellationToken = default)
        {
            Acknowledgments++;
            if (FailAcknowledgment)
            {
                throw new InvalidOperationException("Acknowledgment failed.");
            }
            Intent!.MarkPosted();
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingPort(RecordingRepository repository) : IPaymentLedgerPostingPort
    {
        public List<PaymentLedgerPostingRequest> Requests { get; } = [];
        public bool FailPosting { get; set; }
        public Task PostAsync(PaymentLedgerPostingRequest request, CancellationToken cancellationToken = default)
        {
            Assert.Equal(PaymentLedgerPostingIntentStatus.Pending, repository.Intent!.Status);
            Requests.Add(request);
            if (FailPosting)
            {
                throw new InvalidOperationException("Posting failed.");
            }
            return Task.CompletedTask;
        }
    }
}
