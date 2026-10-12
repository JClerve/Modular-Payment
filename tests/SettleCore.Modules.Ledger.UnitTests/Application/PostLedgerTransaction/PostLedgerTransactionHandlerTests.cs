using SettleCore.Modules.Ledger.Application;
using SettleCore.Modules.Ledger.Application.PostLedgerTransaction;
using SettleCore.Modules.Ledger.Domain;

namespace SettleCore.Modules.Ledger.UnitTests.Application.PostLedgerTransaction;

public sealed class PostLedgerTransactionHandlerTests
{
    [Fact]
    public async Task HandlePostsAndPersistsBalancedTransaction()
    {
        var ledgerId = Guid.NewGuid();

        var debitAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        var creditAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        var repository = new RecordingLedgerRepository(
            debitAccount,
            creditAccount);

        var handler =
            new PostLedgerTransactionHandler(repository);

        var transactionId = Guid.NewGuid();

        var command = new PostLedgerTransactionCommand(
            TransactionId: transactionId,
            LedgerId: ledgerId,
            Entries:
            [
                new PostLedgerTransactionEntry(
                    debitAccount.Id,
                    "SGD",
                    LedgerDirection.Debit,
                    1000),

                new PostLedgerTransactionEntry(
                    creditAccount.Id,
                    "SGD",
                    LedgerDirection.Credit,
                    1000)
            ]);

        var result = await handler.HandleAsync(command);

        Assert.Equal(transactionId, result.TransactionId);
        Assert.Equal(ledgerId, result.LedgerId);
        Assert.Equal(2, result.EntryCount);

        Assert.NotNull(repository.AddedTransaction);

        Assert.Equal(
            transactionId,
            repository.AddedTransaction.Id);

        Assert.Equal(
            ledgerId,
            repository.AddedTransaction.LedgerId);

        Assert.Equal(
            2,
            repository.AddedTransaction.Entries.Count);
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
    public async Task HandleRejectsMissingAccountWithoutPersisting()
    {
        var ledgerId = Guid.NewGuid();

        var debitAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        var missingAccountId = Guid.NewGuid();

        var repository = new RecordingLedgerRepository(
            debitAccount);

        var handler =
            new PostLedgerTransactionHandler(repository);

        var command = new PostLedgerTransactionCommand(
            TransactionId: Guid.NewGuid(),
            LedgerId: ledgerId,
            Entries:
            [
                new PostLedgerTransactionEntry(
                    debitAccount.Id,
                    "SGD",
                    LedgerDirection.Debit,
                    1000),

                new PostLedgerTransactionEntry(
                    missingAccountId,
                    "SGD",
                    LedgerDirection.Credit,
                    1000)
            ]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(command));

        Assert.Null(repository.AddedTransaction);
    }

    [Fact]
    public async Task HandleRejectsAccountOwnedByDifferentLedgerWithoutPersisting()
    {
        var ledgerId = Guid.NewGuid();
        var anotherLedgerId = Guid.NewGuid();

        var debitAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        var creditAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            anotherLedgerId,
            "SGD");

        var repository = new RecordingLedgerRepository(
            debitAccount,
            creditAccount);

        var handler =
            new PostLedgerTransactionHandler(repository);

        var command = new PostLedgerTransactionCommand(
            TransactionId: Guid.NewGuid(),
            LedgerId: ledgerId,
            Entries:
            [
                new PostLedgerTransactionEntry(
                    debitAccount.Id,
                    "SGD",
                    LedgerDirection.Debit,
                    1000),

                new PostLedgerTransactionEntry(
                    creditAccount.Id,
                    "SGD",
                    LedgerDirection.Credit,
                    1000)
            ]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(command));

        Assert.Null(repository.AddedTransaction);
    }

    [Fact]
    public async Task HandleRejectsEntryCurrencyMismatchWithoutPersisting()
    {
        var ledgerId = Guid.NewGuid();

        var debitAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        var creditAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        var repository = new RecordingLedgerRepository(
            debitAccount,
            creditAccount);

        var handler =
            new PostLedgerTransactionHandler(repository);

        var command = new PostLedgerTransactionCommand(
            TransactionId: Guid.NewGuid(),
            LedgerId: ledgerId,
            Entries:
            [
                new PostLedgerTransactionEntry(
                    debitAccount.Id,
                    "USD",
                    LedgerDirection.Debit,
                    1000),

                new PostLedgerTransactionEntry(
                    creditAccount.Id,
                    "SGD",
                    LedgerDirection.Credit,
                    1000)
            ]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(command));

        Assert.Null(repository.AddedTransaction);
    }

    [Fact]
    public async Task HandleRejectsUnbalancedTransactionWithoutPersisting()
    {
        var ledgerId = Guid.NewGuid();

        var debitAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        var creditAccount = LedgerAccount.Open(
            Guid.NewGuid(),
            ledgerId,
            "SGD");

        var repository = new RecordingLedgerRepository(
            debitAccount,
            creditAccount);

        var handler =
            new PostLedgerTransactionHandler(repository);

        var command = new PostLedgerTransactionCommand(
            TransactionId: Guid.NewGuid(),
            LedgerId: ledgerId,
            Entries:
            [
                new PostLedgerTransactionEntry(
                    debitAccount.Id,
                    "SGD",
                    LedgerDirection.Debit,
                    1000),

                new PostLedgerTransactionEntry(
                    creditAccount.Id,
                    "SGD",
                    LedgerDirection.Credit,
                    900)
            ]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => handler.HandleAsync(command));

        Assert.Null(repository.AddedTransaction);
    }
}
