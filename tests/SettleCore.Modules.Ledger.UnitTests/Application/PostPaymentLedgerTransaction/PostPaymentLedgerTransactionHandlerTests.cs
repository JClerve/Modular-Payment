using SettleCore.Modules.Ledger.Application;
using SettleCore.Modules.Ledger.Application.PostPaymentLedgerTransaction;
using SettleCore.Modules.Ledger.Domain;

namespace SettleCore.Modules.Ledger.UnitTests.Application.PostPaymentLedgerTransaction;

public sealed class PostPaymentLedgerTransactionHandlerTests
{
    [Fact]
    public async Task HandlePostsAndPersistsPaymentLedgerTransaction()
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

        var handler =
            new PostPaymentLedgerTransactionHandler(repository);

        var transactionId = Guid.NewGuid();

        var command = new PostPaymentLedgerTransactionCommand(
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

        var result = await handler.HandleAsync(command);

        Assert.Equal(
            transactionId,
            result.TransactionId);

        Assert.Equal(
            ledgerId,
            result.LedgerId);

        Assert.Equal(
            3,
            result.EntryCount);

        var persisted =
            Assert.IsType<LedgerTransaction>(
                repository.AddedTransaction);

        var processorReceivable =
            Assert.Single(
                persisted.Entries,
                entry =>
                    entry.AccountId ==
                    processorReceivableAccount.Id);

        Assert.Equal(
            LedgerDirection.Debit,
            processorReceivable.Direction);

        Assert.Equal(
            10_000,
            processorReceivable.AmountMinorUnits);

        var merchantPayable =
            Assert.Single(
                persisted.Entries,
                entry =>
                    entry.AccountId ==
                    merchantPayableAccount.Id);

        Assert.Equal(
            LedgerDirection.Credit,
            merchantPayable.Direction);

        Assert.Equal(
            9_700,
            merchantPayable.AmountMinorUnits);

        var platformRevenue =
            Assert.Single(
                persisted.Entries,
                entry =>
                    entry.AccountId ==
                    platformRevenueAccount.Id);

        Assert.Equal(
            LedgerDirection.Credit,
            platformRevenue.Direction);

        Assert.Equal(
            300,
            platformRevenue.AmountMinorUnits);
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
            return Task.FromResult(
                AddedTransaction?.Id == transactionId
                    ? AddedTransaction
                    : null);
        }

        public Task AddTransactionAsync(
            LedgerTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            AddedTransaction = transaction;

            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task HandleRejectsMissingAccountingAccountWithoutPersisting()
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

        var missingPlatformRevenueAccountId =
            Guid.NewGuid();

        var repository = new RecordingLedgerRepository(
            processorReceivableAccount,
            merchantPayableAccount);

        var handler =
            new PostPaymentLedgerTransactionHandler(repository);

        var command = new PostPaymentLedgerTransactionCommand(
            TransactionId: Guid.NewGuid(),
            LedgerId: ledgerId,
            ProcessorReceivableAccountId:
            processorReceivableAccount.Id,
            MerchantPayableAccountId:
            merchantPayableAccount.Id,
            PlatformRevenueAccountId:
            missingPlatformRevenueAccountId,
            Currency: "SGD",
            GrossAmountMinorUnits: 10_000,
            FeeAmountMinorUnits: 300);

        var exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => handler.HandleAsync(command));

        Assert.Equal(
            "accounts",
            exception.ParamName);

        Assert.Null(repository.AddedTransaction);
    }

    [Fact]
    public async Task HandleRejectsAccountingAccountOwnedByDifferentLedgerWithoutPersisting()
    {
        var ledgerId = Guid.NewGuid();
        var anotherLedgerId = Guid.NewGuid();

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
            anotherLedgerId,
            "SGD");

        var repository = new RecordingLedgerRepository(
            processorReceivableAccount,
            merchantPayableAccount,
            platformRevenueAccount);

        var handler =
            new PostPaymentLedgerTransactionHandler(repository);

        var command = new PostPaymentLedgerTransactionCommand(
            TransactionId: Guid.NewGuid(),
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

        var exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => handler.HandleAsync(command));

        Assert.Equal(
            "accounts",
            exception.ParamName);

        Assert.Null(repository.AddedTransaction);
    }

    [Fact]
    public async Task HandleRejectsAccountingAccountCurrencyMismatchWithoutPersisting()
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
            "USD");

        var repository = new RecordingLedgerRepository(
            processorReceivableAccount,
            merchantPayableAccount,
            platformRevenueAccount);

        var handler =
            new PostPaymentLedgerTransactionHandler(repository);

        var command = new PostPaymentLedgerTransactionCommand(
            TransactionId: Guid.NewGuid(),
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

        var exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () => handler.HandleAsync(command));

        Assert.Equal(
            "entries",
            exception.ParamName);

        Assert.Null(repository.AddedTransaction);
    }
}
