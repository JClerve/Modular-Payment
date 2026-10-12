namespace SettleCore.Modules.Payments.Infrastructure.Integrations.Stripe;

public sealed class StripePaymentOptions
{
    public bool Enabled { get; set; }
    public string? ApiKey { get; set; }
    public string? WebhookSecret { get; set; }
    public long SignatureToleranceSeconds { get; set; }
    public bool? IsLiveMode { get; set; }
}
