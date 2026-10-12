using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure;
using SettleCore.Modules.Payments.Infrastructure.Dispatch;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace SettleCore.IntegrationTests;

public sealed class PaymentLedgerPostingBatchProcessorTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
    public Task InitializeAsync() => postgres.StartAsync();
    public async Task DisposeAsync() => await postgres.DisposeAsync();

    [Fact]
    public async Task FailedPostingDoesNotBlockOtherIntentsAndRetriesOnlyWhenDue()
    {
        var clock = new ManualClock();
        var port = new RecordingPort { FailFirst = true };
        using var provider = await CreateProviderAsync(port, clock);
        var processor = Processor(provider, clock);
        Assert.Equal(1, await processor.ProcessAsync(10, TimeSpan.FromMinutes(1)));
        using (var scope = provider.CreateScope())
        {
            var intents = await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>()
                .PaymentLedgerPostingIntents.OrderBy(x => x.PaymentId).ToListAsync();
            Assert.Equal(PaymentLedgerPostingIntentStatus.Pending, intents[0].Status);
            Assert.Equal(clock.Now.AddMinutes(1), intents[0].NextAttemptAt);
            Assert.Equal(PaymentLedgerPostingIntentStatus.Posted, intents[1].Status);
        }
        Assert.Equal(0, await processor.ProcessAsync(10, TimeSpan.FromMinutes(1)));
        Assert.Equal(2, port.Calls);
        clock.Now = clock.Now.AddMinutes(1);
        port.FailFirst = false;
        Assert.Equal(1, await processor.ProcessAsync(10, TimeSpan.FromMinutes(1)));
        Assert.Equal(3, port.Calls);
    }

    [Fact]
    public async Task ShutdownCancellationPropagatesWithoutSchedulingRetry()
    {
        var clock = new ManualClock();
        using var cancellation = new CancellationTokenSource();
        var port = new RecordingPort { Cancel = cancellation };
        using var provider = await CreateProviderAsync(port, clock);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Processor(provider, clock)
            .ProcessAsync(10, TimeSpan.FromMinutes(1), cancellation.Token));
        Assert.Equal(1, port.Calls);
        using var scope = provider.CreateScope();
        var intents = await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>()
            .PaymentLedgerPostingIntents.ToListAsync();
        Assert.All(intents, intent =>
        {
            Assert.Equal(PaymentLedgerPostingIntentStatus.Pending, intent.Status);
            Assert.Null(intent.NextAttemptAt);
        });
    }

    [Fact]
    public async Task RetrySchedulingFailureDoesNotBlockLaterIntents()
    {
        var clock = new ManualClock();
        var port = new RecordingPort { FailFirst = true };
        using var provider = await CreateProviderAsync(port, clock, failScheduling: true);
        Assert.Equal(1, await Processor(provider, clock).ProcessAsync(10, TimeSpan.FromMinutes(1)));
        Assert.Equal(2, port.Calls);
        using var scope = provider.CreateScope();
        var intents = await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>()
            .PaymentLedgerPostingIntents.OrderBy(x => x.PaymentId).ToListAsync();
        Assert.Equal(PaymentLedgerPostingIntentStatus.Pending, intents[0].Status);
        Assert.Null(intents[0].NextAttemptAt);
        Assert.Equal(PaymentLedgerPostingIntentStatus.Posted, intents[1].Status);
        port.FailFirst = false;
        Assert.Equal(1, await Processor(provider, clock).ProcessAsync(10, TimeSpan.FromMinutes(1)));
        Assert.Equal(3, port.Calls);
    }

    [Fact]
    public async Task CancellationDuringRetrySchedulingStopsBatch()
    {
        var clock = new ManualClock();
        var port = new RecordingPort { FailFirst = true };
        using var cancellation = new CancellationTokenSource();
        using var provider = await CreateProviderAsync(port, clock, failScheduling: true,
            scheduleCancellation: cancellation);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Processor(provider, clock)
            .ProcessAsync(10, TimeSpan.FromMinutes(1), cancellation.Token));
        Assert.Equal(1, port.Calls);
        using var scope = provider.CreateScope();
        var intents = await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>()
            .PaymentLedgerPostingIntents.ToListAsync();
        Assert.All(intents, intent =>
        {
            Assert.Equal(PaymentLedgerPostingIntentStatus.Pending, intent.Status);
            Assert.Null(intent.NextAttemptAt);
        });
    }

    [Fact]
    public async Task MetricsDistinguishPostingFailureSchedulingFailureAndRecovery()
    {
        var clock = new ManualClock();
        var port = new RecordingPort { FailFirst = true };
        using var provider = await CreateProviderAsync(port, clock, failScheduling: true);
        var meter = provider.GetRequiredService<IMeterFactory>().Create(PaymentLedgerPostingMetrics.MeterName);
        var counts = new Dictionary<string, long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (ReferenceEquals(instrument.Meter, meter)) current.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, _) =>
        {
            Assert.Empty(tags.ToArray());
            counts[instrument.Name] = counts.GetValueOrDefault(instrument.Name) + measurement;
        });
        listener.Start();
        Assert.Equal(1, await Processor(provider, clock).ProcessAsync(10, TimeSpan.FromMinutes(1)));
        port.FailFirst = false;
        Assert.Equal(1, await Processor(provider, clock).ProcessAsync(10, TimeSpan.FromMinutes(1)));
        Assert.Equal(3, counts["settlecore.payment_posting.attempts"]);
        Assert.Equal(2, counts["settlecore.payment_posting.completed"]);
        Assert.Equal(1, counts["settlecore.payment_posting.failures"]);
        Assert.Equal(1, counts["settlecore.payment_posting.retry_scheduling_failures"]);
    }

    [Fact]
    public async Task ShutdownDoesNotCountAsPostingFailure()
    {
        var clock = new ManualClock();
        using var cancellation = new CancellationTokenSource();
        var port = new RecordingPort { Cancel = cancellation };
        using var provider = await CreateProviderAsync(port, clock);
        var meter = provider.GetRequiredService<IMeterFactory>().Create(PaymentLedgerPostingMetrics.MeterName);
        var counts = new Dictionary<string, long>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (ReferenceEquals(instrument.Meter, meter)) current.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
            counts[instrument.Name] = counts.GetValueOrDefault(instrument.Name) + measurement);
        listener.Start();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Processor(provider, clock)
            .ProcessAsync(10, TimeSpan.FromMinutes(1), cancellation.Token));
        Assert.Equal(1, counts["settlecore.payment_posting.attempts"]);
        Assert.Equal(0, counts.GetValueOrDefault("settlecore.payment_posting.completed"));
        Assert.Equal(0, counts.GetValueOrDefault("settlecore.payment_posting.failures"));
        Assert.Equal(0, counts.GetValueOrDefault("settlecore.payment_posting.retry_scheduling_failures"));
    }

    private async Task<ServiceProvider> CreateProviderAsync(RecordingPort port, ManualClock clock,
        bool failScheduling = false, CancellationTokenSource? scheduleCancellation = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Payments"] = postgres.GetConnectionString()
        }).Build();
        var services = new ServiceCollection();
        services.AddPaymentsModule(configuration);
        services.AddSingleton<IPaymentLedgerPostingPort>(port);
        services.AddSingleton<TimeProvider>(clock);
        if (failScheduling)
        {
            services.AddScoped<IPaymentLedgerPostingIntentRepository>(scope => new SchedulingFailureRepository(
                new EfPaymentLedgerPostingIntentRepository(scope.GetRequiredService<PaymentsDbContext>(), clock),
                scheduleCancellation));
        }
        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        await db.Database.MigrateAsync();
        foreach (var number in new[] { 1, 2 })
        {
            var payment = Payment.Rehydrate(PaymentId.From(Guid.Parse(
                $"00000000-0000-0000-0000-{number:D12}")), 100m, "SGD", PaymentStatus.Succeeded);
            db.Payments.Add(payment);
            db.PaymentLedgerPostingIntents.Add(PaymentLedgerPostingIntent.Create(payment.Id,
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "SGD", 10_000, 300));
        }
        await db.SaveChangesAsync();
        return provider;
    }

    private static PaymentLedgerPostingBatchProcessor Processor(ServiceProvider provider, ManualClock clock)
        => new(provider.GetRequiredService<IServiceScopeFactory>(), clock,
            NullLogger<PaymentLedgerPostingBatchProcessor>.Instance,
            provider.GetRequiredService<PaymentLedgerPostingMetrics>());

    private sealed class SchedulingFailureRepository(IPaymentLedgerPostingIntentRepository inner,
        CancellationTokenSource? cancellation) : IPaymentLedgerPostingIntentRepository
    {
        public Task AddAsync(PaymentLedgerPostingIntent intent, CancellationToken cancellationToken = default)
            => inner.AddAsync(intent, cancellationToken);
        public Task<PaymentLedgerPostingIntent?> GetByPaymentIdAsync(PaymentId paymentId,
            CancellationToken cancellationToken = default) => inner.GetByPaymentIdAsync(paymentId, cancellationToken);
        public Task<bool> MarkPostedAsync(PaymentId paymentId, CancellationToken cancellationToken = default)
            => inner.MarkPostedAsync(paymentId, cancellationToken);
        public Task<IReadOnlyList<PaymentLedgerPostingIntent>> GetPendingAsync(int limit,
            CancellationToken cancellationToken = default) => inner.GetPendingAsync(limit, cancellationToken);
        public Task<bool> ScheduleRetryAsync(PaymentId paymentId, DateTimeOffset nextAttemptAt,
            CancellationToken cancellationToken = default)
        {
            cancellation?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Retry schedule storage unavailable.");
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class RecordingPort : IPaymentLedgerPostingPort
    {
        public int Calls { get; private set; }
        public bool FailFirst { get; set; }
        public CancellationTokenSource? Cancel { get; set; }
        public Task PostAsync(PaymentLedgerPostingRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            Cancel?.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            if (FailFirst && request.PaymentId == Guid.Parse("00000000-0000-0000-0000-000000000001"))
            {
                throw new InvalidOperationException("Posting temporarily unavailable.");
            }
            return Task.CompletedTask;
        }
    }
}
