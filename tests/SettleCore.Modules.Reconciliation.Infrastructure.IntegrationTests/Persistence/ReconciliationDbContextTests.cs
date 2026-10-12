using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Reconciliation.Domain;
using SettleCore.Modules.Reconciliation.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SettleCore.Modules.Reconciliation.Infrastructure.IntegrationTests.Persistence;

public sealed class ReconciliationDbContextTests
{
    [Fact]
    public async Task CanConnectToPostgreSql()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("settlecore_test")
            .WithUsername("settlecore")
            .WithPassword("settlecore")
            .Build();

        await postgres.StartAsync();

        var options =
            new DbContextOptionsBuilder<ReconciliationDbContext>()
                .UseNpgsql(postgres.GetConnectionString())
                .Options;

        await using var dbContext =
            new ReconciliationDbContext(options);

        var canConnect =
            await dbContext.Database.CanConnectAsync();

        Assert.True(canConnect);
    }

    [Fact]
    public async Task PersistsReconciliationRecord()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("settlecore_test")
            .WithUsername("settlecore")
            .WithPassword("settlecore")
            .Build();

        await postgres.StartAsync();

        var options =
            new DbContextOptionsBuilder<ReconciliationDbContext>()
                .UseNpgsql(postgres.GetConnectionString())
                .Options;

        await using var dbContext =
            new ReconciliationDbContext(options);

        await dbContext.Database.MigrateAsync();

        var record = new ReconciliationRecord(
            Guid.NewGuid(),
            ReconciliationStatus.AmountMismatch,
            expectedAmount: 100.00m,
            actualAmount: 95.00m,
            expectedCurrency: "SGD",
            actualCurrency: "SGD");

        dbContext.ReconciliationRecords.Add(record);

        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();

        var persisted =
            await dbContext.ReconciliationRecords.SingleAsync();

        Assert.Equal(record.Id, persisted.Id);
        Assert.Equal(
            ReconciliationStatus.AmountMismatch,
            persisted.Status);
        Assert.Equal(100.00m, persisted.ExpectedAmount);
        Assert.Equal(95.00m, persisted.ActualAmount);
        Assert.Equal("SGD", persisted.ExpectedCurrency);
        Assert.Equal("SGD", persisted.ActualCurrency);
    }
}
