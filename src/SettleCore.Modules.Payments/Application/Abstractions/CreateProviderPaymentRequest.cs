using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.Abstractions;

public sealed record CreateProviderPaymentRequest
{
    public CreateProviderPaymentRequest(
        PaymentId paymentId,
        long amountMinorUnits,
        string currency)
    {
        if (paymentId == PaymentId.Empty)
        {
            throw new ArgumentException("Payment ID must not be empty.", nameof(paymentId));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amountMinorUnits);
        ArgumentNullException.ThrowIfNull(currency);
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetter))
        {
            throw new ArgumentException("Currency must be a three-letter alphabetic code.", nameof(currency));
        }

        PaymentId = paymentId;
        AmountMinorUnits = amountMinorUnits;
        Currency = currency.ToUpperInvariant();
    }

    public PaymentId PaymentId { get; }
    public long AmountMinorUnits { get; }
    public string Currency { get; }
    public string IdempotencyKey => $"settlecore:payment:{PaymentId.Value:N}:create";
}
