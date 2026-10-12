using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace SettleCore.Modules.Payments.Infrastructure.IntegrationTests;

public sealed class PaymentProviderEventInboxTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
    public Task InitializeAsync() => _postgres.StartAsync();
    public async Task DisposeAsync() => await _postgres.DisposeAsync();
    private DbContextOptions<PaymentsDbContext> Options => new DbContextOptionsBuilder<PaymentsDbContext>()
        .UseNpgsql(_postgres.GetConnectionString()).Options;
    private static readonly DateTimeOffset ReceivedAt = new(2026, 10, 9, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ReceivesBeforePaymentExistsAndMatchingReplayPreservesFirstReceipt()
    {
        await using var writer = new PaymentsDbContext(Options);
        await writer.Database.MigrateAsync();
        var evidence = Evidence();
        var inbox = new EfPaymentProviderEventInbox(writer, new FixedClock(ReceivedAt));
        Assert.True(await inbox.ReceiveAsync(evidence));
        await using var retry = new PaymentsDbContext(Options);
        Assert.False(await new EfPaymentProviderEventInbox(retry, new FixedClock(ReceivedAt.AddHours(1))).ReceiveAsync(evidence));
        await using var reader = new PaymentsDbContext(Options);
        var stored = await reader.PaymentProviderEventReceipts.SingleAsync();
        Assert.Equal(evidence, stored.ToEvent());
        Assert.Equal(ReceivedAt, stored.ReceivedAt);
        Assert.Null(stored.ProcessedAt);
        Assert.Empty(await reader.Payments.ToListAsync());
    }

    [Fact]
    public async Task ConflictingReplayPreservesWinner()
    {
        await using var writer = new PaymentsDbContext(Options);
        await writer.Database.MigrateAsync();
        var evidence = Evidence();
        var inbox = new EfPaymentProviderEventInbox(writer, new FixedClock(ReceivedAt));
        Assert.True(await inbox.ReceiveAsync(evidence));
        var conflicts = new[]
        {
            evidence with { AmountMinorUnits = 9999 },
            evidence with { PaymentId = PaymentId.New() },
            evidence with { Currency = "JPY" },
            evidence with { IsLiveMode = true },
            evidence with { OccurredAt = evidence.OccurredAt.AddSeconds(1) },
            evidence with { ProviderReference = ProviderPaymentReference.Create("stripe", "pi_other") }
        };
        foreach (var conflict in conflicts)
        {
            await Assert.ThrowsAsync<ProviderPaymentEventConflictException>(() => inbox.ReceiveAsync(conflict));
        }
        Assert.False(await inbox.ReceiveAsync(evidence));
        await using var reader = new PaymentsDbContext(Options);
        Assert.Equal(evidence, (await reader.PaymentProviderEventReceipts.SingleAsync()).ToEvent());
    }

    [Fact]
    public async Task ConcurrentMatchingDeliveriesStoreExactlyOneReceipt()
    {
        await using var seed = new PaymentsDbContext(Options);
        await seed.Database.MigrateAsync();
        var evidence = Evidence();
        await using var first = new PaymentsDbContext(Options);
        await using var second = new PaymentsDbContext(Options);
        var outcomes = await Task.WhenAll(
            new EfPaymentProviderEventInbox(first, new FixedClock(ReceivedAt)).ReceiveAsync(evidence),
            new EfPaymentProviderEventInbox(second, new FixedClock(ReceivedAt)).ReceiveAsync(evidence));
        Assert.Single(outcomes, inserted => inserted);
        await using var reader = new PaymentsDbContext(Options);
        Assert.Equal(evidence, (await reader.PaymentProviderEventReceipts.SingleAsync()).ToEvent());
    }

    [Fact]
    public async Task SameEventIdentityIsScopedByProvider()
    {
        await using var writer = new PaymentsDbContext(Options);
        await writer.Database.MigrateAsync();
        var evidence = Evidence();
        var inbox = new EfPaymentProviderEventInbox(writer, new FixedClock(ReceivedAt));
        Assert.True(await inbox.ReceiveAsync(evidence));
        Assert.True(await inbox.ReceiveAsync(evidence with { ProviderReference = ProviderPaymentReference.Create("other", "ref_test") }));
        Assert.Equal(2, await writer.PaymentProviderEventReceipts.CountAsync());
    }

    [Fact]
    public async Task PendingSelectionIsBoundedProviderScopedAndHonorsExplicitRetryTime()
    {
        await using var context = new PaymentsDbContext(Options);
        await context.Database.MigrateAsync();
        var first = Evidence() with { EventId = "evt_a" };
        var second = Evidence() with { EventId = "evt_b" };
        var other = Evidence() with { ProviderReference = ProviderPaymentReference.Create("other", "ref_test") };
        var inbox = new EfPaymentProviderEventInbox(context, new FixedClock(ReceivedAt));
        await inbox.ReceiveAsync(first);
        await inbox.ReceiveAsync(second);
        await inbox.ReceiveAsync(other);
        Assert.Equal(first, Assert.Single(await inbox.GetPendingAsync("stripe", 1)));
        Assert.True(await inbox.ScheduleRetryAsync("stripe", first.EventId, ReceivedAt.AddMinutes(1)));
        Assert.Equal(second, Assert.Single(await inbox.GetPendingAsync("stripe", 1)));
        var future = new EfPaymentProviderEventInbox(context, new FixedClock(ReceivedAt.AddMinutes(1)));
        Assert.Equal(new[] { first, second }, await future.GetPendingAsync("stripe", 2));
        Assert.Equal(other, Assert.Single(await inbox.GetPendingAsync("other", 10)));
        Assert.Equal(ReceivedAt.AddMinutes(1), (await context.PaymentProviderEventReceipts.AsNoTracking()
            .SingleAsync(x => x.EventId == first.EventId)).NextAttemptAt);
    }

    [Fact]
    public async Task RetryCannotReviveProcessedReceiptAndMissingReceiptIsNoOp()
    {
        await using var context = new PaymentsDbContext(Options);
        await context.Database.MigrateAsync();
        var evidence = Evidence();
        var inbox = new EfPaymentProviderEventInbox(context, new FixedClock(ReceivedAt));
        Assert.False(await inbox.ScheduleRetryAsync("stripe", "evt_missing", ReceivedAt.AddMinutes(1)));
        await inbox.ReceiveAsync(evidence);
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE payment_provider_event_receipts SET processed_at = {ReceivedAt} WHERE event_id = {evidence.EventId}");
        Assert.False(await inbox.ScheduleRetryAsync("stripe", evidence.EventId, ReceivedAt.AddMinutes(1)));
        Assert.Empty(await inbox.GetPendingAsync("stripe", 10));
        Assert.Null((await context.PaymentProviderEventReceipts.SingleAsync()).NextAttemptAt);
    }

    private static ProviderPaymentSucceededEvent Evidence() => new("evt_test", PaymentId.New(),
        ProviderPaymentReference.Create("stripe", "pi_test"), 1234, "SGD", ReceivedAt.AddMinutes(-1), false);
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
