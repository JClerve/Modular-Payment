using SettleCore.Modules.Ledger.Domain;

namespace SettleCore.Modules.Ledger.UnitTests.Domain;

public sealed class PaymentLedgerPostingTests
{
    [Fact]
    public void CreateBuildsBalancedPaymentEntries()
    {
        var processorReceivableAccountId = Guid.NewGuid();
        var merchantPayableAccountId = Guid.NewGuid();
        var platformRevenueAccountId = Guid.NewGuid();

        var entries = PaymentLedgerPosting.Create(
            processorReceivableAccountId,
            merchantPayableAccountId,
            platformRevenueAccountId,
            "SGD",
            grossAmountMinorUnits: 10_000,
            feeAmountMinorUnits: 300);

        Assert.Equal(3, entries.Count);

        var processorReceivable = Assert.Single(
            entries,
            entry =>
                entry.AccountId ==
                processorReceivableAccountId);

        Assert.Equal(
            LedgerDirection.Debit,
            processorReceivable.Direction);

        Assert.Equal(
            10_000,
            processorReceivable.AmountMinorUnits);

        var merchantPayable = Assert.Single(
            entries,
            entry =>
                entry.AccountId ==
                merchantPayableAccountId);

        Assert.Equal(
            LedgerDirection.Credit,
            merchantPayable.Direction);

        Assert.Equal(
            9_700,
            merchantPayable.AmountMinorUnits);

        var platformRevenue = Assert.Single(
            entries,
            entry =>
                entry.AccountId ==
                platformRevenueAccountId);

        Assert.Equal(
            LedgerDirection.Credit,
            platformRevenue.Direction);

        Assert.Equal(
            300,
            platformRevenue.AmountMinorUnits);

        Assert.All(
            entries,
            entry => Assert.Equal("SGD", entry.Currency));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateWithNonPositiveFeeThrows(
        long feeAmountMinorUnits)
    {
        var exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PaymentLedgerPosting.Create(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "SGD",
                    grossAmountMinorUnits: 10_000,
                    feeAmountMinorUnits));

        Assert.Equal(
            "feeAmountMinorUnits",
            exception.ParamName);
    }

    [Theory]
    [InlineData(10_000)]
    [InlineData(10_001)]
    public void CreateWithFeeNotLessThanGrossThrows(
        long feeAmountMinorUnits)
    {
        var exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PaymentLedgerPosting.Create(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "SGD",
                    grossAmountMinorUnits: 10_000,
                    feeAmountMinorUnits));

        Assert.Equal(
            "feeAmountMinorUnits",
            exception.ParamName);
    }

    [Theory]
    [InlineData(0, "processorReceivableAccountId")]
    [InlineData(1, "merchantPayableAccountId")]
    [InlineData(2, "platformRevenueAccountId")]
    public void CreateWithEmptyAccountIdThrowsForAccountingRole(
        int accountPosition,
        string expectedParameterName)
    {
        var processorReceivableAccountId = Guid.NewGuid();
        var merchantPayableAccountId = Guid.NewGuid();
        var platformRevenueAccountId = Guid.NewGuid();

        switch (accountPosition)
        {
            case 0:
                processorReceivableAccountId = Guid.Empty;
                break;

            case 1:
                merchantPayableAccountId = Guid.Empty;
                break;

            case 2:
                platformRevenueAccountId = Guid.Empty;
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(accountPosition));
        }

        var exception =
            Assert.Throws<ArgumentException>(
                () => PaymentLedgerPosting.Create(
                    processorReceivableAccountId,
                    merchantPayableAccountId,
                    platformRevenueAccountId,
                    "SGD",
                    grossAmountMinorUnits: 10_000,
                    feeAmountMinorUnits: 300));

        Assert.Equal(
            expectedParameterName,
            exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateWithNonPositiveGrossAmountThrows(
        long grossAmountMinorUnits)
    {
        var exception =
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PaymentLedgerPosting.Create(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "SGD",
                    grossAmountMinorUnits,
                    feeAmountMinorUnits: 1));

        Assert.Equal(
            "grossAmountMinorUnits",
            exception.ParamName);
    }
}
