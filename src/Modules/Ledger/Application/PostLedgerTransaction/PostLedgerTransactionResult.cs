namespace Demopay.Modules.Ledger.Application.PostLedgerTransaction;

public sealed record PostLedgerTransactionResult(
    Guid TransactionId,
    Guid LedgerId,
    int EntryCount);