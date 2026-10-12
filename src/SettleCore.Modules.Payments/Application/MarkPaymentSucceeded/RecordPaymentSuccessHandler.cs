using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;

public sealed class RecordPaymentSuccessHandler(
    IPaymentRepository repository,
    IPaymentSuccessPersistence persistence)
{
    public async Task<MarkPaymentSucceededResult?> HandleAsync(
        RecordPaymentSuccessCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var payment = await repository.GetByIdAsync(
            PaymentId.From(command.PaymentId), cancellationToken);
        if (payment is null)
        {
            return null;
        }

        if (payment.ProviderReference is not null)
        {
            throw new InvalidOperationException("Provider-linked payments require verified provider success evidence.");
        }

        var request = PaymentLedgerPostingRequestFactory.Create(payment, command.PostingInput);
        var intent = PaymentLedgerPostingIntent.Create(payment.Id,
            request.TransactionId, request.LedgerId,
            request.ProcessorReceivableAccountId, request.MerchantPayableAccountId,
            request.PlatformRevenueAccountId, request.Currency,
            request.GrossAmountMinorUnits, request.FeeAmountMinorUnits);
        if (payment.Status != PaymentStatus.Succeeded)
        {
            payment.MarkSucceeded();
        }

        await persistence.SaveAsync(payment, intent, cancellationToken);
        return new MarkPaymentSucceededResult(payment.Id.Value,
            payment.Amount, payment.Currency, payment.Status.ToString());
    }
}
