namespace SettleCore.Modules.Payments.Domain;

public readonly record struct ProviderPaymentReference
{
    private ProviderPaymentReference(
        string provider,
        string reference)
    {
        Provider = provider;
        Reference = reference;
    }

    public string Provider { get; }

    public string Reference { get; }

    public static ProviderPaymentReference Create(
        string provider,
        string reference)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(reference);

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new ArgumentException(
                "Payment provider must not be blank.",
                nameof(provider));
        }

        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new ArgumentException(
                "Provider payment reference must not be blank.",
                nameof(reference));
        }

        return new ProviderPaymentReference(
            provider.Trim(),
            reference.Trim());
    }
}