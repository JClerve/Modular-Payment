using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;

namespace SettleCore.Modules.Payments.Infrastructure.IntegrationTests;

public sealed class EfPaymentLedgerPostingIntentRepositoryTests
    : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:18-alpine")
            .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task CanPersistAndReloadPendingLedgerPostingIntent()
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var dbContext = new PaymentsDbContext(options);

        await dbContext.Database.MigrateAsync();

        var paymentId = PaymentId.New();
        var transactionId = Guid.NewGuid();
        var ledgerId = Guid.NewGuid();
        var processorReceivableAccountId = Guid.NewGuid();
        var merchantPayableAccountId = Guid.NewGuid();
        var platformRevenueAccountId = Guid.NewGuid();

        var intent = PaymentLedgerPostingIntent.Create(
            paymentId,
            transactionId,
            ledgerId,
            processorReceivableAccountId,
            merchantPayableAccountId,
            platformRevenueAccountId,
            "SGD",
            grossAmountMinorUnits: 10_000,
            feeAmountMinorUnits: 300);

        var repository =
            new EfPaymentLedgerPostingIntentRepository(dbContext);
        await repository.AddAsync(intent);

        dbContext.ChangeTracker.Clear();

        await using var readContext = new PaymentsDbContext(options);
        var reader =
            new EfPaymentLedgerPostingIntentRepository(readContext);
        var reloaded = await reader.GetByPaymentIdAsync(paymentId);

        Assert.NotNull(reloaded);
        Assert.Null(await reader.GetByPaymentIdAsync(PaymentId.New()));

        Assert.Equal(paymentId, reloaded.PaymentId);
        Assert.Equal(transactionId, reloaded.TransactionId);
        Assert.Equal(ledgerId, reloaded.LedgerId);
        Assert.Equal(
            processorReceivableAccountId,
            reloaded.ProcessorReceivableAccountId);
        Assert.Equal(
            merchantPayableAccountId,
            reloaded.MerchantPayableAccountId);
        Assert.Equal(
            platformRevenueAccountId,
            reloaded.PlatformRevenueAccountId);
        Assert.Equal("SGD", reloaded.Currency);
        Assert.Equal(10_000, reloaded.GrossAmountMinorUnits);
        Assert.Equal(300, reloaded.FeeAmountMinorUnits);
        Assert.Equal(
            PaymentLedgerPostingIntentStatus.Pending,
            reloaded.Status);
    }
    [Fact]
    public async Task CompletionIsDurableIdempotentAndChangesOnlyStatus()
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var writer = new PaymentsDbContext(options);
        await writer.Database.MigrateAsync();
        var intent = PaymentLedgerPostingIntent.Create(PaymentId.New(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SGD", 10_000, 300);
        var repository = new EfPaymentLedgerPostingIntentRepository(writer);
        await repository.AddAsync(intent);
        Assert.True(await repository.MarkPostedAsync(intent.PaymentId));
        Assert.True(await repository.MarkPostedAsync(intent.PaymentId));
        Assert.False(await repository.MarkPostedAsync(PaymentId.New()));
        Assert.Equal(PaymentLedgerPostingIntentStatus.Posted,
            (await repository.GetByPaymentIdAsync(intent.PaymentId))!.Status);
        await writer.SaveChangesAsync();
        await using var reader = new PaymentsDbContext(options);
        var stored = await reader.PaymentLedgerPostingIntents.SingleAsync();
        Assert.Equal(PaymentLedgerPostingIntentStatus.Posted, stored.Status);
        Assert.Equal(intent.PaymentId, stored.PaymentId);
        Assert.Equal(intent.TransactionId, stored.TransactionId);
        Assert.Equal(intent.LedgerId, stored.LedgerId);
        Assert.Equal(intent.ProcessorReceivableAccountId, stored.ProcessorReceivableAccountId);
        Assert.Equal(intent.MerchantPayableAccountId, stored.MerchantPayableAccountId);
        Assert.Equal(intent.PlatformRevenueAccountId, stored.PlatformRevenueAccountId);
        Assert.Equal(intent.Currency, stored.Currency);
        Assert.Equal(intent.GrossAmountMinorUnits, stored.GrossAmountMinorUnits);
        Assert.Equal(intent.FeeAmountMinorUnits, stored.FeeAmountMinorUnits);
    }
    [Fact]
    public async Task PendingBatchIsBoundedAndRequiresCommittedPaymentSuccess()
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var context = new PaymentsDbContext(options);
        await context.Database.MigrateAsync();
        var payments = Enumerable.Range(1, 4).Select(number => Payment.Rehydrate(
            PaymentId.From(Guid.Parse($"00000000-0000-0000-0000-{number:D12}")),
            100m, "SGD", number == 4 ? PaymentStatus.Pending : PaymentStatus.Succeeded)).ToArray();
        context.Payments.AddRange(payments);
        var intents = payments.Select(payment => PaymentLedgerPostingIntent.Create(payment.Id,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SGD", 10_000, 300)).ToArray();
        intents[2].MarkPosted();
        context.PaymentLedgerPostingIntents.AddRange(intents);
        context.PaymentLedgerPostingIntents.Add(PaymentLedgerPostingIntent.Create(PaymentId.New(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "SGD", 10_000, 300));
        await context.SaveChangesAsync();
        var repository = new EfPaymentLedgerPostingIntentRepository(context);
        var batch = await repository.GetPendingAsync(1);
        Assert.Equal(payments[0].Id, Assert.Single(batch).PaymentId);
        var all = await repository.GetPendingAsync(10);
        Assert.Equal(new[] { payments[0].Id, payments[1].Id }, all.Select(x => x.PaymentId));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.GetPendingAsync(0));
    }
    [Fact]
    public async Task ScheduledRetryIsDurableExcludedUntilDueAndCannotRevivePostedIntent()
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var writer = new PaymentsDbContext(options);
        await writer.Database.MigrateAsync();
        var payment = Payment.Create(100m, "SGD");
        payment.MarkSucceeded();
        var intent = PaymentLedgerPostingIntent.Create(payment.Id, Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SGD", 10_000, 300);
        writer.Payments.Add(payment);
        writer.PaymentLedgerPostingIntents.Add(intent);
        await writer.SaveChangesAsync();
        var clock = new ManualClock(new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero));
        var repository = new EfPaymentLedgerPostingIntentRepository(writer, clock);
        var nextAttempt = clock.Now.AddMinutes(1);
        Assert.True(await repository.ScheduleRetryAsync(payment.Id, nextAttempt));
        Assert.Empty(await repository.GetPendingAsync(10));
        await using var reader = new PaymentsDbContext(options);
        var freshRepository = new EfPaymentLedgerPostingIntentRepository(reader, clock);
        Assert.Equal(nextAttempt, (await freshRepository.GetByPaymentIdAsync(payment.Id))!.NextAttemptAt);
        clock.Now = nextAttempt;
        Assert.Single(await freshRepository.GetPendingAsync(10));
        Assert.True(await repository.MarkPostedAsync(payment.Id));
        Assert.False(await repository.ScheduleRetryAsync(payment.Id, nextAttempt.AddMinutes(1)));
        Assert.False(await repository.ScheduleRetryAsync(PaymentId.New(), nextAttempt));
        Assert.Empty(await freshRepository.GetPendingAsync(10));
        Assert.Equal(PaymentLedgerPostingIntentStatus.Posted,
            (await freshRepository.GetByPaymentIdAsync(payment.Id))!.Status);
    }

    [Fact]
    public async Task FirstPostingAcknowledgmentTimeIsDurableAndPreservedOnReplay()
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var writer = new PaymentsDbContext(options);
        await writer.Database.MigrateAsync();
        var intent = PaymentLedgerPostingIntent.Create(PaymentId.New(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SGD", 10_000, 300);
        var clock = new ManualClock(new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero));
        var repository = new EfPaymentLedgerPostingIntentRepository(writer, clock);
        await repository.AddAsync(intent);
        Assert.Null((await repository.GetByPaymentIdAsync(intent.PaymentId))!.PostedAt);
        Assert.True(await repository.MarkPostedAsync(intent.PaymentId));
        var firstAcknowledgment = clock.Now;
        clock.Now = clock.Now.AddHours(1);
        Assert.True(await repository.MarkPostedAsync(intent.PaymentId));
        await using var reader = new PaymentsDbContext(options);
        var stored = await reader.PaymentLedgerPostingIntents.SingleAsync();
        Assert.Equal(firstAcknowledgment, stored.PostedAt);
        var audit = Assert.Single(await reader.PaymentLedgerPostingEvents.ToListAsync());
        Assert.Equal(PaymentLedgerPostingEventKind.PostingAcknowledged, audit.Kind);
        Assert.Equal(firstAcknowledgment, audit.OccurredAt);
        Assert.Equal(intent.TransactionId, audit.TransactionId);
        Assert.Equal(intent.PaymentId, audit.PaymentId);
        Assert.Null(audit.NextAttemptAt);
        Assert.Equal(PaymentLedgerPostingIntentStatus.Posted, stored.Status);
        Assert.Equal(intent.TransactionId, stored.TransactionId);
    }

    [Fact]
    public async Task ConcurrentAcknowledgmentsRetainOneWinningTimestamp()
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var seed = new PaymentsDbContext(options);
        await seed.Database.MigrateAsync();
        var intent = PaymentLedgerPostingIntent.Create(PaymentId.New(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SGD", 10_000, 300);
        await new EfPaymentLedgerPostingIntentRepository(seed).AddAsync(intent);
        var firstTime = new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);
        var secondTime = firstTime.AddMinutes(1);
        await using var first = new PaymentsDbContext(options);
        await using var second = new PaymentsDbContext(options);
        var results = await Task.WhenAll(
            new EfPaymentLedgerPostingIntentRepository(first, new ManualClock(firstTime))
                .MarkPostedAsync(intent.PaymentId),
            new EfPaymentLedgerPostingIntentRepository(second, new ManualClock(secondTime))
                .MarkPostedAsync(intent.PaymentId));
        Assert.All(results, Assert.True);
        await using var reader = new PaymentsDbContext(options);
        var stored = await reader.PaymentLedgerPostingIntents.SingleAsync();
        Assert.Contains(stored.PostedAt, new DateTimeOffset?[] { firstTime, secondTime });
        var audit = Assert.Single(await reader.PaymentLedgerPostingEvents.ToListAsync());
        Assert.Equal(stored.PostedAt, audit.OccurredAt);
        Assert.Equal(PaymentLedgerPostingEventKind.PostingAcknowledged, audit.Kind);
        Assert.True(await new EfPaymentLedgerPostingIntentRepository(reader,
            new ManualClock(secondTime.AddHours(1))).MarkPostedAsync(intent.PaymentId));
        Assert.Equal(stored.PostedAt,
            (await new EfPaymentLedgerPostingIntentRepository(reader)
                .GetByPaymentIdAsync(intent.PaymentId))!.PostedAt);
    }

    [Fact]
    public async Task AcknowledgmentAuditFailureLeavesIntentPending()
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var writer = new PaymentsDbContext(options);
        await writer.Database.MigrateAsync();
        var intent = PaymentLedgerPostingIntent.Create(PaymentId.New(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SGD", 10_000, 300);
        var repository = new EfPaymentLedgerPostingIntentRepository(writer);
        await repository.AddAsync(intent);
        await writer.Database.ExecuteSqlRawAsync(
            "ALTER TABLE payment_ledger_posting_events ADD CONSTRAINT reject_audit CHECK (false)");
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => repository.MarkPostedAsync(intent.PaymentId));
        await using var reader = new PaymentsDbContext(options);
        var stored = await reader.PaymentLedgerPostingIntents.SingleAsync();
        Assert.Equal(PaymentLedgerPostingIntentStatus.Pending, stored.Status);
        Assert.Null(stored.PostedAt);
        Assert.Empty(await reader.PaymentLedgerPostingEvents.ToListAsync());
        await writer.Database.ExecuteSqlRawAsync(
            "ALTER TABLE payment_ledger_posting_events DROP CONSTRAINT reject_audit");
        Assert.True(await repository.MarkPostedAsync(intent.PaymentId));
        Assert.Single(await reader.PaymentLedgerPostingEvents.ToListAsync());
    }

    [Fact]
    public async Task RetryEventsRetainEverySuccessfulScheduleAndExcludeMissingOrPostedIntents()
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var writer = new PaymentsDbContext(options);
        await writer.Database.MigrateAsync();
        var intent = PaymentLedgerPostingIntent.Create(PaymentId.New(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SGD", 10_000, 300);
        var clock = new ManualClock(new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero));
        var repository = new EfPaymentLedgerPostingIntentRepository(writer, clock);
        await repository.AddAsync(intent);
        var firstDue = clock.Now.AddMinutes(1);
        Assert.True(await repository.ScheduleRetryAsync(intent.PaymentId, firstDue.ToOffset(TimeSpan.FromHours(5))));
        var firstOccurrence = clock.Now;
        clock.Now = clock.Now.AddSeconds(1);
        var secondDue = clock.Now.AddMinutes(2);
        Assert.True(await repository.ScheduleRetryAsync(intent.PaymentId, secondDue));
        Assert.False(await repository.ScheduleRetryAsync(PaymentId.New(), secondDue));
        Assert.True(await repository.MarkPostedAsync(intent.PaymentId));
        Assert.False(await repository.ScheduleRetryAsync(intent.PaymentId, secondDue));
        await using var reader = new PaymentsDbContext(options);
        var events = await reader.PaymentLedgerPostingEvents
            .Where(x => x.Kind == PaymentLedgerPostingEventKind.RetryScheduled)
            .OrderBy(x => x.OccurredAt).ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.Equal(firstDue, events[0].NextAttemptAt);
        Assert.Equal(firstOccurrence, events[0].OccurredAt);
        Assert.Equal(secondDue, events[1].NextAttemptAt);
        Assert.Equal(clock.Now, events[1].OccurredAt);
        Assert.All(events, audit => {
            Assert.Equal(intent.PaymentId, audit.PaymentId);
            Assert.Equal(intent.TransactionId, audit.TransactionId);
        });
    }

    [Fact]
    public async Task RetryAuditFailurePreservesPreviousSchedule()
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var writer = new PaymentsDbContext(options);
        await writer.Database.MigrateAsync();
        var intent = PaymentLedgerPostingIntent.Create(PaymentId.New(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SGD", 10_000, 300);
        var repository = new EfPaymentLedgerPostingIntentRepository(writer);
        await repository.AddAsync(intent);
        var firstDue = new DateTimeOffset(2026, 10, 9, 0, 1, 0, TimeSpan.Zero);
        Assert.True(await repository.ScheduleRetryAsync(intent.PaymentId, firstDue));
        await writer.Database.ExecuteSqlRawAsync(
            "ALTER TABLE payment_ledger_posting_events ADD CONSTRAINT reject_new_audit CHECK (false) NOT VALID");
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            repository.ScheduleRetryAsync(intent.PaymentId, firstDue.AddMinutes(1)));
        await using var reader = new PaymentsDbContext(options);
        Assert.Equal(firstDue, (await reader.PaymentLedgerPostingIntents.SingleAsync()).NextAttemptAt);
        Assert.Single(await reader.PaymentLedgerPostingEvents.ToListAsync());
        await writer.Database.ExecuteSqlRawAsync(
            "ALTER TABLE payment_ledger_posting_events DROP CONSTRAINT reject_new_audit");
        Assert.True(await repository.ScheduleRetryAsync(intent.PaymentId, firstDue.AddMinutes(1)));
        Assert.Equal(2, await reader.PaymentLedgerPostingEvents.CountAsync());
    }

    [Fact]
    public async Task ConcurrentRetryAndAcknowledgmentNeverRevivePostedIntent()
    {
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var seed = new PaymentsDbContext(options);
        await seed.Database.MigrateAsync();
        var intent = PaymentLedgerPostingIntent.Create(PaymentId.New(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SGD", 10_000, 300);
        await new EfPaymentLedgerPostingIntentRepository(seed).AddAsync(intent);
        await using var retryContext = new PaymentsDbContext(options);
        await using var postingContext = new PaymentsDbContext(options);
        var nextAttempt = new DateTimeOffset(2026, 10, 9, 0, 1, 0, TimeSpan.Zero);
        var retryTask = new EfPaymentLedgerPostingIntentRepository(retryContext)
            .ScheduleRetryAsync(intent.PaymentId, nextAttempt);
        var postingTask = new EfPaymentLedgerPostingIntentRepository(postingContext)
            .MarkPostedAsync(intent.PaymentId);
        await Task.WhenAll(retryTask, postingTask);
        Assert.True(await postingTask);
        await using var reader = new PaymentsDbContext(options);
        var stored = await reader.PaymentLedgerPostingIntents.SingleAsync();
        Assert.Equal(PaymentLedgerPostingIntentStatus.Posted, stored.Status);
        var events = await reader.PaymentLedgerPostingEvents.ToListAsync();
        Assert.Single(events, audit => audit.Kind == PaymentLedgerPostingEventKind.PostingAcknowledged);
        Assert.Equal(await retryTask ? 1 : 0,
            events.Count(audit => audit.Kind == PaymentLedgerPostingEventKind.RetryScheduled));
        Assert.Equal(await retryTask ? nextAttempt : (DateTimeOffset?)null, stored.NextAttemptAt);
        Assert.False(await new EfPaymentLedgerPostingIntentRepository(reader)
            .ScheduleRetryAsync(intent.PaymentId, nextAttempt.AddHours(1)));
        Assert.Equal(events.Count, await reader.PaymentLedgerPostingEvents.CountAsync());
    }

    private sealed class ManualClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
