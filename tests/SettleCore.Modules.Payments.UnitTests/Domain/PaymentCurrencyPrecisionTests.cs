using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.UnitTests.Domain;

public sealed class PaymentCurrencyPrecisionTests
{
    [Theory]
    [InlineData("SGD", 2)]
    [InlineData("sgd", 2)]
    [InlineData("JPY", 0)]
    [InlineData("jpy", 0)]
    [InlineData("KWD", 3)]
    [InlineData("kwd", 3)]
    public void ReturnsConfiguredDecimalPlaces(
        string currency,
        int expectedDecimalPlaces)
    {
        var decimalPlaces =
            PaymentCurrencyPrecision.GetDecimalPlaces(
                currency);

        Assert.Equal(
            expectedDecimalPlaces,
            decimalPlaces);
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("BTC")]
    public void RejectsUnsupportedCurrency(
        string currency)
    {
        var exception =
            Assert.Throws<ArgumentException>(
                () =>
                    PaymentCurrencyPrecision
                        .GetDecimalPlaces(currency));

        Assert.Equal(
            "currency",
            exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("SG")]
    [InlineData("SGDD")]
    [InlineData("S1D")]
    [InlineData(" SGD ")]
    public void RejectsMalformedCurrency(
        string currency)
    {
        Assert.Throws<ArgumentException>(
            () =>
                PaymentCurrencyPrecision
                    .GetDecimalPlaces(currency));
    }

    [Fact]
    public void RejectsNullCurrency()
    {
        Assert.Throws<ArgumentNullException>(
            () =>
                PaymentCurrencyPrecision
                    .GetDecimalPlaces(null!));
    }
}