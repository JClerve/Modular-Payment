using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace SettleCore.Modules.Payments.Infrastructure.IntegrationTests.Persistence;

public sealed class EfPaymentRepositoryStatusPersistenceTests
{
    [Fact]
    public async Task UpdateAsyncPersistsSucceededStatus()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("settlecore_test")
            .WithUsername("settlecore")
            .WithPassword("settlecore")
            .Build();

        await postgres.StartAsync();

        var options =
            new DbContextOptionsBuilder<PaymentsDbContext>()
                .UseNpgsql(postgres.GetConnectionString())
                .Options;

        await using var dbContext =
            new PaymentsDbContext(options);

        await dbContext.Database.MigrateAsync();

        var repository =
            new EfPaymentRepository(dbContext);

        var payment = Payment.Create(
            250.00m,
            "SGD");

        await repository.AddAsync(payment);

        dbContext.ChangeTracker.Clear();

        var persistedPayment =
            Assert.IsType<Payment>(
                await repository.GetByIdAsync(payment.Id));

        persistedPayment.MarkSucceeded();

        await repository.UpdateAsync(persistedPayment);

        dbContext.ChangeTracker.Clear();

        var reloadedPayment =
            Assert.IsType<Payment>(
                await repository.GetByIdAsync(payment.Id));

        Assert.Equal(
            PaymentStatus.Succeeded,
            reloadedPayment.Status);
    }
}
