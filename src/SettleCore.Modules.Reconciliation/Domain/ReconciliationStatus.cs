namespace SettleCore.Modules.Reconciliation.Domain;

public enum ReconciliationStatus
{
    Matched,
    AmountMismatch,
    CurrencyMismatch,
    Mismatch
}
