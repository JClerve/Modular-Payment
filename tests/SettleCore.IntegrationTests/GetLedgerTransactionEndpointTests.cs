using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SettleCore.Modules.Ledger.Application;
using SettleCore.Modules.Ledger.Application.GetLedgerTransactionById;
using SettleCore.Modules.Ledger.Domain;

namespace SettleCore.IntegrationTests;

public sealed class GetLedgerTransactionEndpointTests
{
    [Fact]
    public async Task GetLedgerTransactionReturnsTransactionWhenFound()
    {
        var transactionId = Guid.NewGuid();
        var ledgerId = Guid.NewGuid();

        var debitAccountId = Guid.NewGuid();
        var creditAccountId = Guid.NewGuid();

        var transaction = LedgerTransaction.Rehydrate(
            transactionId,
            ledgerId,
            [
                LedgerEntry.Create(
                    debitAccountId,
                    "SGD",
                    LedgerDirection.Debit,
                    1000),

                LedgerEntry.Create(
                    creditAccountId,
                    "SGD",
                    LedgerDirection.Credit,
                    1000)
            ]);

        using var factory =
            new LedgerApiFactory(transaction);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.GetAsync(
            $"/ledger/transactions/{transactionId}");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        var result =
            Assert.IsType<GetLedgerTransactionByIdResult>(
                await response.Content
                    .ReadFromJsonAsync<GetLedgerTransactionByIdResult>());

        Assert.Equal(
            transactionId,
            result.TransactionId);

        Assert.Equal(
            ledgerId,
            result.LedgerId);

        Assert.Equal(
            2,
            result.Entries.Count);

        Assert.Contains(
            result.Entries,
            entry =>
                entry.AccountId == debitAccountId &&
                entry.Currency == "SGD" &&
                entry.Direction == LedgerDirection.Debit &&
                entry.AmountMinorUnits == 1000);

        Assert.Contains(
            result.Entries,
            entry =>
                entry.AccountId == creditAccountId &&
                entry.Currency == "SGD" &&
                entry.Direction == LedgerDirection.Credit &&
                entry.AmountMinorUnits == 1000);
    }

    private sealed class LedgerApiFactory(
        LedgerTransaction transaction)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILedgerRepository>();

                services.AddSingleton<ILedgerRepository>(
                    new StubLedgerRepository(transaction));
            });
        }
    }

    private sealed class StubLedgerRepository(
        LedgerTransaction transaction)
        : ILedgerRepository
    {
        public Task<IReadOnlyList<LedgerAccount>>
            GetAccountsByIdsAsync(
                IReadOnlyCollection<Guid> accountIds,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<LedgerTransaction?> GetTransactionByIdAsync(
            Guid transactionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                transaction.Id == transactionId
                    ? transaction
                    : null);
        }

        public Task AddTransactionAsync(
            LedgerTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    [Fact]
    public async Task GetLedgerTransactionReturnsNotFoundWhenMissing()
    {
        var existingTransaction = LedgerTransaction.Rehydrate(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [
                LedgerEntry.Create(
                    Guid.NewGuid(),
                    "SGD",
                    LedgerDirection.Debit,
                    1000),

                LedgerEntry.Create(
                    Guid.NewGuid(),
                    "SGD",
                    LedgerDirection.Credit,
                    1000)
            ]);

        using var factory =
            new LedgerApiFactory(existingTransaction);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var missingTransactionId = Guid.NewGuid();

        var response = await client.GetAsync(
            $"/ledger/transactions/{missingTransactionId}");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
}
