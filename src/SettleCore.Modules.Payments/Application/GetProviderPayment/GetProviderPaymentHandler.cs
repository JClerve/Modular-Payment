using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.GetProviderPayment;

public sealed class GetProviderPaymentHandler(IPaymentRepository payments, IPaymentProviderReader provider)
{
    public async Task<CreateProviderPaymentResult?> HandleAsync(Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var payment = await payments.GetByIdAsync(PaymentId.From(paymentId), cancellationToken);
        if (payment?.ProviderReference is not { } reference) return null;
        return await provider.GetPaymentAsync(new GetProviderPaymentRequest(payment.Id, reference,
            PaymentAmountConversion.ToMinorUnits(payment.Amount, payment.Currency), payment.Currency), cancellationToken);
    }
}
