using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using SettleCore.Modules.Ledger.Domain;
using SettleCore.Modules.Ledger.Infrastructure.Persistence;
using SettleCore.Modules.Payments.Application.CreatePayment;
using SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SettleCore.IntegrationTests;

public sealed class PaymentLedgerPostingWorkerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnabledWorkerPostsHttpPaymentSuccessAcrossSeparateStores(bool restartAfterFailure)
    {
        await using var payments = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await using var ledger = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await payments.StartAsync();
        await ledger.StartAsync();
        var paymentOptions = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(payments.GetConnectionString()).Options;
        var ledgerOptions = new DbContextOptionsBuilder<LedgerDbContext>()
            .UseNpgsql(ledger.GetConnectionString()).Options;
        var input = new PaymentLedgerPostingInput(Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 300);
        await using (var paymentSetup = new PaymentsDbContext(paymentOptions))
        {
            await paymentSetup.Database.MigrateAsync();
        }
        await using (var ledgerSetup = new LedgerDbContext(ledgerOptions))
        {
            await ledgerSetup.Database.MigrateAsync();
            if (!restartAfterFailure)
            {
                await ProvisionAccountsAsync(ledgerSetup, input);
            }
        }
        using var factory = new WorkerApiFactory(payments.GetConnectionString(), ledger.GetConnectionString());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        var response = await client.PostAsJsonAsync("/payments", new { amount = 100m, currency = "SGD" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payment = await response.Content.ReadFromJsonAsync<CreatePaymentResult>();
        Assert.NotNull(payment);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            $"/payments/{payment.PaymentId}/succeed", input)).StatusCode);
        if (restartAfterFailure)
        {
            await using var failureReader = new PaymentsDbContext(paymentOptions);
            var failureTimeout = Stopwatch.StartNew();
            PaymentLedgerPostingIntent? failed = null;
            while (failureTimeout.Elapsed < TimeSpan.FromSeconds(10))
            {
                failed = await failureReader.PaymentLedgerPostingIntents.AsNoTracking().SingleAsync();
                if (failed.NextAttemptAt is not null) break;
                await Task.Delay(50);
            }
            Assert.NotNull(failed);
            Assert.Equal(PaymentLedgerPostingIntentStatus.Pending, failed.Status);
            Assert.NotNull(failed.NextAttemptAt);
            factory.Dispose();
            await using var repair = new LedgerDbContext(ledgerOptions);
            Assert.Empty(await repair.LedgerTransactions.ToListAsync());
            await ProvisionAccountsAsync(repair, input);
        }
        using var restartedFactory = restartAfterFailure
            ? new WorkerApiFactory(payments.GetConnectionString(), ledger.GetConnectionString()) : null;
        using var restartedClient = restartedFactory?.CreateClient();
        await using var reader = new PaymentsDbContext(paymentOptions);
        PaymentLedgerPostingIntent? intent = null;
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            intent = await reader.PaymentLedgerPostingIntents.AsNoTracking().SingleAsync();
            if (intent.Status == PaymentLedgerPostingIntentStatus.Posted)
            {
                break;
            }
            await Task.Delay(50);
        }
        Assert.NotNull(intent);
        Assert.Equal(PaymentLedgerPostingIntentStatus.Posted, intent.Status);
        await using var ledgerReader = new LedgerDbContext(ledgerOptions);
        var transaction = Assert.Single(await ledgerReader.LedgerTransactions.Include(x => x.Entries).ToListAsync());
        Assert.Equal(input.TransactionId, transaction.Id);
        Assert.Equal(3, transaction.Entries.Count);
        Assert.Equal(10_000, transaction.Entries.Where(x => x.Direction == LedgerDirection.Debit)
            .Sum(x => x.AmountMinorUnits));
        Assert.Equal(10_000, transaction.Entries.Where(x => x.Direction == LedgerDirection.Credit)
            .Sum(x => x.AmountMinorUnits));
    }

    private static async Task ProvisionAccountsAsync(LedgerDbContext db, PaymentLedgerPostingInput input)
    {
        db.LedgerAccounts.AddRange(
            LedgerAccount.Open(input.ProcessorReceivableAccountId, input.LedgerId, "SGD"),
            LedgerAccount.Open(input.MerchantPayableAccountId, input.LedgerId, "SGD"),
            LedgerAccount.Open(input.PlatformRevenueAccountId, input.LedgerId, "SGD"));
        await db.SaveChangesAsync();
    }

    private sealed class WorkerApiFactory(string payments, string ledger) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                ReplaceContext<PaymentsDbContext>(services, payments);
                ReplaceContext<LedgerDbContext>(services, ledger);
            });
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Payments"] = payments,
                    ["ConnectionStrings:Ledger"] = ledger,
                    ["PaymentLedgerPostingWorker:Enabled"] = "true",
                    ["PaymentLedgerPostingWorker:BatchSize"] = "10",
                    ["PaymentLedgerPostingWorker:PollIntervalMilliseconds"] = "50",
                    ["PaymentLedgerPostingWorker:RetryDelaySeconds"] = "1"
                }));
        }
        private static void ReplaceContext<TContext>(IServiceCollection services, string connectionString)
            where TContext : DbContext
        {
            services.RemoveAll<IDbContextOptionsConfiguration<TContext>>();
            services.RemoveAll<DbContextOptions<TContext>>();
            services.RemoveAll<TContext>();
            services.AddDbContext<TContext>(options => options.UseNpgsql(connectionString));
        }
    }
}
