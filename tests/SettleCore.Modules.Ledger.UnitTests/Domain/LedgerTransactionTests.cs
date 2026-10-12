using SettleCore.Modules.Ledger.Domain;

namespace SettleCore.Modules.Ledger.UnitTests.Domain;

public sealed class LedgerTransactionTests
{
    [Fact]
    public void PostAcceptsBalancedEntriesInEachCurrency()
    {
        var ledgerId = Guid.NewGuid();

        var sgdDebitAccount =
            LedgerAccount.Open(Guid.NewGuid(), ledgerId, "SGD");

        var sgdCreditAccount =
            LedgerAccount.Open(Guid.NewGuid(), ledgerId, "SGD");

        var usdDebitAccount =
            LedgerAccount.Open(Guid.NewGuid(), ledgerId, "USD");

        var usdCreditAccount =
            LedgerAccount.Open(Guid.NewGuid(), ledgerId, "USD");

        var accounts = new[]
        {
            sgdDebitAccount,
            sgdCreditAccount,
            usdDebitAccount,
            usdCreditAccount
        };

        var entries = new[]
        {
            LedgerEntry.Create(
                sgdDebitAccount.Id,
                "sgd",
                LedgerDirection.Debit,
                1000),

            LedgerEntry.Create(
                sgdCreditAccount.Id,
                "SGD",
                LedgerDirection.Credit,
                1000),

            LedgerEntry.Create(
                usdDebitAccount.Id,
                "usd",
                LedgerDirection.Debit,
                250),

            LedgerEntry.Create(
                usdCreditAccount.Id,
                "USD",
                LedgerDirection.Credit,
                250)
        };

        var transaction = LedgerTransaction.Post(
            Guid.NewGuid(),
            ledgerId,
            entries,
            accounts);

        Assert.Equal(ledgerId, transaction.LedgerId);
        Assert.Equal(4, transaction.Entries.Count);

        Assert.All(
            transaction.Entries,
            entry =>
                Assert.Equal(
                    entry.Currency.ToUpperInvariant(),
                    entry.Currency));
    }

    [Fact]
    public void PostRejectsUnbalancedCurrency()
    {
        var ledgerId = Guid.NewGuid();

        var debitAccount =
            LedgerAccount.Open(Guid.NewGuid(), ledgerId, "SGD");

        var creditAccount =
            LedgerAccount.Open(Guid.NewGuid(), ledgerId, "SGD");

        var accounts = new[]
        {
            debitAccount,
            creditAccount
        };

        var entries = new[]
        {
            LedgerEntry.Create(
                debitAccount.Id,
                "SGD",
                LedgerDirection.Debit,
                1000),

            LedgerEntry.Create(
                creditAccount.Id,
                "SGD",
                LedgerDirection.Credit,
                900)
        };

        Assert.Throws<ArgumentException>(() =>
            LedgerTransaction.Post(
                Guid.NewGuid(),
                ledgerId,
                entries,
                accounts));
    }

    [Fact]
    public void PostRejectsCrossCurrencyOffsettingEntries()
    {
        var ledgerId = Guid.NewGuid();

        var sgdDebitAccount =
            LedgerAccount.Open(Guid.NewGuid(), ledgerId, "SGD");

        var sgdCreditAccount =
            LedgerAccount.Open(Guid.NewGuid(), ledgerId, "SGD");

        var usdDebitAccount =
            LedgerAccount.Open(Guid.NewGuid(), ledgerId, "USD");

        var usdCreditAccount =
            LedgerAccount.Open(Guid.NewGuid(), ledgerId, "USD");

        var accounts = new[]
        {
            sgdDebitAccount,
            sgdCreditAccount,
            usdDebitAccount,
            usdCreditAccount
        };

        var entries = new[]
        {
            LedgerEntry.Create(
                sgdDebitAccount.Id,
                "SGD",
                LedgerDirection.Debit,
                1000),

            LedgerEntry.Create(
                sgdCreditAccount.Id,
                "SGD",
                LedgerDirection.Credit,
                500),

            LedgerEntry.Create(
                usdDebitAccount.Id,
                "USD",
                LedgerDirection.Debit,
                500),

            LedgerEntry.Create(
                usdCreditAccount.Id,
                "USD",
                LedgerDirection.Credit,
                1000)
        };

        Assert.Throws<ArgumentException>(() =>
            LedgerTransaction.Post(
                Guid.NewGuid(),
                ledgerId,
                entries,
                accounts));
    }

    [Fact]
    public void PostRejectsSingleEntry()
    {
        var ledgerId = Guid.NewGuid();

        var account =
            LedgerAccount.Open(Guid.NewGuid(), ledgerId, "SGD");

        var entries = new[]
        {
            LedgerEntry.Create(
                account.Id,
                "SGD",
                LedgerDirection.Debit,
                1000)
        };

        Assert.Throws<ArgumentException>(() =>
            LedgerTransaction.Post(
                Guid.NewGuid(),
                ledgerId,
                entries,
                new[] { account }));
    }

    [Fact]
    public void PostRejectsAccountOwnedByDifferentLedger()
    {
        var ledgerId = Guid.NewGuid();
        var anotherLedgerId = Guid.NewGuid();

        var debitAccount =
            LedgerAccount.Open(
                Guid.NewGuid(),
                ledgerId,
                "SGD");

        var creditAccount =
            LedgerAccount.Open(
                Guid.NewGuid(),
                anotherLedgerId,
                "SGD");

        var entries = new[]
        {
            LedgerEntry.Create(
                debitAccount.Id,
                "SGD",
                LedgerDirection.Debit,
                1000),

            LedgerEntry.Create(
                creditAccount.Id,
                "SGD",
                LedgerDirection.Credit,
                1000)
        };

        Assert.Throws<ArgumentException>(() =>
            LedgerTransaction.Post(
                Guid.NewGuid(),
                ledgerId,
                entries,
                new[]
                {
                    debitAccount,
                    creditAccount
                }));
    }

    [Fact]
    public void PostRejectsCurrencyThatDoesNotMatchAccount()
    {
        var ledgerId = Guid.NewGuid();

        var debitAccount =
            LedgerAccount.Open(
                Guid.NewGuid(),
                ledgerId,
                "SGD");

        var creditAccount =
            LedgerAccount.Open(
                Guid.NewGuid(),
                ledgerId,
                "USD");

        var entries = new[]
        {
            LedgerEntry.Create(
                debitAccount.Id,
                "USD",
                LedgerDirection.Debit,
                1000),

            LedgerEntry.Create(
                creditAccount.Id,
                "USD",
                LedgerDirection.Credit,
                1000)
        };

        Assert.Throws<ArgumentException>(() =>
            LedgerTransaction.Post(
                Guid.NewGuid(),
                ledgerId,
                entries,
                new[]
                {
                    debitAccount,
                    creditAccount
                }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EntryRejectsNonPositiveAmount(long amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            LedgerEntry.Create(
                Guid.NewGuid(),
                "SGD",
                LedgerDirection.Debit,
                amount));
    }
}
