namespace SettleCore.Modules.Ledger.Application.PostPaymentLedgerTransaction;

public sealed record PostPaymentLedgerTransactionResult(
    Guid TransactionId,
    Guid LedgerId,
    int EntryCount);