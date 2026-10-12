using System.Net;
using System.Text;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Integrations.Stripe;
using Stripe;

namespace SettleCore.Modules.Payments.Infrastructure.IntegrationTests.Integrations.Stripe;

public sealed class StripePaymentProviderTests
{
    [Fact]
    public async Task CreatesUnconfirmedIntentWithExplicitAmountAndStableIdentity()
    {
        using var transport = new StubTransport("requires_payment_method");
        using var httpClient = new HttpClient(transport);
        var stripe = new StripeClient("sk_test_placeholder", httpClient: new SystemNetHttpClient(httpClient, maxNetworkRetries: 0));
        var provider = new StripePaymentProvider(stripe);
        var payment = Payment.Create(12.34m, "SGD");
        var request = new CreateProviderPaymentRequest(payment.Id, 1234, payment.Currency);

        var result = await provider.CreatePaymentAsync(request);
        await provider.CreatePaymentAsync(request);

        Assert.Equal(2, transport.Calls);
        Assert.Equal(HttpMethod.Post, transport.Method);
        Assert.Equal("https://api.stripe.com/v1/payment_intents", transport.Url);
        Assert.Equal(request.IdempotencyKey, transport.IdempotencyKey);
        Assert.Equal("1234", transport.Fields["amount"]);
        Assert.Equal("sgd", transport.Fields["currency"]);
        Assert.Equal(payment.Id.Value.ToString("D"), transport.Fields["metadata[settlecore_payment_id]"]);
        Assert.False(transport.Fields.ContainsKey("confirm"));
        Assert.False(transport.Fields.ContainsKey("application_fee_amount"));
        Assert.DoesNotContain(transport.Fields.Keys, key => key.StartsWith("transfer_data", StringComparison.Ordinal));
        Assert.Equal("stripe", result.ProviderReference.Provider);
        Assert.Equal("pi_test", result.ProviderReference.Reference);
        Assert.Equal(ProviderPaymentStatus.Pending, result.Status);
        Assert.Equal("test-client-secret", result.ClientSecret);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Theory]
    [InlineData("requires_payment_method", ProviderPaymentStatus.Pending)]
    [InlineData("requires_confirmation", ProviderPaymentStatus.Pending)]
    [InlineData("requires_action", ProviderPaymentStatus.RequiresAction)]
    [InlineData("processing", ProviderPaymentStatus.Processing)]
    [InlineData("requires_capture", ProviderPaymentStatus.RequiresCapture)]
    [InlineData("succeeded", ProviderPaymentStatus.Succeeded)]
    [InlineData("canceled", ProviderPaymentStatus.Canceled)]
    public async Task NormalizesProviderStatus(string status, ProviderPaymentStatus expected)
    {
        using var transport = new StubTransport(status);
        using var httpClient = new HttpClient(transport);
        var provider = new StripePaymentProvider(new StripeClient("sk_test_placeholder", httpClient: new SystemNetHttpClient(httpClient, maxNetworkRetries: 0)));
        var result = await provider.CreatePaymentAsync(new CreateProviderPaymentRequest(PaymentId.New(), 1234, "SGD"));
        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task UnknownStatusIsRejectedInsteadOfAssumingCompletion()
    {
        using var transport = new StubTransport("new_unknown_status");
        using var httpClient = new HttpClient(transport);
        var provider = new StripePaymentProvider(new StripeClient("sk_test_placeholder", httpClient: new SystemNetHttpClient(httpClient, maxNetworkRetries: 0)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.CreatePaymentAsync(new CreateProviderPaymentRequest(PaymentId.New(), 1234, "SGD")));
    }

    private sealed class StubTransport(string status) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public HttpMethod? Method { get; private set; }
        public string? Url { get; private set; }
        public string? IdempotencyKey { get; private set; }
        public Dictionary<string, string> Fields { get; private set; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Method = request.Method;
            Url = request.RequestUri?.AbsoluteUri;
            IdempotencyKey = request.Headers.GetValues("Idempotency-Key").Single();
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Fields = body.Split('&').Select(pair => pair.Split('=', 2)).ToDictionary(
                pair => WebUtility.UrlDecode(pair[0]), pair => WebUtility.UrlDecode(pair[1]));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$"""{"id":"pi_test","object":"payment_intent","amount":1234,"currency":"sgd","status":"{{status}}","client_secret":"test-client-secret"}""", Encoding.UTF8, "application/json")
            };
        }
    }
}
