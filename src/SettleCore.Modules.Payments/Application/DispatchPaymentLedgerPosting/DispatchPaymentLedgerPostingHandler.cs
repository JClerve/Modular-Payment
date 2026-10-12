using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.DispatchPaymentLedgerPosting;

public sealed class DispatchPaymentLedgerPostingHandler(
    IPaymentLedgerPostingIntentRepository repository,
    IPaymentLedgerPostingPort port)
{
    public async Task<bool> HandleAsync(
        DispatchPaymentLedgerPostingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var paymentId = PaymentId.From(command.PaymentId);
        var intent = await repository.GetByPaymentIdAsync(paymentId, cancellationToken);
        if (intent is null)
        {
            return false;
        }
        if (intent.Status == PaymentLedgerPostingIntentStatus.Posted)
        {
            return true;
        }

        var request = new PaymentLedgerPostingRequest(intent.PaymentId.Value,
            intent.TransactionId, intent.LedgerId, intent.ProcessorReceivableAccountId,
            intent.MerchantPayableAccountId, intent.PlatformRevenueAccountId,
            intent.Currency, intent.GrossAmountMinorUnits, intent.FeeAmountMinorUnits);
        await port.PostAsync(request, cancellationToken);
        if (!await repository.MarkPostedAsync(paymentId, cancellationToken))
        {
            throw new InvalidOperationException("Posting intent could not be acknowledged.");
        }
        return true;
    }
}
