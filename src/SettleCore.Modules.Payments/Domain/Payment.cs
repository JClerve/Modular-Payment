namespace SettleCore.Modules.Payments.Domain;

public sealed class Payment
{
    private ProviderPaymentReference? _providerReference;

    private Payment(
        PaymentId id,
        decimal amount,
        string currency,
        PaymentStatus status)
    {
        Id = id;
        Amount = amount;
        Currency = currency;
        Status = status;
    }

    public PaymentId Id { get; }

    public decimal Amount { get; }

    public string Currency { get; }

    public PaymentStatus Status { get; private set; }

    public ProviderPaymentReference? ProviderReference =>
        _providerReference;

    public static Payment Create(
        decimal amount,
        string currency)
    {
        ValidateAmount(amount);

        var normalizedCurrency =
            ValidateAndNormalizeCurrency(currency);

        return new Payment(
            PaymentId.New(),
            amount,
            normalizedCurrency,
            PaymentStatus.Pending);
    }

    public static Payment Rehydrate(
        PaymentId id,
        decimal amount,
        string currency,
        PaymentStatus status)
    {
        if (id == PaymentId.Empty)
        {
            throw new ArgumentException(
                "Payment ID must not be empty.",
                nameof(id));
        }

        ValidateAmount(amount);

        var normalizedCurrency =
            ValidateAndNormalizeCurrency(currency);

        return new Payment(
            id,
            amount,
            normalizedCurrency,
            status);
    }

    public void MarkSucceeded()
    {
        if (Status == PaymentStatus.Succeeded)
        {
            throw new InvalidOperationException(
                "Payment is already succeeded.");
        }

        Status = PaymentStatus.Succeeded;
    }

    public void AttachProviderReference(
        ProviderPaymentReference providerReference)
    {
        _providerReference = providerReference;
    }

    private static void ValidateAmount(
        decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(amount),
                amount,
                "Payment amount must be greater than zero.");
        }
    }

    private static string ValidateAndNormalizeCurrency(
        string currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException(
                "Payment currency must not be blank.",
                nameof(currency));
        }

        if (currency.Length != 3 ||
            !currency.All(char.IsLetter))
        {
            throw new ArgumentException(
                "Payment currency must be a three-letter alphabetic code.",
                nameof(currency));
        }

        return currency.ToUpperInvariant();
    }
}
