using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Integrations.Stripe;
using Stripe;

namespace SettleCore.Modules.Payments.Infrastructure.IntegrationTests.Integrations.Stripe;

public sealed class StripePaymentProviderReaderTests
{
    [Theory]
    [InlineData("match")]
    [InlineData("identity")]
    [InlineData("reference")]
    [InlineData("amount")]
    [InlineData("currency")]
    [InlineData("mode")]
    public async Task RetrievesStoredIntentAndRequiresMatchingNeutralEvidence(string mismatch)
    {
        var paymentId = PaymentId.New();
        using var transport = new Transport(paymentId, mismatch);
        using var http = new HttpClient(transport);
        var reader = new StripePaymentProviderReader(
            new StripeClient("sk_test_placeholder", httpClient: new SystemNetHttpClient(http, maxNetworkRetries: 0)),
            Options.Create(new StripePaymentOptions { Enabled = true, IsLiveMode = false }));
        var request = new GetProviderPaymentRequest(paymentId, ProviderPaymentReference.Create("stripe", "pi_stored"), 1234, "SGD");
        if (mismatch != "match")
            await Assert.ThrowsAsync<InvalidOperationException>(() => reader.GetPaymentAsync(request));
        else
        {
            var result = await reader.GetPaymentAsync(request);
            Assert.Equal(request.ProviderReference, result.ProviderReference);
            Assert.Equal(ProviderPaymentStatus.RequiresAction, result.Status);
            Assert.Equal("test-client-secret", result.ClientSecret);
        }
        Assert.Equal(HttpMethod.Get, transport.Method);
        Assert.Equal("https://api.stripe.com/v1/payment_intents/pi_stored", transport.Url);
    }

    [Fact]
    public async Task OtherProviderReferenceIsRejectedBeforeOutboundLookup()
    {
        using var transport = new Transport(PaymentId.New(), "match");
        using var http = new HttpClient(transport);
        var reader = new StripePaymentProviderReader(
            new StripeClient("sk_test_placeholder", httpClient: new SystemNetHttpClient(http, maxNetworkRetries: 0)),
            Options.Create(new StripePaymentOptions { Enabled = true, IsLiveMode = false }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.GetPaymentAsync(
            new GetProviderPaymentRequest(PaymentId.New(), ProviderPaymentReference.Create("other", "pi_stored"), 1234, "SGD")));
        Assert.Null(transport.Method);
    }

    private sealed class Transport(PaymentId paymentId, string mismatch) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? Url { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            Url = request.RequestUri?.AbsoluteUri;
            var payload = JsonSerializer.Serialize(new
            {
                id = mismatch == "reference" ? "pi_other" : "pi_stored", @object = "payment_intent",
                amount = mismatch == "amount" ? 1235 : 1234, currency = mismatch == "currency" ? "usd" : "sgd",
                livemode = mismatch == "mode", status = "requires_action", client_secret = "test-client-secret",
                metadata = new { settlecore_payment_id = (mismatch == "identity" ? Guid.NewGuid() : paymentId.Value).ToString("D") }
            });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(payload, Encoding.UTF8, "application/json") });
        }
    }
}
