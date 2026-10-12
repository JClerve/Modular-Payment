using System.Text.Json;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;
using global::Stripe;

namespace SettleCore.Modules.Payments.Infrastructure.Integrations.Stripe;

public sealed class StripePaymentWebhookDecoder : IPaymentProviderWebhookDecoder
{
    private readonly string _secret;
    private readonly long _toleranceSeconds;
    private readonly TimeProvider _clock;

    public StripePaymentWebhookDecoder(string secret, long toleranceSeconds, TimeProvider clock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(toleranceSeconds);
        ArgumentNullException.ThrowIfNull(clock);
        _secret = secret;
        _toleranceSeconds = toleranceSeconds;
        _clock = clock;
    }

    private static bool HasString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString());

    public ProviderPaymentSucceededEvent? Decode(string payload, string signatureHeader)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(signatureHeader);
        try
        {
            EventUtility.ValidateSignature(payload, signatureHeader, _secret, _toleranceSeconds,
                _clock.GetUtcNow().ToUnixTimeSeconds());
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !HasString(root, "api_version") || !HasString(root, "id") || !HasString(root, "type") ||
                !root.TryGetProperty("object", out var objectKind) || objectKind.ValueKind != JsonValueKind.String || objectKind.GetString() != "event" ||
                !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object ||
                !data.TryGetProperty("object", out var resource) || resource.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidProviderWebhookException();
            }

            var stripeEvent = EventUtility.ParseEvent(payload, throwOnApiVersionMismatch: true);
            if (stripeEvent.Type != "payment_intent.succeeded")
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(stripeEvent.Id) ||
                stripeEvent.Data?.Object is not PaymentIntent intent ||
                intent.Status != "succeeded" || intent.Amount <= 0 ||
                intent.AmountReceived != intent.Amount ||
                intent.Metadata is null ||
                !intent.Metadata.TryGetValue("settlecore_payment_id", out var identity) ||
                !Guid.TryParse(identity, out var paymentId) || paymentId == Guid.Empty)
            {
                throw new InvalidProviderWebhookException();
            }

            // Reuse neutral request validation for amount, identity and currency.
            var request = new CreateProviderPaymentRequest(PaymentId.From(paymentId), intent.Amount, intent.Currency);
            return new ProviderPaymentSucceededEvent(
                stripeEvent.Id, request.PaymentId,
                ProviderPaymentReference.Create("stripe", intent.Id),
                request.AmountMinorUnits, request.Currency,
                new DateTimeOffset(DateTime.SpecifyKind(stripeEvent.Created, DateTimeKind.Utc)),
                stripeEvent.Livemode);
        }
        catch (Exception exception) when (exception is StripeException or JsonException or ArgumentException or FormatException or OverflowException)
        {
            throw new InvalidProviderWebhookException();
        }
    }
}
