using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Application.MarkPaymentSucceeded;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.PreparePaymentLedgerPosting;

public sealed class PreparePaymentLedgerPostingHandler(
    IPaymentRepository payments, IPaymentLedgerPostingPreparationRepository preparations)
{
    public async Task<PaymentLedgerPostingRequest?> HandleAsync(PreparePaymentLedgerPostingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var payment = await payments.GetByIdAsync(PaymentId.From(command.PaymentId), cancellationToken);
        if (payment is null) return null;
        var request = PaymentLedgerPostingRequestFactory.Create(payment, command.PostingInput);
        await preparations.SaveAsync(request, cancellationToken);
        return request;
    }
}
