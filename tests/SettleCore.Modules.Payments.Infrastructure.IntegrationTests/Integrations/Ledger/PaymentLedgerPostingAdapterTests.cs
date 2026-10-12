using SettleCore.Modules.Ledger.Application;
using SettleCore.Modules.Ledger.Application.PostPaymentLedgerTransaction;
using SettleCore.Modules.Ledger.Domain;
using SettleCore.Modules.Payments.Application.Abstractions;
using SettleCore.Modules.Payments.Infrastructure.Integrations.Ledger;

namespace SettleCore.Modules.Payments.Infrastructure.IntegrationTests.Integrations.Ledger;

public sealed class PaymentLedgerPostingAdapterTests
{
    [Fact]
    public async Task PostTranslatesPaymentRequestIntoLedgerTransaction()
    {
        var ledgerId = Guid.NewGuid();

        var processorReceivableAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        var merchantPayableAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        var platformRevenueAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        var repository = new RecordingLedgerRepository(
            processorReceivableAccount,
            merchantPayableAccount,
            platformRevenueAccount);

        var ledgerHandler =
            new PostPaymentLedgerTransactionHandler(repository);

        var adapter =
            new PaymentLedgerPostingAdapter(ledgerHandler);

        var transactionId = Guid.NewGuid();

        var request = new PaymentLedgerPostingRequest(
            PaymentId: Guid.NewGuid(),
            TransactionId: transactionId,
            LedgerId: ledgerId,
            ProcessorReceivableAccountId:
                processorReceivableAccount.Id,
            MerchantPayableAccountId:
                merchantPayableAccount.Id,
            PlatformRevenueAccountId:
                platformRevenueAccount.Id,
            Currency: "SGD",
            GrossAmountMinorUnits: 10_000,
            FeeAmountMinorUnits: 300);

        await adapter.PostAsync(request);

        var persisted =
            Assert.IsType<LedgerTransaction>(
                repository.AddedTransaction);

        Assert.Equal(
            transactionId,
            persisted.Id);

        Assert.Equal(
            ledgerId,
            persisted.LedgerId);

        Assert.Equal(
            3,
            persisted.Entries.Count);

        Assert.Contains(
            persisted.Entries,
            entry =>
                entry.AccountId ==
                    processorReceivableAccount.Id &&
                entry.Direction ==
                    LedgerDirection.Debit &&
                entry.AmountMinorUnits == 10_000);

        Assert.Contains(
            persisted.Entries,
            entry =>
                entry.AccountId ==
                    merchantPayableAccount.Id &&
                entry.Direction ==
                    LedgerDirection.Credit &&
                entry.AmountMinorUnits == 9_700);

        Assert.Contains(
            persisted.Entries,
            entry =>
                entry.AccountId ==
                    platformRevenueAccount.Id &&
                entry.Direction ==
                    LedgerDirection.Credit &&
                entry.AmountMinorUnits == 300);
    }

    private sealed class RecordingLedgerRepository(
        params LedgerAccount[] accounts)
        : ILedgerRepository
    {
        private readonly IReadOnlyList<LedgerAccount> accounts =
            accounts;

        public LedgerTransaction? AddedTransaction { get; private set; }

        public Task<IReadOnlyList<LedgerAccount>>
            GetAccountsByIdsAsync(
                IReadOnlyCollection<Guid> accountIds,
                CancellationToken cancellationToken = default)
        {
            IReadOnlyList<LedgerAccount> matches =
                accounts
                    .Where(account =>
                        accountIds.Contains(account.Id))
                    .ToArray();

            return Task.FromResult(matches);
        }

        public Task<LedgerTransaction?> GetTransactionByIdAsync(
            Guid transactionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<LedgerTransaction?>(null);
        }

        public Task AddTransactionAsync(
            LedgerTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            AddedTransaction = transaction;

            return Task.CompletedTask;
        }
    }
}