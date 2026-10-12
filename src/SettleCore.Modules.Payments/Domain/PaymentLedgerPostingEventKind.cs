namespace SettleCore.Modules.Payments.Domain;

public enum PaymentLedgerPostingEventKind
{
    IntentRecorded,
    RetryScheduled,
    PostingAcknowledged
}
