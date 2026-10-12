using SettleCore.Modules.Payments.Domain;

namespace SettleCore.Modules.Payments.UnitTests.Domain;

public sealed class PaymentCurrencyAmountConversionTests
{
    [Theory]
    [InlineData("123.45", "SGD", 12345L)]
    [InlineData("123.45", "sgd", 12345L)]
    [InlineData("123", "JPY", 123L)]
    [InlineData("123", "jpy", 123L)]
    [InlineData("123.456", "KWD", 123456L)]
    [InlineData("123.456", "kwd", 123456L)]
    public void ConvertsUsingCurrencyPrecision(string amount, string currency, long expected)
    {
        Assert.Equal(expected, PaymentAmountConversion.ToMinorUnits(Parse(amount), currency));
    }

    [Theory]
    [InlineData("1.001", "SGD")]
    [InlineData("1.1", "JPY")]
    [InlineData("1.0001", "KWD")]
    public void RejectsFractionalMinorUnits(string amount, string currency)
    {
        Assert.Throws<ArgumentException>(() => PaymentAmountConversion.ToMinorUnits(Parse(amount), currency));
    }

    [Theory]
    [InlineData("USD")]
    [InlineData(" SGD ")]
    [InlineData("")]
    public void RejectsUnsupportedOrMalformedCurrency(string currency)
    {
        var exception = Assert.Throws<ArgumentException>(() => PaymentAmountConversion.ToMinorUnits(1m, currency));
        Assert.Equal("currency", exception.ParamName);
    }

    [Fact]
    public void RejectsNullCurrency()
    {
        Assert.Throws<ArgumentNullException>(() => PaymentAmountConversion.ToMinorUnits(1m, null!));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("92233720368547758.08")]
    public void RejectsInvalidAmount(string amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PaymentAmountConversion.ToMinorUnits(Parse(amount), "SGD"));
    }

    private static decimal Parse(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
