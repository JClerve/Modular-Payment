using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace SettleCore.Modules.Payments.Infrastructure.IntegrationTests;

public sealed class PaymentSuccessRetryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task RetryRequiresIdenticalPostingPayload(int changedField)
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options;
        var payment = Payment.Create(100m, "SGD");
        var original = PaymentLedgerPostingIntent.Create(payment.Id,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "SGD", 10_000, 300);
        await using (var seed = new PaymentsDbContext(options))
        {
            await seed.Database.MigrateAsync();
            seed.Payments.Add(payment);
            await seed.SaveChangesAsync();
            payment.MarkSucceeded();
            var persistence = new EfPaymentSuccessPersistence(seed);
            await persistence.SaveAsync(payment, original);
            if (changedField == 0)
            {
                await persistence.SaveAsync(payment, Copy(original, 0));
            }
        }
        await using var writer = new PaymentsDbContext(options);
        var loaded = await writer.Payments.SingleAsync(x => x.Id == payment.Id);
        var retry = Copy(original, changedField);
        var repository = new EfPaymentSuccessPersistence(writer);
        if (changedField == 0)
        {
            await repository.SaveAsync(loaded, retry);
        }
        else
        {
            await Assert.ThrowsAsync<PaymentLedgerPostingIntentConflictException>(
                () => repository.SaveAsync(loaded, retry));
        }
        await using var reader = new PaymentsDbContext(options);
        Assert.Equal(PaymentStatus.Succeeded, (await reader.Payments.SingleAsync()).Status);
        var stored = Assert.Single(await reader.PaymentLedgerPostingIntents.ToListAsync());
        Assert.Equal(original.TransactionId, stored.TransactionId);
        Assert.Equal(original.LedgerId, stored.LedgerId);
        Assert.Equal(original.ProcessorReceivableAccountId, stored.ProcessorReceivableAccountId);
        Assert.Equal(original.MerchantPayableAccountId, stored.MerchantPayableAccountId);
        Assert.Equal(original.PlatformRevenueAccountId, stored.PlatformRevenueAccountId);
        Assert.Equal(original.Currency, stored.Currency);
        Assert.Equal(original.GrossAmountMinorUnits, stored.GrossAmountMinorUnits);
        Assert.Equal(original.FeeAmountMinorUnits, stored.FeeAmountMinorUnits);
        var audit = Assert.Single(await reader.PaymentLedgerPostingEvents.ToListAsync());
        Assert.Equal(stored.TransactionId, audit.TransactionId);
        Assert.Equal(PaymentLedgerPostingEventKind.IntentRecorded, audit.Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentWritersPreserveOneIntentAndUsableContexts(bool conflict)
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options;
        var payment = Payment.Create(100m, "SGD");
        var original = PaymentLedgerPostingIntent.Create(payment.Id,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "SGD", 10_000, 300);
        await using (var seed = new PaymentsDbContext(options))
        {
            await seed.Database.MigrateAsync();
            seed.Payments.Add(payment);
            await seed.SaveChangesAsync();
        }
        var writerOptions = new DbContextOptionsBuilder<PaymentsDbContext>(options)
            .AddInterceptors(new ConcurrentSaveBarrier()).Options;
        await using var first = new PaymentsDbContext(writerOptions);
        await using var second = new PaymentsDbContext(writerOptions);
        var firstPayment = await first.Payments.SingleAsync();
        var secondPayment = await second.Payments.SingleAsync();
        firstPayment.MarkSucceeded();
        secondPayment.MarkSucceeded();
        var alternative = Copy(original, conflict ? 1 : 0);
        var results = await Task.WhenAll(
            Record.ExceptionAsync(() => new EfPaymentSuccessPersistence(first)
                .SaveAsync(firstPayment, original)),
            Record.ExceptionAsync(() => new EfPaymentSuccessPersistence(second)
                .SaveAsync(secondPayment, alternative)));
        if (conflict)
        {
            Assert.Single(results, exception => exception is null);
            Assert.IsType<PaymentLedgerPostingIntentConflictException>(
                Assert.Single(results, exception => exception is not null));
        }
        else
        {
            Assert.All(results, Assert.Null);
        }
        await first.SaveChangesAsync();
        await second.SaveChangesAsync();
        await using var reader = new PaymentsDbContext(options);
        Assert.Equal(PaymentStatus.Succeeded, (await reader.Payments.SingleAsync()).Status);
        var stored = Assert.Single(await reader.PaymentLedgerPostingIntents.ToListAsync());
        Assert.Contains(stored.TransactionId, new[] { original.TransactionId, alternative.TransactionId });
        Assert.Equal(original.FeeAmountMinorUnits, stored.FeeAmountMinorUnits);
        var audit = Assert.Single(await reader.PaymentLedgerPostingEvents.ToListAsync());
        Assert.Equal(stored.TransactionId, audit.TransactionId);
        Assert.Equal(PaymentLedgerPostingEventKind.IntentRecorded, audit.Kind);
    }

    [Fact]
    public async Task ExistingIntentCannotAcknowledgeUncommittedSuccess()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options;
        var payment = Payment.Create(100m, "SGD");
        var intent = PaymentLedgerPostingIntent.Create(payment.Id,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "SGD", 10_000, 300);
        await using var writer = new PaymentsDbContext(options);
        await writer.Database.MigrateAsync();
        writer.Payments.Add(payment);
        writer.PaymentLedgerPostingIntents.Add(intent);
        await writer.SaveChangesAsync();
        payment.MarkSucceeded();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EfPaymentSuccessPersistence(writer).SaveAsync(payment, Copy(intent, 0)));
        await writer.SaveChangesAsync();
        await using var reader = new PaymentsDbContext(options);
        Assert.Equal(PaymentStatus.Pending, (await reader.Payments.SingleAsync()).Status);
    }

    private sealed class ConcurrentSaveBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref arrivals) == 2)
            {
                ready.TrySetResult();
            }
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return result;
        }
    }

    private static PaymentLedgerPostingIntent Copy(PaymentLedgerPostingIntent original, int changedField)
    {
        return PaymentLedgerPostingIntent.Create(original.PaymentId,
            changedField == 1 ? Guid.NewGuid() : original.TransactionId,
            changedField == 2 ? Guid.NewGuid() : original.LedgerId,
            changedField == 3 ? Guid.NewGuid() : original.ProcessorReceivableAccountId,
            changedField == 4 ? Guid.NewGuid() : original.MerchantPayableAccountId,
            changedField == 5 ? Guid.NewGuid() : original.PlatformRevenueAccountId,
            changedField == 6 ? "USD" : original.Currency,
            changedField == 7 ? 20_000 : original.GrossAmountMinorUnits,
            changedField == 8 ? 400 : original.FeeAmountMinorUnits);
    }
}
