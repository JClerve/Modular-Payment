using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SettleCore.Modules.Payments.Infrastructure.IntegrationTests;

public sealed class PaymentLedgerPostingIntentPersistenceTests
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

        dbContext.PaymentLedgerPostingIntents.Add(intent);

        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();

        var reloaded = await dbContext.PaymentLedgerPostingIntents
            .SingleAsync(x => x.PaymentId == paymentId);

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
}
