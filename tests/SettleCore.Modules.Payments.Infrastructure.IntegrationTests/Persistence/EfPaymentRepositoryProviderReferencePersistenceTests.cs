using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace SettleCore.Modules.Payments.Infrastructure.IntegrationTests.Persistence;

public sealed class EfPaymentRepositoryProviderReferencePersistenceTests
{
    [Fact]
    public async Task AddAsyncPersistsProviderPaymentReference()
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
            300.00m,
            "SGD");

        var providerReference =
            ProviderPaymentReference.Create(
                "stripe",
                "pi_3ABC123");

        payment.AttachProviderReference(
            providerReference);

        await repository.AddAsync(payment);

        dbContext.ChangeTracker.Clear();

        var reloadedPayment =
            Assert.IsType<Payment>(
                await repository.GetByIdAsync(payment.Id));

        var reloadedReference =
            Assert.IsType<ProviderPaymentReference>(
                reloadedPayment.ProviderReference);

        Assert.Equal(
            "stripe",
            reloadedReference.Provider);

        Assert.Equal(
            "pi_3ABC123",
            reloadedReference.Reference);
    }
}