using SettleCore.Modules.Reconciliation.Infrastructure.Persistence;
using SettleCore.Api.Security;
using System.Diagnostics;
using System.Security.Cryptography;
using SettleCore.Modules.Ledger.Domain;
using SettleCore.Modules.Ledger.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SettleCore.Modules.Payments.Application.CreatePayment;
using SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using SettleCore.Modules.Payments.Infrastructure.Persistence.Repositories;
using Testcontainers.PostgreSql;
using Stripe;

namespace SettleCore.IntegrationTests;

public sealed class CreateProviderPaymentEndpointTests
{
    [Fact]
    public async Task DisabledProviderCreationReturnsNotFoundWithoutResolvingStripe()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/payments/{Guid.NewGuid()}/provider-payment", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/payments/{Guid.NewGuid()}/provider-payment")).StatusCode);
    }

    [Fact]
    public async Task PreparedHttpPaymentCreatesIntentAndPersistsReferenceWithoutCompletingPayment()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await postgres.StartAsync();
        using var transport = new Transport();
        using var http = new HttpClient(transport);
        using var factory = new Factory(postgres.GetConnectionString(), http);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Database.MigrateAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/payments/{Guid.NewGuid()}/provider-payment", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/payments/{Guid.NewGuid()}/provider-payment")).StatusCode);
        var created = await client.PostAsJsonAsync("/payments", new { amount = 12.34m, currency = "SGD" });
        var payment = Assert.IsType<CreatePaymentResult>(await created.Content.ReadFromJsonAsync<CreatePaymentResult>());
        var path = $"/payments/{payment.PaymentId}/provider-payment";
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(path, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(0, transport.Calls);
        var input = new PaymentLedgerPostingInput(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 34);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/payments/{payment.PaymentId}/ledger-posting/preparation", input)).StatusCode);
        var response = await client.PostAsync(path, null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("test-client-secret", json.RootElement.GetProperty("clientSecret").GetString());
        Assert.Equal("pi_http_create", json.RootElement.GetProperty("providerReference").GetProperty("reference").GetString());
        Assert.Equal(1, transport.Calls);
        Assert.Equal($"settlecore:payment:{payment.PaymentId:N}:create", transport.IdempotencyKey);
        Assert.Equal("1234", transport.Fields["amount"]);
        Assert.Equal("sgd", transport.Fields["currency"]);
        Assert.Equal(payment.PaymentId.ToString("D"), transport.Fields["metadata[settlecore_payment_id]"]);
        Assert.False(transport.Fields.ContainsKey("application_fee_amount"));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(path, null)).StatusCode);
        Assert.Equal(1, transport.Calls);
        using var recovered = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.True(recovered.Headers.CacheControl?.NoStore);
        using var recoveredJson = JsonDocument.Parse(await recovered.Content.ReadAsStringAsync());
        Assert.Equal("test-client-secret", recoveredJson.RootElement.GetProperty("clientSecret").GetString());
        Assert.Equal(1, transport.Calls);
        Assert.Equal(1, transport.LookupCalls);
        transport.MismatchLookup = true;
        using var conflict = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.DoesNotContain("test-client-secret", await conflict.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using var final = factory.Services.CreateScope();
        var db = final.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var stored = await db.Payments.SingleAsync();
        Assert.Equal(PaymentStatus.Pending, stored.Status);
        Assert.Equal("pi_http_create", stored.ProviderReference!.Value.Reference);
        Assert.Single(await db.PaymentLedgerPostingPreparations.ToListAsync());
        Assert.Empty(await db.PaymentLedgerPostingIntents.ToListAsync());
        Assert.Empty(await db.PaymentLedgerPostingEvents.ToListAsync());
        Assert.Single(await db.PaymentProviderCreationAttempts.ToListAsync());
        var expiredResponse = await client.PostAsJsonAsync("/payments", new { amount = 12.34m, currency = "SGD" });
        var expired = Assert.IsType<CreatePaymentResult>(await expiredResponse.Content.ReadFromJsonAsync<CreatePaymentResult>());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/payments/{expired.PaymentId}/ledger-posting/preparation",
            input with { TransactionId = Guid.NewGuid() })).StatusCode);
        await new EfPaymentProviderCreationAttempts(db, new Clock(DateTimeOffset.UtcNow.AddDays(-2)))
            .GetOrRecordAsync(PaymentId.From(expired.PaymentId));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/payments/{expired.PaymentId}/provider-payment", null)).StatusCode);
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public async Task CreatedProviderPaymentCompletesOnlyFromSignedEvidenceAndPostsBalancedLedgerOnce()
    {
        await using var payments = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await using var ledger = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await using var reconciliation = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await Task.WhenAll(payments.StartAsync(), ledger.StartAsync(), reconciliation.StartAsync());
        using var transport = new Transport();
        using var http = new HttpClient(transport);
        using var factory = new Factory(payments.GetConnectionString(), http, ledger.GetConnectionString(), reconciliation.GetConnectionString());
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add(LocalApiAccess.Header, Factory.TestOperatorKey);
        using var publicClient = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.Unauthorized, (await publicClient.GetAsync($"/payments/{Guid.NewGuid()}")).StatusCode);
        var input = new PaymentLedgerPostingInput(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 34);
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Database.MigrateAsync();
            var ledgerDb = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
            await ledgerDb.Database.MigrateAsync();
            var reconciliationDb = scope.ServiceProvider.GetRequiredService<ReconciliationDbContext>();
            await reconciliationDb.Database.MigrateAsync();
            Assert.False(scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Database.HasPendingModelChanges());
            Assert.False(ledgerDb.Database.HasPendingModelChanges());
            Assert.False(reconciliationDb.Database.HasPendingModelChanges());
        }
        Assert.Equal(HttpStatusCode.OK, (await publicClient.GetAsync("/health/ready")).StatusCode);
        foreach (var accountId in new[] { input.ProcessorReceivableAccountId, input.MerchantPayableAccountId, input.PlatformRevenueAccountId })
        {
            var provisions = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ =>
                client.PostAsJsonAsync("/ledger/accounts", new { accountId, ledgerId = input.LedgerId, currency = "SGD" })));
            foreach (var provision in provisions) { Assert.Equal(HttpStatusCode.OK, provision.StatusCode); provision.Dispose(); }
        }
        var response = await client.PostAsJsonAsync("/payments", new { amount = 12.34m, currency = "SGD" });
        var payment = Assert.IsType<CreatePaymentResult>(await response.Content.ReadFromJsonAsync<CreatePaymentResult>());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/payments/{payment.PaymentId}/ledger-posting/preparation", input)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/payments/{payment.PaymentId}/provider-payment", null)).StatusCode);
        using (var beforeScope = factory.Services.CreateScope())
        {
            var before = beforeScope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            Assert.Equal(PaymentStatus.Pending, (await before.Payments.AsNoTracking().SingleAsync()).Status);
            Assert.Empty(await before.PaymentLedgerPostingIntents.ToListAsync());
        }
        var payload = JsonSerializer.Serialize(new
        {
            id = "evt_created_payment", @object = "event", api_version = StripeConfiguration.ApiVersion,
            created = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(), livemode = false,
            type = "payment_intent.succeeded", data = new { @object = new
            {
                id = "pi_http_create", @object = "payment_intent", amount = 1234, amount_received = 1234,
                currency = "sgd", status = "succeeded", metadata = new { settlecore_payment_id = payment.PaymentId.ToString("D") }
            }}
        });
        async Task<HttpStatusCode> SendWebhook(bool valid)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(valid ? "whsec_test_placeholder" : "wrong-secret"),
                Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
            using var request = new HttpRequestMessage(HttpMethod.Post, "/payments/webhooks/stripe")
            { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
            request.Headers.Add("Stripe-Signature", $"t={timestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}");
            using var result = await publicClient.SendAsync(request);
            return result.StatusCode;
        }
        Assert.Equal(HttpStatusCode.BadRequest, await SendWebhook(valid: false));
        using (var invalidScope = factory.Services.CreateScope())
        {
            var invalid = invalidScope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            Assert.Empty(await invalid.PaymentProviderEventReceipts.ToListAsync());
            Assert.Equal(PaymentStatus.Pending, (await invalid.Payments.AsNoTracking().SingleAsync()).Status);
        }
        Assert.Equal(HttpStatusCode.OK, await SendWebhook(valid: true));
        using var finalScope = factory.Services.CreateScope();
        var db = finalScope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(15))
        {
            if (await db.PaymentLedgerPostingIntents.AsNoTracking().AnyAsync(x => x.Status == PaymentLedgerPostingIntentStatus.Posted)) break;
            await Task.Delay(50);
        }
        Assert.Equal(PaymentStatus.Succeeded, (await db.Payments.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(PaymentLedgerPostingIntentStatus.Posted, (await db.PaymentLedgerPostingIntents.AsNoTracking().SingleAsync()).Status);
        Assert.NotNull((await db.PaymentProviderEventReceipts.AsNoTracking().SingleAsync()).ProcessedAt);
        Assert.Equal(HttpStatusCode.OK, await SendWebhook(valid: true));
        Assert.Single(await db.PaymentProviderEventReceipts.AsNoTracking().ToListAsync());
        var transaction = Assert.Single(await finalScope.ServiceProvider.GetRequiredService<LedgerDbContext>()
            .LedgerTransactions.AsNoTracking().Include(x => x.Entries).ToListAsync());
        Assert.Equal(input.TransactionId, transaction.Id);
        Assert.Equal(input.LedgerId, transaction.LedgerId);
        Assert.Equal(3, transaction.Entries.Count);
        Assert.Equal(1234, transaction.Entries.Where(x => x.Direction == LedgerDirection.Debit).Sum(x => x.AmountMinorUnits));
        Assert.Equal(1234, transaction.Entries.Where(x => x.Direction == LedgerDirection.Credit).Sum(x => x.AmountMinorUnits));
        Assert.Contains(transaction.Entries, x => x.AccountId == input.PlatformRevenueAccountId && x.AmountMinorUnits == 34);
        Assert.Equal(3, await finalScope.ServiceProvider.GetRequiredService<LedgerDbContext>().LedgerAccounts.CountAsync());
        using var paymentRead = await client.GetAsync($"/payments/{payment.PaymentId}");
        Assert.Equal(HttpStatusCode.OK, paymentRead.StatusCode);
        using var paymentJson = JsonDocument.Parse(await paymentRead.Content.ReadAsStringAsync());
        Assert.Equal("Succeeded", paymentJson.RootElement.GetProperty("status").GetString());
        using var postingRead = await client.GetAsync($"/payments/{payment.PaymentId}/ledger-posting");
        Assert.Equal(HttpStatusCode.OK, postingRead.StatusCode);
        using var transactionRead = await client.GetAsync($"/ledger/transactions/{input.TransactionId}");
        Assert.Equal(HttpStatusCode.OK, transactionRead.StatusCode);
        using var reconciliationResponse = await client.PostAsJsonAsync("/reconciliations", new
        { expectedAmount = 12.34m, expectedCurrency = "SGD", actualAmount = 12.34m, actualCurrency = "SGD" });
        Assert.Equal(HttpStatusCode.Created, reconciliationResponse.StatusCode);
    }

    [Fact]
    public async Task ExpiredOrphanRecoveryVerifiesOriginalIntentWithoutCreatingOrCompletingPayment()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await postgres.StartAsync();
        var transport = new Transport();
        using var http = new HttpClient(transport);
        using var factory = new Factory(postgres.GetConnectionString(), http);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Database.MigrateAsync();
        using var created = await client.PostAsJsonAsync("/payments", new { amount = 12.34m, currency = "SGD" });
        var payment = Assert.IsType<CreatePaymentResult>(await created.Content.ReadFromJsonAsync<CreatePaymentResult>());
        transport.LookupPaymentId = payment.PaymentId.ToString("D");
        var firstAttempt = DateTimeOffset.UtcNow.AddDays(-2);
        using (var seed = factory.Services.CreateScope())
            await new EfPaymentProviderCreationAttempts(seed.ServiceProvider.GetRequiredService<PaymentsDbContext>(), new Clock(firstAttempt))
                .GetOrRecordAsync(PaymentId.From(payment.PaymentId));
        var path = $"/payments/{payment.PaymentId}/provider-reference";
        transport.MismatchLookup = true;
        using var mismatch = await client.PostAsJsonAsync(path, new { provider = "stripe", reference = "pi_http_create" });
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        using (var verify = factory.Services.CreateScope())
            Assert.Null((await verify.ServiceProvider.GetRequiredService<PaymentsDbContext>().Payments.AsNoTracking().SingleAsync()).ProviderReference);
        transport.MismatchLookup = false;
        using var recovered = await client.PostAsJsonAsync(path, new { provider = "stripe", reference = "pi_http_create" });
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.DoesNotContain("test-client-secret", await recovered.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using var replay = await client.PostAsJsonAsync(path, new { provider = "stripe", reference = "pi_http_create" });
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(0, transport.Calls);
        Assert.Equal(3, transport.LookupCalls);
        using var final = factory.Services.CreateScope();
        var db = final.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var stored = await db.Payments.AsNoTracking().SingleAsync();
        Assert.Equal("pi_http_create", stored.ProviderReference!.Value.Reference);
        Assert.Equal(PaymentStatus.Pending, stored.Status);
        Assert.Empty(await db.PaymentLedgerPostingIntents.ToListAsync());
        Assert.Empty(await db.PaymentLedgerPostingEvents.ToListAsync());
        Assert.Equal(firstAttempt.ToUnixTimeSeconds(), (await db.PaymentProviderCreationAttempts.SingleAsync()).StartedAt.ToUnixTimeSeconds());
    }

    private sealed class Factory(string connection, HttpClient http, string? ledgerConnection = null, string? reconciliationConnection = null) : WebApplicationFactory<Program>
    {
        public const string TestOperatorKey = "acceptance-only-key-with-at-least-32-characters";
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LocalApiAccess:Enabled"] = (ledgerConnection is not null).ToString(),
                ["LocalApiAccess:OperatorKey"] = TestOperatorKey,
                ["Payments:Stripe:Enabled"] = "true", ["Payments:Stripe:ApiKey"] = "sk_test_placeholder",
                ["Payments:Stripe:WebhookSecret"] = "whsec_test_placeholder", ["Payments:Stripe:SignatureToleranceSeconds"] = "300",
                ["Payments:Stripe:IsLiveMode"] = "false",
                ["PaymentProviderEventWorker:Enabled"] = (ledgerConnection is not null).ToString(),
                ["PaymentProviderEventWorker:PollIntervalMilliseconds"] = "50",
                ["PaymentProviderEventWorker:RetryDelaySeconds"] = "1",
                ["PaymentLedgerPostingWorker:Enabled"] = (ledgerConnection is not null).ToString(),
                ["PaymentLedgerPostingWorker:PollIntervalMilliseconds"] = "50",
                ["PaymentLedgerPostingWorker:RetryDelaySeconds"] = "1"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<PaymentsDbContext>>();
                services.RemoveAll<DbContextOptions<PaymentsDbContext>>();
                services.RemoveAll<PaymentsDbContext>();
                services.AddDbContext<PaymentsDbContext>(options => options.UseNpgsql(connection));
                if (ledgerConnection is not null)
                {
                    services.RemoveAll<IDbContextOptionsConfiguration<LedgerDbContext>>();
                    services.RemoveAll<DbContextOptions<LedgerDbContext>>();
                    services.RemoveAll<LedgerDbContext>();
                    services.AddDbContext<LedgerDbContext>(options => options.UseNpgsql(ledgerConnection));
                }
                if (reconciliationConnection is not null)
                {
                    services.RemoveAll<IDbContextOptionsConfiguration<ReconciliationDbContext>>();
                    services.RemoveAll<DbContextOptions<ReconciliationDbContext>>();
                    services.RemoveAll<ReconciliationDbContext>();
                    services.AddDbContext<ReconciliationDbContext>(options => options.UseNpgsql(reconciliationConnection));
                }
                services.RemoveAll<StripeClient>();
                services.AddSingleton(new StripeClient("sk_test_placeholder", httpClient: new SystemNetHttpClient(http, maxNetworkRetries: 0)));
            });
        }
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Transport : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public int LookupCalls { get; private set; }
        public bool MismatchLookup { get; set; }
        public string? LookupPaymentId { get; set; }
        public string? IdempotencyKey { get; private set; }
        public Dictionary<string, string> Fields { get; private set; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                Calls++;
                IdempotencyKey = request.Headers.GetValues("Idempotency-Key").Single();
                var body = await request.Content!.ReadAsStringAsync(cancellationToken);
                Fields = body.Split('&').Select(pair => pair.Split('=', 2)).ToDictionary(
                    pair => WebUtility.UrlDecode(pair[0]), pair => WebUtility.UrlDecode(pair[1]));
            }
            else
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.EndsWith("/v1/payment_intents/pi_http_create", request.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
                LookupCalls++;
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    id = "pi_http_create", @object = "payment_intent", amount = MismatchLookup ? 1235 : 1234,
                    currency = "sgd", status = "succeeded", client_secret = "test-client-secret", livemode = false,
                    metadata = new { settlecore_payment_id = LookupPaymentId ?? Fields["metadata[settlecore_payment_id]"] }
                }), Encoding.UTF8, "application/json")
            };
        }
    }
}
