using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace SettleCore.Modules.Payments.Infrastructure.IntegrationTests;

public sealed class EfPaymentRepositoryTests
{
    [Fact]
    public async Task AddAndGetByIdRoundTripsPayment()
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

        var repository = new EfPaymentRepository(dbContext);
        var payment = Payment.Create(250.75m, "sgd");

        await repository.AddAsync(payment);

        dbContext.ChangeTracker.Clear();

        var result = await repository.GetByIdAsync(payment.Id);
        var persistedPayment = Assert.IsType<Payment>(result);

        Assert.Equal(payment.Id, persistedPayment.Id);
        Assert.Equal(250.75m, persistedPayment.Amount);
        Assert.Equal("SGD", persistedPayment.Currency);
        Assert.Equal(PaymentStatus.Pending, persistedPayment.Status);
    }

    [Fact]
    public async Task GetByIdReturnsNullWhenPaymentDoesNotExist()
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

        var repository = new EfPaymentRepository(dbContext);
        var missingPaymentId = PaymentId.From(Guid.NewGuid());

        var result = await repository.GetByIdAsync(missingPaymentId);

        Assert.Null(result);
    }
}
