using Demopay.Modules.Ledger.Domain;

namespace Demopay.Modules.Ledger.Application.GetLedgerTransactionById;

public sealed record GetLedgerTransactionByIdResult(
    Guid TransactionId,
    Guid LedgerId,
    IReadOnlyList<GetLedgerTransactionByIdEntryResult> Entries);

public sealed record GetLedgerTransactionByIdEntryResult(
    Guid AccountId,
    string Currency,
    LedgerDirection Direction,
    long AmountMinorUnits);