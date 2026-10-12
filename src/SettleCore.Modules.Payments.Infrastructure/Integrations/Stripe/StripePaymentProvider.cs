using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;
using global::Stripe;

namespace SettleCore.Modules.Payments.Infrastructure.Integrations.Stripe;

public sealed class StripePaymentProvider(StripeClient client) : IPaymentProvider
{
    public async Task<CreateProviderPaymentResult> CreatePaymentAsync(
        CreateProviderPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var intent = await client.V1.PaymentIntents.CreateAsync(
            new PaymentIntentCreateOptions
            {
                Amount = request.AmountMinorUnits,
                Currency = request.Currency.ToLowerInvariant(),
                Metadata = new Dictionary<string, string>
                {
                    ["settlecore_payment_id"] = request.PaymentId.Value.ToString("D")
                }
            },
            new RequestOptions { IdempotencyKey = request.IdempotencyKey },
            cancellationToken);

        return new CreateProviderPaymentResult(
            ProviderPaymentReference.Create("stripe", intent.Id),
            NormalizeStatus(intent.Status),
            intent.ClientSecret);
    }

    internal static ProviderPaymentStatus NormalizeStatus(string status) => status switch
    {
        "requires_payment_method" or "requires_confirmation" => ProviderPaymentStatus.Pending,
        "requires_action" => ProviderPaymentStatus.RequiresAction,
        "processing" => ProviderPaymentStatus.Processing,
        "requires_capture" => ProviderPaymentStatus.RequiresCapture,
        "succeeded" => ProviderPaymentStatus.Succeeded,
        "canceled" => ProviderPaymentStatus.Canceled,
        _ => throw new InvalidOperationException("Unrecognized Stripe PaymentIntent status.")
    };
}
