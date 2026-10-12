using Microsoft.EntityFrameworkCore;
using SettleCore.Modules.Ledger.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using SettleCore.Modules.Ledger.Domain;
using SettleCore.Modules.Ledger.Infrastructure.Persistence.Records;

namespace SettleCore.Modules.Ledger.Infrastructure.IntegrationTests.Persistence;

public sealed class LedgerDbContextTests
{
    [Fact]
    public async Task CanConnectToPostgreSql()
    {
        await using var postgres =
            new PostgreSqlBuilder("postgres:18-alpine")
                .WithDatabase("settlecore_test")
                .WithUsername("settlecore")
                .WithPassword("settlecore")
                .Build();

        await postgres.StartAsync();

        var options =
            new DbContextOptionsBuilder<LedgerDbContext>()
                .UseNpgsql(postgres.GetConnectionString())
                .Options;

        await using var dbContext =
            new LedgerDbContext(options);

        var canConnect =
            await dbContext.Database.CanConnectAsync();

        Assert.True(canConnect);
    }

    [Fact]
    public async Task PersistsLedgerAccount()
    {
        await using var postgres =
            new PostgreSqlBuilder("postgres:18-alpine")
                .WithDatabase("settlecore_test")
                .WithUsername("settlecore")
                .WithPassword("settlecore")
                .Build();

        await postgres.StartAsync();

        var options =
            new DbContextOptionsBuilder<LedgerDbContext>()
                .UseNpgsql(postgres.GetConnectionString())
                .Options;

        await using var dbContext =
            new LedgerDbContext(options);

        await dbContext.Database.MigrateAsync();

        var ledgerId = Guid.NewGuid();

        var account = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "sgd");

        dbContext.LedgerAccounts.Add(account);

        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();

        var persisted =
            await dbContext.LedgerAccounts.SingleAsync();

        Assert.Equal(account.Id, persisted.Id);
        Assert.Equal(ledgerId, persisted.LedgerId);
        Assert.Equal("SGD", persisted.Currency);
    }

    [Fact]
    public async Task PersistsLedgerTransactionAndEntriesAtomically()
    {
        await using var postgres =
            new PostgreSqlBuilder("postgres:18-alpine")
                .WithDatabase("settlecore_test")
                .WithUsername("settlecore")
                .WithPassword("settlecore")
                .Build();

        await postgres.StartAsync();

        var options =
            new DbContextOptionsBuilder<LedgerDbContext>()
                .UseNpgsql(postgres.GetConnectionString())
                .Options;

        await using var dbContext =
            new LedgerDbContext(options);

        await dbContext.Database.MigrateAsync();

        var ledgerId = Guid.NewGuid();

        var debitAccount =
            LedgerAccount.Open(
                Guid.NewGuid(),
                ledgerId,
                "SGD");

        var creditAccount =
            LedgerAccount.Open(
                Guid.NewGuid(),
                ledgerId,
                "SGD");

        var transactionId = Guid.NewGuid();

        var entries = new[]
        {
            new LedgerEntryRecord(
                Guid.NewGuid(),
                transactionId,
                debitAccount.Id,
                "SGD",
                LedgerDirection.Debit,
                1000),

            new LedgerEntryRecord(
                Guid.NewGuid(),
                transactionId,
                creditAccount.Id,
                "SGD",
                LedgerDirection.Credit,
                1000)
        };

        var transaction =
            new LedgerTransactionRecord(
                transactionId,
                ledgerId,
                entries);

        dbContext.LedgerAccounts.AddRange(
            debitAccount,
            creditAccount);

        dbContext.LedgerTransactions.Add(transaction);

        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();

        var persisted =
            await dbContext.LedgerTransactions
                .Include(record => record.Entries)
                .SingleAsync();

        Assert.Equal(transactionId, persisted.Id);
        Assert.Equal(ledgerId, persisted.LedgerId);
        Assert.Equal(2, persisted.Entries.Count);

        Assert.Contains(
            persisted.Entries,
            entry =>
                entry.AccountId == debitAccount.Id &&
                entry.Direction == LedgerDirection.Debit &&
                entry.AmountMinorUnits == 1000);

        Assert.Contains(
            persisted.Entries,
            entry =>
                entry.AccountId == creditAccount.Id &&
                entry.Direction == LedgerDirection.Credit &&
                entry.AmountMinorUnits == 1000);
    }
}
