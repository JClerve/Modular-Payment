using Microsoft.EntityFrameworkCore;
using Demopay.Modules.Payments.Domain;
using Demopay.Modules.Payments.Infrastructure.Persistence;
using Demopay.Modules.Payments.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace Demopay.Modules.Payments.Infrastructure.IntegrationTests.Persistence;

public sealed class EfPaymentRepositoryProviderReferenceLookupTests
{
    [Fact]
    public async Task GetByProviderReferenceAsyncReturnsMatchingPayment()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("Demopay_test")
            .WithUsername("Demopay")
            .WithPassword("Demopay")
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
            100.00m,
            "SGD");

        payment.AttachProviderReference(
            ProviderPaymentReference.Create(
                "stripe",
                "pi_3ABC123"));

        await repository.AddAsync(payment);

        dbContext.ChangeTracker.Clear();

        var found = await repository.GetByProviderReferenceAsync(
            ProviderPaymentReference.Create(
                "stripe",
                "pi_3ABC123"));

        var foundPayment =
            Assert.IsType<Payment>(found);

        Assert.Equal(
            payment.Id,
            foundPayment.Id);
    }

    [Fact]
    public async Task GetByProviderReferenceAsyncReturnsNullWhenNoPaymentMatches()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("Demopay_test")
            .WithUsername("Demopay")
            .WithPassword("Demopay")
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

        var found = await repository.GetByProviderReferenceAsync(
            ProviderPaymentReference.Create(
                "stripe",
                "pi_missing"));

        Assert.Null(found);
    }
}
