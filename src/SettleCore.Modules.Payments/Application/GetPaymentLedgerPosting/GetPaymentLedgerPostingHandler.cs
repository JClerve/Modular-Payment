using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.Application.GetPaymentLedgerPosting;

public sealed class GetPaymentLedgerPostingHandler(IPaymentLedgerPostingIntentRepository repository)
{
    public async Task<GetPaymentLedgerPostingResult?> HandleAsync(
        GetPaymentLedgerPostingQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var intent = await repository.GetByPaymentIdAsync(PaymentId.From(query.PaymentId), cancellationToken);
        return intent is null ? null : new GetPaymentLedgerPostingResult(
            intent.PaymentId.Value, intent.TransactionId, intent.Status.ToString(), intent.NextAttemptAt, intent.PostedAt);
    }
}
