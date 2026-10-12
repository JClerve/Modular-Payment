namespace SettleCore.Modules.Ledger.Application.GetLedgerTransactionById;

public sealed record GetLedgerTransactionByIdQuery(
    Guid TransactionId);