using Demopay.Modules.Payments.Application.Abstractions;
using Demopay.Modules.Payments.Domain;

namespace Demopay.Modules.Payments.Application.Providers;

public static class ProviderPaymentSuccessCorrelation
{
    public static void Validate(Payment payment, ProviderPaymentSucceededEvent evidence, bool expectedLiveMode)
    {
        ArgumentNullException.ThrowIfNull(payment);
        ArgumentNullException.ThrowIfNull(evidence);
        if (payment.Id != evidence.PaymentId || payment.ProviderReference is null ||
            payment.ProviderReference != evidence.ProviderReference ||
            !string.Equals(payment.Currency, evidence.Currency, StringComparison.Ordinal) ||
            evidence.IsLiveMode != expectedLiveMode ||
            PaymentAmountConversion.ToMinorUnits(payment.Amount, payment.Currency) != evidence.AmountMinorUnits)
        {
            throw new ProviderPaymentCorrelationException();
        }
    }
}
