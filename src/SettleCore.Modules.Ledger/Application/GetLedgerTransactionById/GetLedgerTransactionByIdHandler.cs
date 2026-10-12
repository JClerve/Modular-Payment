namespace SettleCore.Modules.Ledger.Application.GetLedgerTransactionById;

public sealed class GetLedgerTransactionByIdHandler(
    ILedgerRepository repository)
{
    public async Task<GetLedgerTransactionByIdResult?> HandleAsync(
        GetLedgerTransactionByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var transaction =
            await repository.GetTransactionByIdAsync(
                query.TransactionId,
                cancellationToken);

        if (transaction is null)
        {
            return null;
        }

        var entries = transaction.Entries
            .Select(entry =>
                new GetLedgerTransactionByIdEntryResult(
                    entry.AccountId,
                    entry.Currency,
                    entry.Direction,
                    entry.AmountMinorUnits))
            .ToArray();

        return new GetLedgerTransactionByIdResult(
            transaction.Id,
            transaction.LedgerId,
            entries);
    }
}