using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SettleCore.Modules.Ledger.Domain;
using SettleCore.Modules.Ledger.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SettleCore.Modules.Ledger.Infrastructure.IntegrationTests.Persistence;

public sealed class EfLedgerRepositoryTests
{
    private sealed class ConcurrentInsertBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref arrivals) == 2)
            {
                ready.TrySetResult();
            }
            // Both repositories must finish their missing-ID lookup before either inserts.
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return result;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RepeatedTransactionIsStoredOnce(bool concurrent)
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options;
        await using var setup = new LedgerDbContext(options);
        await setup.Database.MigrateAsync();
        var ledgerId = Guid.NewGuid();
        var accounts = new[] { LedgerAccount.Open(Guid.NewGuid(), ledgerId, "SGD"), LedgerAccount.Open(Guid.NewGuid(), ledgerId, "SGD") };
        setup.LedgerAccounts.AddRange(accounts);
        await setup.SaveChangesAsync();
        var entries = new[] {
            LedgerEntry.Create(accounts[0].Id, "SGD", LedgerDirection.Debit, 1000),
            LedgerEntry.Create(accounts[1].Id, "SGD", LedgerDirection.Credit, 1000) };
        var transaction = LedgerTransaction.Post(Guid.NewGuid(), ledgerId, entries, accounts);
        var reordered = LedgerTransaction.Post(transaction.Id, ledgerId, entries.Reverse(), accounts);
        if (concurrent)
        {
            options = new DbContextOptionsBuilder<LedgerDbContext>(options)
                .AddInterceptors(new ConcurrentInsertBarrier()).Options;
        }
        await using var first = new LedgerDbContext(options);
        await using var second = new LedgerDbContext(options);
        var firstRepository = new EfLedgerRepository(first);
        var secondRepository = new EfLedgerRepository(second);
        if (concurrent)
        {
            await Task.WhenAll(firstRepository.AddTransactionAsync(transaction), secondRepository.AddTransactionAsync(reordered));
        }
        else
        {
            await firstRepository.AddTransactionAsync(transaction);
            await firstRepository.AddTransactionAsync(reordered);
            await secondRepository.AddTransactionAsync(reordered);
        }
        Assert.Equal(1, await setup.LedgerTransactions.CountAsync());
        Assert.Equal(2, await setup.LedgerEntries.CountAsync());
        // The losing/retried context remains usable after duplicate handling.
        await second.SaveChangesAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReusedTransactionIdWithDifferentAmountIsRejected(bool concurrent)
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(postgres.GetConnectionString()).Options;
        await using var setup = new LedgerDbContext(options);
        await setup.Database.MigrateAsync();
        var ledgerId = Guid.NewGuid();
        var accounts = new[] { LedgerAccount.Open(Guid.NewGuid(), ledgerId, "SGD"), LedgerAccount.Open(Guid.NewGuid(), ledgerId, "SGD") };
        setup.LedgerAccounts.AddRange(accounts);
        await setup.SaveChangesAsync();
        var id = Guid.NewGuid();
        LedgerTransaction Create(long amount) => LedgerTransaction.Post(id, ledgerId, new[] {
            LedgerEntry.Create(accounts[0].Id, "SGD", LedgerDirection.Debit, amount),
            LedgerEntry.Create(accounts[1].Id, "SGD", LedgerDirection.Credit, amount) }, accounts);
        if (concurrent)
        {
            options = new DbContextOptionsBuilder<LedgerDbContext>(options)
                .AddInterceptors(new ConcurrentInsertBarrier()).Options;
        }
        await using var first = new LedgerDbContext(options);
        await using var second = new LedgerDbContext(options);
        var firstRepository = new EfLedgerRepository(first);
        var secondRepository = new EfLedgerRepository(second);
        if (concurrent)
        {
            await Assert.ThrowsAsync<LedgerTransactionConflictException>(() => Task.WhenAll(
                firstRepository.AddTransactionAsync(Create(1000)), secondRepository.AddTransactionAsync(Create(2000))));
        }
        else
        {
            await firstRepository.AddTransactionAsync(Create(1000));
            await Assert.ThrowsAsync<LedgerTransactionConflictException>(() => secondRepository.AddTransactionAsync(Create(2000)));
        }
        Assert.Equal(1, await setup.LedgerTransactions.CountAsync());
        Assert.Equal(2, await setup.LedgerEntries.CountAsync());
        await second.SaveChangesAsync();
    }

    [Fact]
    public async Task AddTransactionAsyncPersistsTransactionAndEntries()
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

        var debitAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        var creditAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        dbContext.LedgerAccounts.AddRange(
            debitAccount,
            creditAccount);

        await dbContext.SaveChangesAsync();

        var entries = new[]
        {
            LedgerEntry.Create(
                debitAccount.Id,
                "SGD",
                LedgerDirection.Debit,
                1000),

            LedgerEntry.Create(
                creditAccount.Id,
                "SGD",
                LedgerDirection.Credit,
                1000)
        };

        var transaction = LedgerTransaction.Post(
            Guid.NewGuid(),
            ledgerId,
            entries,
            new[]
            {
                debitAccount,
                creditAccount
            });

        var repository =
            new EfLedgerRepository(dbContext);

        await repository.AddTransactionAsync(transaction);

        dbContext.ChangeTracker.Clear();

        var persisted =
            await dbContext.LedgerTransactions
                .Include(record => record.Entries)
                .SingleAsync();

        Assert.Equal(transaction.Id, persisted.Id);
        Assert.Equal(ledgerId, persisted.LedgerId);
        Assert.Equal(2, persisted.Entries.Count);

        Assert.Contains(
            persisted.Entries,
            entry =>
                entry.AccountId == debitAccount.Id &&
                entry.Currency == "SGD" &&
                entry.Direction == LedgerDirection.Debit &&
                entry.AmountMinorUnits == 1000);

        Assert.Contains(
            persisted.Entries,
            entry =>
                entry.AccountId == creditAccount.Id &&
                entry.Currency == "SGD" &&
                entry.Direction == LedgerDirection.Credit &&
                entry.AmountMinorUnits == 1000);
    }

    [Fact]
    public async Task GetAccountsByIdsAsyncReturnsRequestedAccounts()
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

        var firstAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        var secondAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "USD");

        var unrelatedAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "EUR");

        dbContext.LedgerAccounts.AddRange(
            firstAccount,
            secondAccount,
            unrelatedAccount);

        await dbContext.SaveChangesAsync();

        dbContext.ChangeTracker.Clear();

        var repository =
            new EfLedgerRepository(dbContext);

        var accounts =
            await repository.GetAccountsByIdsAsync(
                new[]
                {
                    firstAccount.Id,
                    secondAccount.Id
                });

        Assert.Equal(2, accounts.Count);

        Assert.Contains(
            accounts,
            account =>
                account.Id == firstAccount.Id &&
                account.LedgerId == ledgerId &&
                account.Currency == "SGD");

        Assert.Contains(
            accounts,
            account =>
                account.Id == secondAccount.Id &&
                account.LedgerId == ledgerId &&
                account.Currency == "USD");

        Assert.DoesNotContain(
            accounts,
            account =>
                account.Id == unrelatedAccount.Id);
    }

    [Fact]
    public async Task GetTransactionByIdAsyncReturnsPersistedTransaction()
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

        var debitAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        var creditAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        dbContext.LedgerAccounts.AddRange(
            debitAccount,
            creditAccount);

        await dbContext.SaveChangesAsync();

        var transactionId = Guid.NewGuid();

        var transaction = LedgerTransaction.Post(
            transactionId,
            ledgerId,
            new[]
            {
                LedgerEntry.Create(
                    debitAccount.Id,
                    "SGD",
                    LedgerDirection.Debit,
                    1000),

                LedgerEntry.Create(
                    creditAccount.Id,
                    "SGD",
                    LedgerDirection.Credit,
                    1000)
            },
            new[]
            {
                debitAccount,
                creditAccount
            });

        var repository =
            new EfLedgerRepository(dbContext);

        await repository.AddTransactionAsync(transaction);

        dbContext.ChangeTracker.Clear();

        var result =
            await repository.GetTransactionByIdAsync(
                transactionId);

        Assert.NotNull(result);

        Assert.Equal(transactionId, result.Id);
        Assert.Equal(ledgerId, result.LedgerId);
        Assert.Equal(2, result.Entries.Count);

        Assert.Contains(
            result.Entries,
            entry =>
                entry.AccountId == debitAccount.Id &&
                entry.Currency == "SGD" &&
                entry.Direction == LedgerDirection.Debit &&
                entry.AmountMinorUnits == 1000);

        Assert.Contains(
            result.Entries,
            entry =>
                entry.AccountId == creditAccount.Id &&
                entry.Currency == "SGD" &&
                entry.Direction == LedgerDirection.Credit &&
                entry.AmountMinorUnits == 1000);
    }

    [Fact]
    public async Task GetTransactionByIdAsyncReturnsNullWhenMissing()
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

        var repository =
            new EfLedgerRepository(dbContext);

        var result =
            await repository.GetTransactionByIdAsync(
                Guid.NewGuid());

        Assert.Null(result);
    }
}
