using SettleCore.Modules.Ledger.Domain;

namespace SettleCore.Modules.Ledger.Application.PostPaymentLedgerTransaction;

public sealed class PostPaymentLedgerTransactionHandler(
    ILedgerRepository repository)
{
    public async Task<PostPaymentLedgerTransactionResult> HandleAsync(
        PostPaymentLedgerTransactionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var entries = PaymentLedgerPosting.Create(
            command.ProcessorReceivableAccountId,
            command.MerchantPayableAccountId,
            command.PlatformRevenueAccountId,
            command.Currency,
            command.GrossAmountMinorUnits,
            command.FeeAmountMinorUnits);

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

        return new PostPaymentLedgerTransactionResult(
            transaction.Id,
            transaction.LedgerId,
            transaction.Entries.Count);
    }
}