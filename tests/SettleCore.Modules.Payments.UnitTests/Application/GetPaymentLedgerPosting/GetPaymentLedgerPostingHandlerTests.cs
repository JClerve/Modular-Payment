using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.GetPaymentLedgerPosting;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.UnitTests.Application.GetPaymentLedgerPosting;

public sealed class GetPaymentLedgerPostingHandlerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReturnsPersistedIdentityAndStatusWithoutWriting(bool posted)
    {
        var intent = PaymentLedgerPostingIntent.Create(PaymentId.New(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SGD", 10_000, 300);
        if (posted) intent.MarkPosted();
        var repository = new ReadOnlyRepository(intent);
        using var cancellation = new CancellationTokenSource();
        var result = await new GetPaymentLedgerPostingHandler(repository).HandleAsync(
            new GetPaymentLedgerPostingQuery(intent.PaymentId.Value), cancellation.Token);
        Assert.NotNull(result);
        Assert.Equal(intent.PaymentId.Value, result.PaymentId);
        Assert.Equal(intent.TransactionId, result.TransactionId);
        Assert.Equal(posted ? "Posted" : "Pending", result.Status);
        Assert.Null(result.NextAttemptAt);
        Assert.Null(result.PostedAt);
        Assert.Equal(intent.PaymentId, repository.RequestedId);
        Assert.Equal(cancellation.Token, repository.Token);
    }

    [Fact]
    public async Task MissingIntentReturnsNull()
    {
        Assert.Null(await new GetPaymentLedgerPostingHandler(new ReadOnlyRepository(null))
            .HandleAsync(new GetPaymentLedgerPostingQuery(Guid.NewGuid())));
    }

    [Fact]
    public async Task RejectsEmptyPaymentIdentityBeforeReading()
    {
        var repository = new ReadOnlyRepository(null);
        await Assert.ThrowsAsync<ArgumentException>(() => new GetPaymentLedgerPostingHandler(repository)
            .HandleAsync(new GetPaymentLedgerPostingQuery(Guid.Empty)));
        Assert.Null(repository.RequestedId);
    }

    private sealed class ReadOnlyRepository(PaymentLedgerPostingIntent? intent) : IPaymentLedgerPostingIntentRepository
    {
        public PaymentId? RequestedId { get; private set; }
        public CancellationToken Token { get; private set; }
        public Task<PaymentLedgerPostingIntent?> GetByPaymentIdAsync(PaymentId paymentId,
            CancellationToken cancellationToken = default)
        {
            RequestedId = paymentId;
            Token = cancellationToken;
            return Task.FromResult(intent);
        }
        public Task AddAsync(PaymentLedgerPostingIntent intent, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> MarkPostedAsync(PaymentId paymentId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<bool> ScheduleRetryAsync(PaymentId paymentId, DateTimeOffset nextAttemptAt,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PaymentLedgerPostingIntent>> GetPendingAsync(int limit,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
