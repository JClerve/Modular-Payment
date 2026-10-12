namespace SettleCore.Modules.Payments.Application.GetPaymentLedgerPosting;

public sealed record GetPaymentLedgerPostingResult(
    Guid PaymentId,
    Guid TransactionId,
    string Status,
    DateTimeOffset? NextAttemptAt,
    DateTimeOffset? PostedAt);
