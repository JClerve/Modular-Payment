using SettleCore.Modules.Ledger.Domain;

namespace SettleCore.Modules.Ledger.Application.PostLedgerTransaction;

public sealed record PostLedgerTransactionCommand(
    Guid TransactionId,
    Guid LedgerId,
    IReadOnlyList<PostLedgerTransactionEntry> Entries);

public sealed record PostLedgerTransactionEntry(
    Guid AccountId,
    string Currency,
    LedgerDirection Direction,
    long AmountMinorUnits);