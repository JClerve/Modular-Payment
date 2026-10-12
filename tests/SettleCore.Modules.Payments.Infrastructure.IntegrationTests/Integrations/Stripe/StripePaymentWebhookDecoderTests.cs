using System.Security.Cryptography;
using System.Text;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;
using SettleCore.Modules.Payments.Infrastructure.Integrations.Stripe;
using Stripe;

namespace SettleCore.Modules.Payments.Infrastructure.IntegrationTests.Integrations.Stripe;

public sealed class StripePaymentWebhookDecoderTests
{
    private const string Secret = "whsec_test_placeholder";
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 6, 0, 0, TimeSpan.Zero);
    private static readonly Guid PaymentGuid = Guid.Parse("fbb35a13-d191-4057-a77e-c1bde5792b74");

    [Fact]
    public void ValidSignatureReturnsNeutralSuccessEvidence()
    {
        var payload = Payload();
        var decoder = CreateDecoder();
        var result = Assert.IsType<ProviderPaymentSucceededEvent>(decoder.Decode(payload, Sign(payload)));
        Assert.Equal("evt_test", result.EventId);
        Assert.Equal(PaymentId.From(PaymentGuid), result.PaymentId);
        Assert.Equal(ProviderPaymentReference.Create("stripe", "pi_test"), result.ProviderReference);
        Assert.Equal(1234, result.AmountMinorUnits);
        Assert.Equal("SGD", result.Currency);
        Assert.Equal(Now.AddMinutes(-1), result.OccurredAt);
        Assert.False(result.IsLiveMode);
    }

    [Fact]
    public void ChangedBodyIsRejected()
    {
        var payload = Payload();
        Assert.Throws<InvalidProviderWebhookException>(() => CreateDecoder().Decode(payload.Replace("1234", "9999", StringComparison.Ordinal), Sign(payload)));
    }

    [Theory]
    [InlineData(-301)]
    [InlineData(301)]
    public void SignatureOutsideExplicitToleranceIsRejected(int seconds)
    {
        var payload = Payload();
        Assert.Throws<InvalidProviderWebhookException>(() => CreateDecoder().Decode(payload, Sign(payload, Now.AddSeconds(seconds))));
    }

    [Fact]
    public void WrongSecretIsRejected()
    {
        var payload = Payload();
        Assert.Throws<InvalidProviderWebhookException>(() => CreateDecoder().Decode(payload, Sign(payload, secret: "whsec_wrong")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("t=abc,v1=invalid")]
    public void InvalidSignatureHeaderIsRejected(string signature)
    {
        Assert.Throws<InvalidProviderWebhookException>(() => CreateDecoder().Decode(Payload(), signature));
    }

    [Fact]
    public void UnrelatedSignedEventIsIgnoredAfterVerification()
    {
        var payload = Payload(type: "payment_intent.processing", status: "processing");
        Assert.Null(CreateDecoder().Decode(payload, Sign(payload)));
        Assert.Throws<InvalidProviderWebhookException>(() => CreateDecoder().Decode(payload, Sign(payload, secret: "whsec_wrong")));
    }

    [Theory]
    [InlineData("requires_payment_method", "fbb35a13-d191-4057-a77e-c1bde5792b74", 1234)]
    [InlineData("succeeded", "", 1234)]
    [InlineData("succeeded", "00000000-0000-0000-0000-000000000000", 1234)]
    [InlineData("succeeded", "fbb35a13-d191-4057-a77e-c1bde5792b74", 1200)]
    public void InconsistentSuccessEvidenceIsRejected(string status, string paymentId, long received)
    {
        var payload = Payload(status: status, paymentId: paymentId, received: received);
        Assert.Throws<InvalidProviderWebhookException>(() => CreateDecoder().Decode(payload, Sign(payload)));
    }

    [Fact]
    public void IncompatibleApiVersionIsRejected()
    {
        var payload = Payload(apiVersion: "2020-08-27");
        Assert.Throws<InvalidProviderWebhookException>(() => CreateDecoder().Decode(payload, Sign(payload)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void DisabledReplayToleranceIsRejected(long tolerance)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StripePaymentWebhookDecoder(Secret, tolerance, new FixedClock()));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("not-json")]
    [InlineData("null")]
    public void MalformedSignedPayloadIsRejected(string payload)
    {
        Assert.Throws<InvalidProviderWebhookException>(() => CreateDecoder().Decode(payload, Sign(payload)));
    }

    private static StripePaymentWebhookDecoder CreateDecoder() => new(Secret, 300, new FixedClock());

    private static string Payload(string type = "payment_intent.succeeded", string status = "succeeded", string? paymentId = null, long received = 1234, string? apiVersion = null) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            id = "evt_test", @object = "event", api_version = apiVersion ?? StripeConfiguration.ApiVersion,
            created = Now.AddMinutes(-1).ToUnixTimeSeconds(), livemode = false, type,
            data = new { @object = new
            {
                id = "pi_test", @object = "payment_intent", amount = 1234, amount_received = received,
                currency = "sgd", status,
                metadata = new { settlecore_payment_id = paymentId ?? PaymentGuid.ToString("D") }
            }}
        });

    private static string Sign(string payload, DateTimeOffset? time = null, string secret = Secret)
    {
        var timestamp = (time ?? Now).ToUnixTimeSeconds();
        var bytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return $"t={timestamp},v1={Convert.ToHexString(bytes).ToLowerInvariant()}";
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
