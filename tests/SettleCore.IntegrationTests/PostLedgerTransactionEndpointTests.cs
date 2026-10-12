using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SettleCore.Modules.Ledger.Application;
using SettleCore.Modules.Ledger.Application.PostLedgerTransaction;
using SettleCore.Modules.Ledger.Domain;

namespace SettleCore.IntegrationTests;

public sealed class PostLedgerTransactionEndpointTests
{
    [Fact]
    public async Task PostLedgerTransactionCreatesAndPersistsTransaction()
    {
        var ledgerId = Guid.NewGuid();

        var debitAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        var creditAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        using var factory = new LedgerApiFactory(
            debitAccount,
            creditAccount);

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
                        currency = "SGD",
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

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);

        var created =
            Assert.IsType<PostLedgerTransactionResult>(
                await response.Content
                    .ReadFromJsonAsync<PostLedgerTransactionResult>());

        Assert.Equal(
            transactionId,
            created.TransactionId);

        Assert.Equal(
            ledgerId,
            created.LedgerId);

        Assert.Equal(
            2,
            created.EntryCount);

        Assert.Equal(
            $"/ledger/transactions/{transactionId}",
            response.Headers.Location?.OriginalString);

        Assert.NotNull(factory.Repository.AddedTransaction);

        Assert.Equal(
            transactionId,
            factory.Repository.AddedTransaction.Id);
    }

    [Fact]
    public async Task PostLedgerTransactionRejectsUnbalancedEntriesWithoutPersisting()
    {
        var ledgerId = Guid.NewGuid();

        var debitAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        var creditAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        using var factory = new LedgerApiFactory(
            debitAccount,
            creditAccount);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var response = await client.PostAsJsonAsync(
            "/ledger/transactions",
            new
            {
                transactionId = Guid.NewGuid(),
                ledgerId,
                entries = new[]
                {
                    new
                    {
                        accountId = debitAccount.Id,
                        currency = "SGD",
                        direction = LedgerDirection.Debit,
                        amountMinorUnits = 1000L
                    },
                    new
                    {
                        accountId = creditAccount.Id,
                        currency = "SGD",
                        direction = LedgerDirection.Credit,
                        amountMinorUnits = 900L
                    }
                }
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        var problem =
            await response.Content.ReadFromJsonAsync<
                Microsoft.AspNetCore.Mvc.ValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("entries", problem.Errors.Keys);
        Assert.Null(factory.Repository.AddedTransaction);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"entries\":null}")]
    [InlineData("{\"entries\":[null,null]}")]
    public async Task PostLedgerTransactionRejectsMalformedEntriesWithoutPersisting(
        string json)
    {
        using var factory = new LedgerApiFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        using var content = new StringContent(
            json,
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync(
            "/ledger/transactions",
            content);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        var problem =
            await response.Content.ReadFromJsonAsync<
                Microsoft.AspNetCore.Mvc.ValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains("entries", problem.Errors.Keys);
        Assert.Null(factory.Repository.AddedTransaction);
    }

    [Theory]
    [InlineData("missing-account", "accounts")]
    [InlineData("wrong-ledger", "accounts")]
    [InlineData("wrong-currency", "entries")]
    [InlineData("invalid-currency", "currency")]
    [InlineData("invalid-direction", "direction")]
    [InlineData("zero-amount", "amountMinorUnits")]
    [InlineData("negative-amount", "amountMinorUnits")]
    [InlineData("empty-transaction-id", "id")]
    [InlineData("empty-ledger-id", "ledgerId")]
    [InlineData("empty-account-id", "accountId")]
    [InlineData("empty-entries", "entries")]
    public async Task PostLedgerTransactionRejectsInvalidTransactionWithoutPersisting(
        string scenario,
        string expectedError)
    {
        var ledgerId = Guid.NewGuid();

        var debitAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        var creditAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            scenario == "wrong-ledger"
                ? Guid.NewGuid()
                : ledgerId,
            "SGD");

        using var factory = new LedgerApiFactory(
            debitAccount,
            creditAccount);

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        var entries = new[]
        {
            new PostLedgerTransactionEntry(
                scenario == "empty-account-id"
                    ? Guid.Empty
                    : debitAccount.Id,
                scenario switch
                {
                    "wrong-currency" => "USD",
                    "invalid-currency" => "12",
                    _ => "SGD"
                },
                scenario == "invalid-direction"
                    ? (LedgerDirection)99
                    : LedgerDirection.Debit,
                scenario switch
                {
                    "zero-amount" => 0L,
                    "negative-amount" => -1L,
                    _ => 1000L
                }),

            new PostLedgerTransactionEntry(
                scenario == "missing-account"
                    ? Guid.NewGuid()
                    : creditAccount.Id,
                "SGD",
                LedgerDirection.Credit,
                1000L)
        };

        var response = await client.PostAsJsonAsync(
            "/ledger/transactions",
            new
            {
                transactionId =
                    scenario == "empty-transaction-id"
                        ? Guid.Empty
                        : Guid.NewGuid(),

                ledgerId =
                    scenario == "empty-ledger-id"
                        ? Guid.Empty
                        : ledgerId,

                entries =
                    scenario == "empty-entries"
                        ? []
                        : entries
            });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        var problem =
            await response.Content.ReadFromJsonAsync<
                Microsoft.AspNetCore.Mvc.ValidationProblemDetails>();

        Assert.NotNull(problem);
        Assert.Contains(expectedError, problem.Errors.Keys);
        Assert.Null(factory.Repository.AddedTransaction);
    }

    private sealed class LedgerApiFactory(
        params LedgerAccount[] accounts)
        : WebApplicationFactory<Program>
    {
        public RecordingLedgerRepository Repository { get; } =
            new(accounts);

        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ILedgerRepository>();

                services.AddSingleton<ILedgerRepository>(
                    Repository);
            });
        }
    }

    public sealed class RecordingLedgerRepository(
        params LedgerAccount[] accounts)
        : ILedgerRepository
    {
        private readonly IReadOnlyList<LedgerAccount> accounts =
            accounts;

        public LedgerTransaction? AddedTransaction { get; private set; }

        public Task<IReadOnlyList<LedgerAccount>>
            GetAccountsByIdsAsync(
                IReadOnlyCollection<Guid> accountIds,
                CancellationToken cancellationToken = default)
        {
            IReadOnlyList<LedgerAccount> matches =
                accounts
                    .Where(account =>
                        accountIds.Contains(account.Id))
                    .ToArray();

            return Task.FromResult(matches);
        }

        public Task<LedgerTransaction?> GetTransactionByIdAsync(
            Guid transactionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                AddedTransaction?.Id == transactionId
                    ? AddedTransaction
                    : null);
        }

        public Task AddTransactionAsync(
            LedgerTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            AddedTransaction = transaction;

            return Task.CompletedTask;
        }
    }
}
