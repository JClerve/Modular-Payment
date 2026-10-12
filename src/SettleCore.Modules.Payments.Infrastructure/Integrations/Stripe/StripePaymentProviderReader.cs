using Microsoft.Extensions.Options;
using SettleCore.Modules.Payments.Application.Abstractions;
using global::Stripe;

namespace SettleCore.Modules.Payments.Infrastructure.Integrations.Stripe;

public sealed class StripePaymentProviderReader(StripeClient client, IOptions<StripePaymentOptions> options)
    : IPaymentProviderReader
{
    public async Task<CreateProviderPaymentResult> GetPaymentAsync(GetProviderPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = options.Value;
        if (!settings.Enabled || !settings.IsLiveMode.HasValue || request.ProviderReference.Provider != "stripe")
            throw new InvalidOperationException("Enabled Stripe lookup requires a Stripe reference and explicit live/test mode.");
        var intent = await client.V1.PaymentIntents.GetAsync(request.ProviderReference.Reference,
            cancellationToken: cancellationToken);
        if (intent.Id != request.ProviderReference.Reference || intent.Amount != request.AmountMinorUnits ||
            !string.Equals(intent.Currency, request.Currency, StringComparison.OrdinalIgnoreCase) ||
            intent.Livemode != settings.IsLiveMode.Value || intent.Metadata is null ||
            !intent.Metadata.TryGetValue("settlecore_payment_id", out var identity) ||
            !Guid.TryParse(identity, out var paymentId) || paymentId != request.PaymentId.Value)
            throw new InvalidOperationException("Retrieved provider payment does not match stored payment evidence.");
        return new CreateProviderPaymentResult(request.ProviderReference,
            StripePaymentProvider.NormalizeStatus(intent.Status), intent.ClientSecret);
    }
}
