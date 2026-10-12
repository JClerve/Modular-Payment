using SettleCore.Modules.Ledger.Application;
using SettleCore.Modules.Ledger.Application.GetLedgerTransactionById;
using SettleCore.Modules.Ledger.Domain;

namespace SettleCore.Modules.Ledger.UnitTests.Application.GetLedgerTransactionById;

public sealed class GetLedgerTransactionByIdHandlerTests
{
    [Fact]
    public async Task HandleReturnsTransactionWhenFound()
    {
        var transactionId = Guid.NewGuid();
        var ledgerId = Guid.NewGuid();

        var debitAccountId = Guid.NewGuid();
        var creditAccountId = Guid.NewGuid();

        var transaction = LedgerTransaction.Rehydrate(
            transactionId,
            ledgerId,
            [
                LedgerEntry.Create(
                    debitAccountId,
                    "SGD",
                    LedgerDirection.Debit,
                    1000),

                LedgerEntry.Create(
                    creditAccountId,
                    "SGD",
                    LedgerDirection.Credit,
                    1000)
            ]);

        var handler =
            new GetLedgerTransactionByIdHandler(
                new StubLedgerRepository(transaction));

        var result = await handler.HandleAsync(
            new GetLedgerTransactionByIdQuery(transactionId));

        var found =
            Assert.IsType<GetLedgerTransactionByIdResult>(result);

        Assert.Equal(transactionId, found.TransactionId);
        Assert.Equal(ledgerId, found.LedgerId);
        Assert.Equal(2, found.Entries.Count);

        Assert.Contains(
            found.Entries,
            entry =>
                entry.AccountId == debitAccountId &&
                entry.Currency == "SGD" &&
                entry.Direction == LedgerDirection.Debit &&
                entry.AmountMinorUnits == 1000);

        Assert.Contains(
            found.Entries,
            entry =>
                entry.AccountId == creditAccountId &&
                entry.Currency == "SGD" &&
                entry.Direction == LedgerDirection.Credit &&
                entry.AmountMinorUnits == 1000);
    }

    [Fact]
    public async Task HandleReturnsNullWhenTransactionIsMissing()
    {
        var handler =
            new GetLedgerTransactionByIdHandler(
                new StubLedgerRepository(null));

        var result = await handler.HandleAsync(
            new GetLedgerTransactionByIdQuery(
                Guid.NewGuid()));

        Assert.Null(result);
    }

    private sealed class StubLedgerRepository(
        LedgerTransaction? transaction)
        : ILedgerRepository
    {
        public Task<IReadOnlyList<LedgerAccount>>
            GetAccountsByIdsAsync(
                IReadOnlyCollection<Guid> accountIds,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<LedgerTransaction?> GetTransactionByIdAsync(
            Guid transactionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                transaction?.Id == transactionId
                    ? transaction
                    : null);
        }

        public Task AddTransactionAsync(
            LedgerTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}