using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace SettleCore.Modules.Payments.Infrastructure.IntegrationTests;

public sealed class PaymentSuccessPersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessAndIntentAreCommittedTogether(bool invalidIntent)
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options;
        var payment = Payment.Create(100m, "SGD");
        var intent = invalidIntent
            ? PaymentLedgerPostingIntent.Create(payment.Id, Guid.NewGuid(), Guid.NewGuid(),
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "TOOLONG", 10_000, 300)
            : CreateIntent(payment.Id);
        await using (var seed = new PaymentsDbContext(options))
        {
            await seed.Database.MigrateAsync();
            seed.Payments.Add(payment);
            await seed.SaveChangesAsync();
        }
        await using (var writer = new PaymentsDbContext(options))
        {
            var loaded = await writer.Payments.SingleAsync(x => x.Id == payment.Id);
            loaded.MarkSucceeded();
            var persistence = new EfPaymentSuccessPersistence(writer);
            if (invalidIntent)
            {
                await Assert.ThrowsAsync<DbUpdateException>(
                    () => persistence.SaveAsync(loaded, intent));
            }
            else
            {
                await persistence.SaveAsync(loaded, intent);
            }
        }
        await using var reader = new PaymentsDbContext(options);
        var stored = await reader.Payments.SingleAsync(x => x.Id == payment.Id);
        Assert.Equal(invalidIntent ? PaymentStatus.Pending : PaymentStatus.Succeeded,
            stored.Status);
        if (invalidIntent)
        {
            Assert.Empty(await reader.PaymentLedgerPostingIntents.ToListAsync());
            Assert.Empty(await reader.PaymentLedgerPostingEvents.ToListAsync());
            return;
        }
        var storedIntent = await reader.PaymentLedgerPostingIntents
            .SingleAsync(x => x.PaymentId == payment.Id);
        if (!invalidIntent)
        {
            Assert.Equal(intent.TransactionId, storedIntent.TransactionId);
        }
        Assert.Equal(PaymentLedgerPostingIntentStatus.Pending, storedIntent.Status);
        var audit = Assert.Single(await reader.PaymentLedgerPostingEvents.ToListAsync());
        Assert.Equal(payment.Id, audit.PaymentId);
        Assert.Equal(intent.TransactionId, audit.TransactionId);
        Assert.Equal(PaymentLedgerPostingEventKind.IntentRecorded, audit.Kind);
        Assert.NotEqual(default, audit.OccurredAt);
        await new EfPaymentSuccessPersistence(reader).SaveAsync(stored, intent);
        Assert.Single(await reader.PaymentLedgerPostingEvents.ToListAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RejectsInvalidPairWithoutPersisting(bool mismatchedId)
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options;
        var payment = Payment.Create(100m, "SGD");
        await using var writer = new PaymentsDbContext(options);
        await writer.Database.MigrateAsync();
        writer.Payments.Add(payment);
        await writer.SaveChangesAsync();
        var intent = CreateIntent(mismatchedId ? PaymentId.New() : payment.Id);
        if (mismatchedId)
        {
            payment.MarkSucceeded();
        }
        var persistence = new EfPaymentSuccessPersistence(writer);
        await Assert.ThrowsAsync<ArgumentException>(
            () => persistence.SaveAsync(payment, intent));
        await using var reader = new PaymentsDbContext(options);
        Assert.Equal(PaymentStatus.Pending,
            (await reader.Payments.SingleAsync()).Status);
        Assert.Empty(await reader.PaymentLedgerPostingIntents.ToListAsync());
    }

    [Fact]
    public async Task AuditInsertFailureRollsBackPaymentSuccessAndIntent()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options;
        await using var writer = new PaymentsDbContext(options);
        await writer.Database.MigrateAsync();
        var payment = Payment.Create(100m, "SGD");
        writer.Payments.Add(payment);
        await writer.SaveChangesAsync();
        await writer.Database.ExecuteSqlRawAsync(
            "ALTER TABLE payment_ledger_posting_events ADD CONSTRAINT reject_audit CHECK (false)");
        payment.MarkSucceeded();
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            new EfPaymentSuccessPersistence(writer).SaveAsync(payment, CreateIntent(payment.Id)));
        await using var reader = new PaymentsDbContext(options);
        Assert.Equal(PaymentStatus.Pending, (await reader.Payments.SingleAsync()).Status);
        Assert.Empty(await reader.PaymentLedgerPostingIntents.ToListAsync());
        Assert.Empty(await reader.PaymentLedgerPostingEvents.ToListAsync());
    }

    private static PaymentLedgerPostingIntent CreateIntent(PaymentId paymentId)
    {
        return PaymentLedgerPostingIntent.Create(paymentId,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "SGD", 10_000, 300);
    }
}
