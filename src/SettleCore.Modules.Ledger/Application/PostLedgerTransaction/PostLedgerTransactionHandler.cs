using SettleCore.Modules.Ledger.Domain;

namespace SettleCore.Modules.Ledger.Application.PostLedgerTransaction;

public sealed class PostLedgerTransactionHandler(
    ILedgerRepository repository)
{
    public async Task<PostLedgerTransactionResult> HandleAsync(
        PostLedgerTransactionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var entries = command.Entries
            .Select(entry =>
                LedgerEntry.Create(
                    entry.AccountId,
                    entry.Currency,
                    entry.Direction,
                    entry.AmountMinorUnits))
            .ToArray();

        var accountIds = entries
            .Select(entry => entry.AccountId)
            .Distinct()
            .ToArray();

        var accounts =
            await repository.GetAccountsByIdsAsync(
                accountIds,
                cancellationToken);

        var transaction = LedgerTransaction.Post(
            command.TransactionId,
            command.LedgerId,
            entries,
            accounts);

        await repository.AddTransactionAsync(
            transaction,
            cancellationToken);

        return new PostLedgerTransactionResult(
            transaction.Id,
            transaction.LedgerId,
            transaction.Entries.Count);
    }
}