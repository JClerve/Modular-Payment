using SettleCore.Modules.Ledger.Application.PostPaymentLedgerTransaction;
using SettleCore.Modules.Payments.Application.Abstractions;

namespace SettleCore.Modules.Payments.Infrastructure.Integrations.Ledger;

public sealed class PaymentLedgerPostingAdapter(
    PostPaymentLedgerTransactionHandler handler)
    : IPaymentLedgerPostingPort
{
    public async Task PostAsync(
        PaymentLedgerPostingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var command = new PostPaymentLedgerTransactionCommand(
            TransactionId: request.TransactionId,
            LedgerId: request.LedgerId,
            ProcessorReceivableAccountId:
                request.ProcessorReceivableAccountId,
            MerchantPayableAccountId:
                request.MerchantPayableAccountId,
            PlatformRevenueAccountId:
                request.PlatformRevenueAccountId,
            Currency: request.Currency,
            GrossAmountMinorUnits:
                request.GrossAmountMinorUnits,
            FeeAmountMinorUnits:
                request.FeeAmountMinorUnits);

        await handler.HandleAsync(
            command,
            cancellationToken);
    }
}