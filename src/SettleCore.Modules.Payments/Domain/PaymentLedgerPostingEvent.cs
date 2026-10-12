namespace SettleCore.Modules.Payments.Domain;

public sealed record PaymentLedgerPostingEvent(
    Guid Id,
    PaymentId PaymentId,
    Guid TransactionId,
    PaymentLedgerPostingEventKind Kind,
    DateTimeOffset OccurredAt,
    DateTimeOffset? NextAttemptAt);
