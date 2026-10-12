using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SettleCore.Modules.Ledger.Domain;
using SettleCore.Modules.Ledger.Infrastructure;
using SettleCore.Modules.Ledger.Infrastructure.Persistence;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.DispatchPaymentLedgerPosting;
using SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace SettleCore.IntegrationTests;

public sealed class PaymentLedgerDispatchPostgreSqlTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DispatchAcrossStoresSurvivesLostAcknowledgment(bool loseAcknowledgment)
    {
        await using var payments = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await using var ledger = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await payments.StartAsync();
        await ledger.StartAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Payments"] = payments.GetConnectionString(),
                ["ConnectionStrings:Ledger"] = ledger.GetConnectionString()
            }).Build();
        var services = new ServiceCollection();
        services.AddPaymentsModule(configuration);
        services.AddLedgerModule(configuration);
        var failure = new FailureGate { Fail = loseAcknowledgment };
        services.AddScoped<IPaymentLedgerPostingIntentRepository>(provider =>
            new InterruptedRepository(new EfPaymentLedgerPostingIntentRepository(
                provider.GetRequiredService<PaymentsDbContext>()), failure));
        using var provider = services.BuildServiceProvider();
        var payment = Payment.Create(100m, "SGD");
        var input = new PaymentLedgerPostingInput(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 300);
        using (var setup = provider.CreateScope())
        {
            var paymentDb = setup.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            await paymentDb.Database.MigrateAsync();
            paymentDb.Payments.Add(payment);
            await paymentDb.SaveChangesAsync();
            var ledgerDb = setup.ServiceProvider.GetRequiredService<LedgerDbContext>();
            await ledgerDb.Database.MigrateAsync();
            ledgerDb.LedgerAccounts.AddRange(
                LedgerAccount.Open(input.ProcessorReceivableAccountId, input.LedgerId, "SGD"),
                LedgerAccount.Open(input.MerchantPayableAccountId, input.LedgerId, "SGD"),
                LedgerAccount.Open(input.PlatformRevenueAccountId, input.LedgerId, "SGD"));
            await ledgerDb.SaveChangesAsync();
            await setup.ServiceProvider.GetRequiredService<RecordPaymentSuccessHandler>()
                .HandleAsync(new RecordPaymentSuccessCommand(payment.Id.Value, input));
        }
        var command = new DispatchPaymentLedgerPostingCommand(payment.Id.Value);
        using (var attempt = provider.CreateScope())
        {
            var handler = attempt.ServiceProvider.GetRequiredService<DispatchPaymentLedgerPostingHandler>();
            if (loseAcknowledgment)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(command));
            }
            else
            {
                Assert.True(await handler.HandleAsync(command));
            }
        }
        using (var verification = provider.CreateScope())
        {
            var db = verification.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            Assert.Equal(loseAcknowledgment ? PaymentLedgerPostingIntentStatus.Pending :
                PaymentLedgerPostingIntentStatus.Posted, (await db.PaymentLedgerPostingIntents.SingleAsync()).Status);
            Assert.Equal(1, await verification.ServiceProvider.GetRequiredService<LedgerDbContext>()
                .LedgerTransactions.CountAsync());
        }
        using (var replay = provider.CreateScope())
        {
            Assert.True(await replay.ServiceProvider.GetRequiredService<DispatchPaymentLedgerPostingHandler>()
                .HandleAsync(command));
        }
        using var read = provider.CreateScope();
        var stored = await read.ServiceProvider.GetRequiredService<PaymentsDbContext>()
            .PaymentLedgerPostingIntents.SingleAsync();
        Assert.Equal(PaymentLedgerPostingIntentStatus.Posted, stored.Status);
        Assert.Equal(input.TransactionId, stored.TransactionId);
        var ledgerContext = read.ServiceProvider.GetRequiredService<LedgerDbContext>();
        var transaction = Assert.Single(await ledgerContext.LedgerTransactions.Include(x => x.Entries).ToListAsync());
        Assert.Equal(input.TransactionId, transaction.Id);
        Assert.Equal(input.LedgerId, transaction.LedgerId);
        Assert.Equal(3, transaction.Entries.Count);
        Assert.Contains(transaction.Entries, x => x.AccountId == input.ProcessorReceivableAccountId &&
            x.Direction == LedgerDirection.Debit && x.AmountMinorUnits == 10_000);
        Assert.Contains(transaction.Entries, x => x.AccountId == input.MerchantPayableAccountId &&
            x.Direction == LedgerDirection.Credit && x.AmountMinorUnits == 9_700);
        Assert.Contains(transaction.Entries, x => x.AccountId == input.PlatformRevenueAccountId &&
            x.Direction == LedgerDirection.Credit && x.AmountMinorUnits == 300);
    }

    private sealed class FailureGate
    {
        public bool Fail { get; set; }
    }

    private sealed class InterruptedRepository(
        EfPaymentLedgerPostingIntentRepository inner, FailureGate failure)
        : IPaymentLedgerPostingIntentRepository
    {
        public Task<IReadOnlyList<PaymentLedgerPostingIntent>> GetPendingAsync(
            int limit, CancellationToken cancellationToken = default) => inner.GetPendingAsync(limit, cancellationToken);
        public Task<bool> ScheduleRetryAsync(PaymentId paymentId, DateTimeOffset nextAttemptAt,
            CancellationToken cancellationToken = default) => inner.ScheduleRetryAsync(paymentId, nextAttemptAt, cancellationToken);
        public Task AddAsync(PaymentLedgerPostingIntent intent, CancellationToken cancellationToken = default)
            => inner.AddAsync(intent, cancellationToken);
        public Task<PaymentLedgerPostingIntent?> GetByPaymentIdAsync(PaymentId paymentId,
            CancellationToken cancellationToken = default) => inner.GetByPaymentIdAsync(paymentId, cancellationToken);
        public Task<bool> MarkPostedAsync(PaymentId paymentId, CancellationToken cancellationToken = default)
        {
            if (failure.Fail)
            {
                failure.Fail = false;
                throw new InvalidOperationException("Simulated interruption after Ledger commit.");
            }
            return inner.MarkPostedAsync(paymentId, cancellationToken);
        }
    }
}
