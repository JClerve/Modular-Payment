using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;

namespace SettleCore.Modules.Payments.Infrastructure.IntegrationTests.Persistence;

public sealed class EfPaymentRepositoryProviderReferenceUniquenessTests
{
    [Fact]
    public async Task AddAsyncRejectsDuplicateProviderPaymentReference()
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

        var providerReference =
            ProviderPaymentReference.Create(
                "stripe",
                "pi_3ABC123");

        var firstPayment =
            Payment.Create(100.00m, "SGD");

        firstPayment.AttachProviderReference(
            providerReference);

        await repository.AddAsync(firstPayment);

        var secondPayment =
            Payment.Create(200.00m, "SGD");

        secondPayment.AttachProviderReference(
            providerReference);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => repository.AddAsync(secondPayment));
    }
}