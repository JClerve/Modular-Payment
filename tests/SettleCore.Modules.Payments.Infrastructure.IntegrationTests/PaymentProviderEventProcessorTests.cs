using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;
using SettleCore.Modules.Payments.Application.Providers;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace SettleCore.Modules.Payments.Infrastructure.IntegrationTests;

public sealed class PaymentProviderEventProcessorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 4, 0, 0, TimeSpan.Zero);
    public Task InitializeAsync() => _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync();
    private DbContextOptions<PaymentsDbContext> Options => new DbContextOptionsBuilder<PaymentsDbContext>()
        .UseNpgsql(_postgres.GetConnectionString()).Options;

    [Fact]
    public async Task CorrelatedReceiptAtomicallyCompletesPaymentAndReplayPreservesFirstProcessingTime()
    {
        var evidence = await Seed();
        await using var first = new PaymentsDbContext(Options);
        Assert.True(await Processor(first).ProcessAsync("stripe", evidence.EventId, false));
        await using var replay = new PaymentsDbContext(Options);
        Assert.True(await new EfPaymentProviderEventProcessor(replay, new Clock(Now.AddHours(1)))
            .ProcessAsync("stripe", evidence.EventId, false));
        await AssertCompleted(evidence.PaymentId);
    }

    [Theory]
    [InlineData("payment")]
    [InlineData("reference")]
    [InlineData("preparation")]
    [InlineData("event")]
    public async Task MissingPrerequisitesRemainPendingWithoutPartialWrites(string missing)
    {
        var evidence = await Seed(missing);
        await using var context = new PaymentsDbContext(Options);
        Assert.False(await Processor(context).ProcessAsync("stripe", missing == "event" ? "evt_absent" : evidence.EventId, false));
        await AssertUnprocessed();
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("reference")]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("mode")]
    public async Task MismatchingStoredEvidenceCannotAuthorizeSuccess(string mismatch)
    {
        var evidence = await Seed(receive: false);
        evidence = mismatch switch
        {
            "identity" => evidence with { PaymentId = PaymentId.New() },
            "reference" => evidence with { ProviderReference = ProviderPaymentReference.Create("stripe", "pi_other") },
            "amount" => evidence with { AmountMinorUnits = 1235 },
            "currency" => evidence with { Currency = "JPY" },
            "mode" => evidence with { IsLiveMode = true },
            _ => evidence
        };
        await using var context = new PaymentsDbContext(Options);
        await new EfPaymentProviderEventInbox(context, new Clock(Now)).ReceiveAsync(evidence);
        if (mismatch == "identity")
            Assert.False(await Processor(context).ProcessAsync("stripe", evidence.EventId, false));
        else
            await Assert.ThrowsAsync<ProviderPaymentCorrelationException>(() => Processor(context).ProcessAsync("stripe", evidence.EventId, false));
        await AssertUnprocessed();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentSameOrDifferentEventDeliveriesCreateOneSuccessIntentAndAudit(bool distinctEvents)
    {
        var evidence = await Seed();
        if (distinctEvents)
        {
            await using var seed = new PaymentsDbContext(Options);
            await new EfPaymentProviderEventInbox(seed, new Clock(Now)).ReceiveAsync(evidence with { EventId = "evt_second" });
        }
        await using var first = new PaymentsDbContext(Options);
        await using var second = new PaymentsDbContext(Options);
        Assert.All(await Task.WhenAll(Processor(first).ProcessAsync("stripe", evidence.EventId, false),
            Processor(second).ProcessAsync("stripe", distinctEvents ? "evt_second" : evidence.EventId, false)), Assert.True);
        await AssertCompleted(evidence.PaymentId, distinctEvents ? 2 : 1);
    }

    [Fact]
    public async Task ReceiptAcknowledgmentFailureRollsBackSuccessIntentAndAuditAndAllowsRetry()
    {
        var evidence = await Seed();
        await using var context = new PaymentsDbContext(Options);
        await context.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_processed_receipt() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'test acknowledgment failure'; END; $$;
            CREATE TRIGGER reject_processed_receipt BEFORE UPDATE ON payment_provider_event_receipts
            FOR EACH ROW EXECUTE FUNCTION reject_processed_receipt();
            """);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => Processor(context).ProcessAsync("stripe", evidence.EventId, false));
        await AssertUnprocessed();
        await context.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_processed_receipt ON payment_provider_event_receipts;");
        Assert.True(await Processor(context).ProcessAsync("stripe", evidence.EventId, false));
        await AssertCompleted(evidence.PaymentId);
    }

    [Fact]
    public async Task EarlyReceiptCanBeProcessedAfterLocalProviderReferenceArrives()
    {
        var evidence = await Seed("reference");
        await using var context = new PaymentsDbContext(Options);
        Assert.False(await Processor(context).ProcessAsync("stripe", evidence.EventId, false));
        await using (var referenceWriter = new PaymentsDbContext(Options))
        {
            var payment = await referenceWriter.Payments.SingleAsync();
            payment.AttachProviderReference(evidence.ProviderReference);
            await referenceWriter.SaveChangesAsync();
        }
        Assert.True(await Processor(context).ProcessAsync("stripe", evidence.EventId, false));
        await AssertCompleted(evidence.PaymentId);
    }

    [Fact]
    public async Task ConcurrentRetrySchedulingCannotReviveProcessedReceipt()
    {
        var evidence = await Seed();
        await using var processing = new PaymentsDbContext(Options);
        await using var scheduling = new PaymentsDbContext(Options);
        var outcomes = await Task.WhenAll(Processor(processing).ProcessAsync("stripe", evidence.EventId, false),
            new EfPaymentProviderEventInbox(scheduling, new Clock(Now)).ScheduleRetryAsync("stripe", evidence.EventId, Now.AddMinutes(1)));
        Assert.True(outcomes[0]);
        await using var reader = new PaymentsDbContext(Options);
        var receipt = await reader.PaymentProviderEventReceipts.SingleAsync();
        Assert.Equal(Now, receipt.ProcessedAt);
        Assert.Null(receipt.NextAttemptAt);
    }

    private async Task<ProviderPaymentSucceededEvent> Seed(string? missing = null, bool receive = true)
    {
        await using var context = new PaymentsDbContext(Options);
        await context.Database.MigrateAsync();
        var payment = Payment.Create(12.34m, "SGD");
        if (missing != "reference") payment.AttachProviderReference(ProviderPaymentReference.Create("stripe", "pi_test"));
        if (missing != "payment")
        {
            context.Payments.Add(payment);
            await context.SaveChangesAsync();
            if (missing != "preparation")
                await new EfPaymentLedgerPostingPreparationRepository(context).SaveAsync(
                    PaymentLedgerPostingRequestFactory.Create(payment, new PaymentLedgerPostingInput(
                        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 100)));
        }
        var evidence = new ProviderPaymentSucceededEvent("evt_test", payment.Id,
            ProviderPaymentReference.Create("stripe", "pi_test"), 1234, "SGD", Now.AddMinutes(-1), false);
        if (receive) await new EfPaymentProviderEventInbox(context, new Clock(Now)).ReceiveAsync(evidence);
        return evidence;
    }

    private async Task AssertUnprocessed()
    {
        await using var reader = new PaymentsDbContext(Options);
        Assert.All(await reader.Payments.ToListAsync(), payment => Assert.Equal(PaymentStatus.Pending, payment.Status));
        Assert.All(await reader.PaymentProviderEventReceipts.ToListAsync(), receipt => Assert.Null(receipt.ProcessedAt));
        Assert.Empty(await reader.PaymentLedgerPostingIntents.ToListAsync());
        Assert.Empty(await reader.PaymentLedgerPostingEvents.ToListAsync());
    }

    private async Task AssertCompleted(PaymentId id, int receiptCount = 1)
    {
        await using var reader = new PaymentsDbContext(Options);
        Assert.Equal(PaymentStatus.Succeeded, (await reader.Payments.SingleAsync()).Status);
        var intent = await reader.PaymentLedgerPostingIntents.SingleAsync();
        Assert.Equal(id, intent.PaymentId);
        Assert.Equal(PaymentLedgerPostingIntentStatus.Pending, intent.Status);
        Assert.Equal(1234, intent.GrossAmountMinorUnits);
        Assert.Equal(100, intent.FeeAmountMinorUnits);
        Assert.Equal(PaymentLedgerPostingEventKind.IntentRecorded, (await reader.PaymentLedgerPostingEvents.SingleAsync()).Kind);
        var receipts = await reader.PaymentProviderEventReceipts.ToListAsync();
        Assert.Equal(receiptCount, receipts.Count);
        Assert.All(receipts, receipt => Assert.Equal(Now, receipt.ProcessedAt));
    }

    private static EfPaymentProviderEventProcessor Processor(PaymentsDbContext context) => new(context, new Clock(Now));
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
