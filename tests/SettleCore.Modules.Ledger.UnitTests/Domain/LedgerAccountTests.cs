using SettleCore.Modules.Ledger.Domain;

namespace SettleCore.Modules.Ledger.UnitTests.Domain;

public sealed class LedgerAccountTests
{
    [Fact]
    public void OpenRecordsLedgerAndNormalizesCurrency()
    {
        var accountId = Guid.NewGuid();
        var ledgerId = Guid.NewGuid();

        var account = LedgerAccount.Open(accountId, ledgerId, "sgd");

        Assert.Equal(accountId, account.Id);
        Assert.Equal(ledgerId, account.LedgerId);
        Assert.Equal("SGD", account.Currency);
    }

    [Fact]
    public void OpenRejectsEmptyLedgerId()
    {
        Assert.Throws<ArgumentException>(() =>
            LedgerAccount.Open(Guid.NewGuid(), Guid.Empty, "SGD"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("SG")]
    [InlineData("123")]
    public void OpenRejectsInvalidCurrency(string currency)
    {
        Assert.Throws<ArgumentException>(() =>
            LedgerAccount.Open(Guid.NewGuid(), Guid.NewGuid(), currency));
    }
}
