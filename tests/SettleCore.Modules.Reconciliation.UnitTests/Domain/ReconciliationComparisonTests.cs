using SettleCore.Modules.Reconciliation.Domain;

namespace SettleCore.Modules.Reconciliation.UnitTests.Domain;

public sealed class ReconciliationComparisonTests
{
    [Fact]
    public void CompareReturnsMatchedWhenAmountAndCurrencyMatch()
    {
        var result = ReconciliationComparison.Compare(
            expectedAmount: 100.00m,
            expectedCurrency: "SGD",
            actualAmount: 100.00m,
            actualCurrency: "SGD");

        Assert.Equal(
            ReconciliationStatus.Matched,
            result.Status);
    }

    [Fact]
    public void CompareReturnsAmountMismatchWhenAmountsDiffer()
    {
        var result = ReconciliationComparison.Compare(
            expectedAmount: 100.00m,
            expectedCurrency: "SGD",
            actualAmount: 95.00m,
            actualCurrency: "SGD");

        Assert.Equal(
            ReconciliationStatus.AmountMismatch,
            result.Status);
    }

    [Fact]
    public void CompareReturnsCurrencyMismatchWhenCurrenciesDiffer()
    {
        var result = ReconciliationComparison.Compare(
            expectedAmount: 100.00m,
            expectedCurrency: "SGD",
            actualAmount: 100.00m,
            actualCurrency: "USD");

        Assert.Equal(
            ReconciliationStatus.CurrencyMismatch,
            result.Status);
    }

    [Fact]
    public void CompareReturnsMismatchWhenAmountAndCurrencyDiffer()
    {
        var result = ReconciliationComparison.Compare(
            expectedAmount: 100.00m,
            expectedCurrency: "SGD",
            actualAmount: 95.00m,
            actualCurrency: "USD");

        Assert.Equal(
            ReconciliationStatus.Mismatch,
            result.Status);
    }

    [Fact]
    public void CompareIncludesAmountDetailsWhenAmountsDiffer()
    {
        var result = ReconciliationComparison.Compare(
            expectedAmount: 100.00m,
            expectedCurrency: "SGD",
            actualAmount: 95.00m,
            actualCurrency: "SGD");

        Assert.Equal(
            100.00m,
            result.ExpectedAmount);

        Assert.Equal(
            95.00m,
            result.ActualAmount);
    }

    [Fact]
    public void CompareIncludesCurrencyDetailsWhenCurrenciesDiffer()
    {
        var result = ReconciliationComparison.Compare(
            expectedAmount: 100.00m,
            expectedCurrency: "SGD",
            actualAmount: 100.00m,
            actualCurrency: "USD");

        Assert.Equal(
            "SGD",
            result.ExpectedCurrency);

        Assert.Equal(
            "USD",
            result.ActualCurrency);
    }

    [Fact]
    public void CompareTreatsCurrencyCodesCaseInsensitively()
    {
        var result = ReconciliationComparison.Compare(
            expectedAmount: 100.00m,
            expectedCurrency: "sgd",
            actualAmount: 100.00m,
            actualCurrency: "SGD");

        Assert.Equal(
            ReconciliationStatus.Matched,
            result.Status);

        Assert.Equal(
            "SGD",
            result.ExpectedCurrency);

        Assert.Equal(
            "SGD",
            result.ActualCurrency);
    }

    [Fact]
    public void CompareRejectsBlankExpectedCurrency()
    {
        Assert.Throws<ArgumentException>(
            () => ReconciliationComparison.Compare(
                expectedAmount: 100.00m,
                expectedCurrency: " ",
                actualAmount: 100.00m,
                actualCurrency: "SGD"));
    }

    [Fact]
    public void CompareRejectsBlankActualCurrency()
    {
        Assert.Throws<ArgumentException>(
            () => ReconciliationComparison.Compare(
                expectedAmount: 100.00m,
                expectedCurrency: "SGD",
                actualAmount: 100.00m,
                actualCurrency: " "));
    }

    [Theory]
    [InlineData("SG")]
    [InlineData("SGDD")]
    [InlineData("S1D")]
    public void CompareRejectsInvalidExpectedCurrency(
        string currency)
    {
        Assert.Throws<ArgumentException>(
            () => ReconciliationComparison.Compare(
                expectedAmount: 100.00m,
                expectedCurrency: currency,
                actualAmount: 100.00m,
                actualCurrency: "SGD"));
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("U$D")]
    public void CompareRejectsInvalidActualCurrency(
        string currency)
    {
        Assert.Throws<ArgumentException>(
            () => ReconciliationComparison.Compare(
                expectedAmount: 100.00m,
                expectedCurrency: "SGD",
                actualAmount: 100.00m,
                actualCurrency: currency));
    }
}
