using System.Diagnostics;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Application.Abstractions;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SettleCore.Modules.Payments.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Stripe;

namespace SettleCore.IntegrationTests;

public sealed class StripeWebhookEndpointTests
{
    private const string Secret = "whsec_test_placeholder";
    private static readonly Guid PaymentId = Guid.NewGuid();

    [Fact]
    public async Task SignedSuccessIsDurableBeforeAcknowledgmentAndMatchingReplayIsIdempotent()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await postgres.StartAsync();
        using var factory = new Factory(postgres.GetConnectionString());
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        using (var scope = factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Database.MigrateAsync();
        var payload = Payload();
        Assert.Equal(HttpStatusCode.OK, (await Send(client, payload)).StatusCode);
        DateTimeOffset receivedAt;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            var receipt = await db.PaymentProviderEventReceipts.SingleAsync();
            Assert.Equal(PaymentId, receipt.PaymentId);
            Assert.Equal(1234, receipt.AmountMinorUnits);
            Assert.Null(receipt.ProcessedAt);
            Assert.Empty(await db.Payments.ToListAsync());
            Assert.Empty(await db.PaymentLedgerPostingIntents.ToListAsync());
            receivedAt = receipt.ReceivedAt;
        }
        Assert.Equal(HttpStatusCode.OK, (await Send(client, payload)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(client, Payload(amount: 1235))).StatusCode);
        using var finalScope = factory.Services.CreateScope();
        var stored = await finalScope.ServiceProvider.GetRequiredService<PaymentsDbContext>().PaymentProviderEventReceipts.SingleAsync();
        Assert.Equal(receivedAt, stored.ReceivedAt);
        Assert.Equal(1234, stored.AmountMinorUnits);
    }

    [Fact]
    public async Task EnabledWorkerCompletesCorrelatedSignedReceiptWithIntentAndAudit()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();
        await postgres.StartAsync();
        using var factory = new Factory(postgres.GetConnectionString(), workerEnabled: true);
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var payment = Payment.Rehydrate(SettleCore.Modules.Payments.Domain.PaymentId.From(StripeWebhookEndpointTests.PaymentId), 12.34m, "SGD", PaymentStatus.Pending);
        payment.AttachProviderReference(ProviderPaymentReference.Create("stripe", "pi_http"));
        var transactionId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
            await db.Database.MigrateAsync();
            db.Payments.Add(payment);
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<IPaymentLedgerPostingPreparationRepository>().SaveAsync(
                new PaymentLedgerPostingRequest(StripeWebhookEndpointTests.PaymentId, transactionId, Guid.NewGuid(),
                    Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SGD", 1234, 34));
        }
        Assert.Equal(HttpStatusCode.OK, (await Send(client, Payload())).StatusCode);
        using var readerScope = factory.Services.CreateScope();
        var reader = readerScope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(10))
        {
            if (await reader.PaymentProviderEventReceipts.AsNoTracking().AnyAsync(x => x.ProcessedAt != null)) break;
            await Task.Delay(50);
        }
        Assert.NotNull((await reader.PaymentProviderEventReceipts.AsNoTracking().SingleAsync()).ProcessedAt);
        Assert.Equal(PaymentStatus.Succeeded, (await reader.Payments.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(transactionId, (await reader.PaymentLedgerPostingIntents.AsNoTracking().SingleAsync()).TransactionId);
        Assert.Single(await reader.PaymentLedgerPostingEvents.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData(null)]
    public async Task InvalidOrMissingSignatureIsRejected(string? signature)
    {
        using var factory = new Factory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, Payload(), signature, sign: false)).StatusCode);
    }

    [Fact]
    public async Task WrongModeIsRejectedBeforeStorage()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(client, Payload(live: true))).StatusCode);
    }

    [Fact]
    public async Task AuthenticatedUnrelatedEventIsAcknowledgedWithoutStorage()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.OK, (await Send(client, Payload(type: "payment_intent.processing", status: "processing"))).StatusCode);
    }

    [Fact]
    public async Task StorageFailureIsNeverAcknowledged()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.InternalServerError, (await Send(client, Payload())).StatusCode);
    }

    [Fact]
    public async Task DisabledIngressReturnsNotFound()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        Assert.Equal(HttpStatusCode.NotFound, (await Send(client, Payload())).StatusCode);
    }

    private static string Payload(long amount = 1234, bool live = false,
        string type = "payment_intent.succeeded", string status = "succeeded") =>
        "  " + JsonSerializer.Serialize(new
        {
            id = "evt_http", @object = "event", api_version = StripeConfiguration.ApiVersion,
            created = DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds(), livemode = live, type,
            data = new { @object = new
            {
                id = "pi_http", @object = "payment_intent", amount, amount_received = amount,
                currency = "sgd", status, metadata = new { settlecore_payment_id = PaymentId.ToString("D") }
            }}
        }) + "\n";

    private static Task<HttpResponseMessage> Send(HttpClient client, string payload, string? signature = null, bool sign = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/payments/webhooks/stripe")
        { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        if (sign)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
            signature = $"t={timestamp},v1={Convert.ToHexString(hash).ToLowerInvariant()}";
        }
        if (signature is not null) request.Headers.TryAddWithoutValidation("Stripe-Signature", signature);
        return client.SendAsync(request);
    }

    private sealed class Factory(string? connection = null, bool workerEnabled = false) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Payments"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["ConnectionStrings:Ledger"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["ConnectionStrings:Reconciliation"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["Payments:Stripe:Enabled"] = "true", ["Payments:Stripe:ApiKey"] = "sk_test_placeholder",
                ["Payments:Stripe:WebhookSecret"] = Secret, ["Payments:Stripe:SignatureToleranceSeconds"] = "300",
                ["Payments:Stripe:IsLiveMode"] = "false",
                ["PaymentProviderEventWorker:Enabled"] = workerEnabled.ToString(),
                ["PaymentProviderEventWorker:BatchSize"] = "10",
                ["PaymentProviderEventWorker:PollIntervalMilliseconds"] = "50",
                ["PaymentProviderEventWorker:RetryDelaySeconds"] = "1"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<PaymentsDbContext>>();
                services.RemoveAll<DbContextOptions<PaymentsDbContext>>();
                services.RemoveAll<PaymentsDbContext>();
                services.AddDbContext<PaymentsDbContext>(options => options.UseNpgsql(connection ??
                    "Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Password=test;Timeout=1"));
            });
        }
    }
}
