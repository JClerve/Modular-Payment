using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SettleCore.Modules.Ledger.Application.PostLedgerTransaction;
using SettleCore.Modules.Ledger.Domain;
using SettleCore.Modules.Ledger.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using SettleCore.Modules.Ledger.Application.GetLedgerTransactionById;

namespace SettleCore.IntegrationTests;

public sealed class LedgerPostgreSqlEndToEndTests
{
    [Fact]
    public async Task ProvisioningPreservesExplicitIdentityAndRejectsConflictingReplay()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await postgres.StartAsync();
        using var factory = new LedgerApiFactory(postgres.GetConnectionString());
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<LedgerDbContext>().Database.MigrateAsync();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var accountId = Guid.NewGuid();
        var ledgerId = Guid.NewGuid();
        using var created = await client.PostAsJsonAsync("/ledger/accounts", new { accountId, ledgerId, currency = "sgd" });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var retries = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            client.PostAsJsonAsync("/ledger/accounts", new { accountId, ledgerId, currency = "SGD" })));
        foreach (var retry in retries) { Assert.Equal(HttpStatusCode.OK, retry.StatusCode); retry.Dispose(); }
        using var currencyConflict = await client.PostAsJsonAsync("/ledger/accounts", new { accountId, ledgerId, currency = "USD" });
        using var identityConflict = await client.PostAsJsonAsync("/ledger/accounts", new { accountId, ledgerId = Guid.NewGuid(), currency = "SGD" });
        using var invalid = await client.PostAsJsonAsync("/ledger/accounts", new { accountId = Guid.Empty, ledgerId, currency = "SGD" });
        Assert.Equal(HttpStatusCode.Conflict, currencyConflict.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, identityConflict.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var verification = factory.Services.CreateScope();
        var account = Assert.Single(await verification.ServiceProvider.GetRequiredService<LedgerDbContext>().LedgerAccounts.AsNoTracking().ToListAsync());
        Assert.Equal(accountId, account.Id);
        Assert.Equal(ledgerId, account.LedgerId);
        Assert.Equal("SGD", account.Currency);
    }

    [Fact]
    public async Task PostingRetrySucceedsAndConflictingPayloadReturnsConflict()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await postgres.StartAsync();
        using var factory = new LedgerApiFactory(postgres.GetConnectionString());
        var ledgerId = Guid.NewGuid();
        var debit = LedgerAccount.Open(Guid.NewGuid(), ledgerId, "SGD");
        var credit = LedgerAccount.Open(Guid.NewGuid(), ledgerId, "SGD");
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
            await context.Database.MigrateAsync();
            context.LedgerAccounts.AddRange(debit, credit);
            await context.SaveChangesAsync();
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var transactionId = Guid.NewGuid();
        object Request(long amount) => new {
            transactionId, ledgerId,
            entries = new[] {
                new { accountId = debit.Id, currency = "SGD", direction = LedgerDirection.Debit, amountMinorUnits = amount },
                new { accountId = credit.Id, currency = "SGD", direction = LedgerDirection.Credit, amountMinorUnits = amount } }
        };
        using var original = await client.PostAsJsonAsync("/ledger/transactions", Request(1000));
        using var retry = await client.PostAsJsonAsync("/ledger/transactions", Request(1000));
        using var conflict = await client.PostAsJsonAsync("/ledger/transactions", Request(2000));
        Assert.Equal(HttpStatusCode.Created, original.StatusCode);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(original.Headers.Location, retry.Headers.Location);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var verification = factory.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<LedgerDbContext>();
        Assert.Equal(1, await db.LedgerTransactions.CountAsync());
        Assert.Equal(2, await db.LedgerEntries.CountAsync());
        Assert.All(await db.LedgerEntries.ToListAsync(), entry => Assert.Equal(1000, entry.AmountMinorUnits));
    }

    [Fact]
    public async Task PostLedgerTransactionPersistsEntriesInPostgreSql()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("settlecore_test")
            .WithUsername("settlecore")
            .WithPassword("settlecore")
            .Build();

        await postgres.StartAsync();

        using var factory = new LedgerApiFactory(postgres.GetConnectionString());
        var ledgerId = Guid.NewGuid();
        var debitAccount = LedgerAccount.Open(Guid.NewGuid(), ledgerId, "SGD");
        var creditAccount = LedgerAccount.Open(Guid.NewGuid(), ledgerId, "SGD");

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
            await context.Database.MigrateAsync();
            context.LedgerAccounts.AddRange(debitAccount, creditAccount);
            await context.SaveChangesAsync();
        }

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var transactionId = Guid.NewGuid();
        var response = await client.PostAsJsonAsync(
            "/ledger/transactions",
            new
            {
                transactionId,
                ledgerId,
                entries = new[]
                {
                    new
                    {
                        accountId = debitAccount.Id,
                        currency = "sgd",
                        direction = LedgerDirection.Debit,
                        amountMinorUnits = 1000L
                    },
                    new
                    {
                        accountId = creditAccount.Id,
                        currency = "SGD",
                        direction = LedgerDirection.Credit,
                        amountMinorUnits = 1000L
                    }
                }
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = Assert.IsType<PostLedgerTransactionResult>(
            await response.Content.ReadFromJsonAsync<PostLedgerTransactionResult>());
        Assert.Equal(transactionId, created.TransactionId);
        Assert.Equal(ledgerId, created.LedgerId);
        Assert.Equal(2, created.EntryCount);
        Assert.Equal($"/ledger/transactions/{transactionId}",
            response.Headers.Location?.OriginalString);

        using var verificationScope = factory.Services.CreateScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<LedgerDbContext>();
        var persisted = await verificationContext.LedgerTransactions
            .AsNoTracking()
            .Include(transaction => transaction.Entries)
            .SingleAsync(transaction => transaction.Id == transactionId);

        Assert.Equal(ledgerId, persisted.LedgerId);
        Assert.Equal(2, persisted.Entries.Count);
        Assert.All(persisted.Entries, entry =>
        {
            Assert.Equal(transactionId, entry.TransactionId);
            Assert.Equal("SGD", entry.Currency);
            Assert.Equal(1000L, entry.AmountMinorUnits);
        });
        var debit = Assert.Single(persisted.Entries,
            entry => entry.Direction == LedgerDirection.Debit);
        var credit = Assert.Single(persisted.Entries,
            entry => entry.Direction == LedgerDirection.Credit);
        Assert.Equal(debitAccount.Id, debit.AccountId);
        Assert.Equal(creditAccount.Id, credit.AccountId);
    }

    private sealed class LedgerApiFactory(string connectionString)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<LedgerDbContext>>();
                services.RemoveAll<DbContextOptions<LedgerDbContext>>();
                services.RemoveAll<LedgerDbContext>();
                services.AddDbContext<LedgerDbContext>(
                    options => options.UseNpgsql(connectionString));
            });
        }
    }

    [Fact]
    public async Task PostedLedgerTransactionCanBeRetrievedFromPostgreSql()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("settlecore_test")
            .WithUsername("settlecore")
            .WithPassword("settlecore")
            .Build();

        await postgres.StartAsync();

        using var factory =
            new LedgerApiFactory(postgres.GetConnectionString());

        var ledgerId = Guid.NewGuid();

        var debitAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        var creditAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        using (var scope = factory.Services.CreateScope())
        {
            var context =
                scope.ServiceProvider
                    .GetRequiredService<LedgerDbContext>();

            await context.Database.MigrateAsync();

            context.LedgerAccounts.AddRange(
                debitAccount,
                creditAccount);

            await context.SaveChangesAsync();
        }

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var transactionId = Guid.NewGuid();

        var postResponse = await client.PostAsJsonAsync(
            "/ledger/transactions",
            new
            {
                transactionId,
                ledgerId,
                entries = new[]
                {
                    new
                    {
                        accountId = debitAccount.Id,
                        currency = "SGD",
                        direction = LedgerDirection.Debit,
                        amountMinorUnits = 2500L
                    },
                    new
                    {
                        accountId = creditAccount.Id,
                        currency = "SGD",
                        direction = LedgerDirection.Credit,
                        amountMinorUnits = 2500L
                    }
                }
            });

        Assert.Equal(
            HttpStatusCode.Created,
            postResponse.StatusCode);

        var getResponse = await client.GetAsync(
            $"/ledger/transactions/{transactionId}");

        Assert.Equal(
            HttpStatusCode.OK,
            getResponse.StatusCode);

        var retrieved =
            Assert.IsType<GetLedgerTransactionByIdResult>(
                await getResponse.Content
                    .ReadFromJsonAsync<GetLedgerTransactionByIdResult>());

        Assert.Equal(
            transactionId,
            retrieved.TransactionId);

        Assert.Equal(
            ledgerId,
            retrieved.LedgerId);

        Assert.Equal(
            2,
            retrieved.Entries.Count);

        var debit = Assert.Single(
            retrieved.Entries,
            entry => entry.Direction == LedgerDirection.Debit);

        Assert.Equal(
            debitAccount.Id,
            debit.AccountId);

        Assert.Equal(
            "SGD",
            debit.Currency);

        Assert.Equal(
            2500L,
            debit.AmountMinorUnits);

        var credit = Assert.Single(
            retrieved.Entries,
            entry => entry.Direction == LedgerDirection.Credit);

        Assert.Equal(
            creditAccount.Id,
            credit.AccountId);

        Assert.Equal(
            "SGD",
            credit.Currency);

        Assert.Equal(
            2500L,
            credit.AmountMinorUnits);
    }
}
