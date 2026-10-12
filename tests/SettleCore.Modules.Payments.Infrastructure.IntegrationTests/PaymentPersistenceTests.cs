using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SettleCore.Modules.Payments.Infrastructure.IntegrationTests;

public sealed class PaymentPersistenceTests
{
    [Fact]
    public async Task SaveAndReloadPaymentPreservesState()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("settlecore_test")
            .WithUsername("settlecore")
            .WithPassword("settlecore")
            .Build();

        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;

        await using var dbContext = new PaymentsDbContext(options);

        await dbContext.Database.MigrateAsync();

        var payment = Payment.Create(125.50m, "sgd");

        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();

        var persistedPayment = await dbContext.Payments.SingleAsync();

        Assert.Equal(payment.Id, persistedPayment.Id);
        Assert.Equal(125.50m, persistedPayment.Amount);
        Assert.Equal("SGD", persistedPayment.Currency);
        Assert.Equal(PaymentStatus.Pending, persistedPayment.Status);
    }
}